using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using BetterInBlue.Windows;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;
using Action = Lumina.Excel.Sheets.Action;

namespace BetterInBlue;

public sealed unsafe class Plugin : IDalamudPlugin {
    private const string CommandName = "/pblue";
    public static Configuration Configuration = null!;

    public static ExcelSheet<Action> Action = null!;
    public static ExcelSheet<AozAction> AozAction = null!;
    public static ExcelSheet<AozActionTransient> AozActionTransient = null!;
    private static int Ticks;

    private static bool SpellbookOpen;

    private static int SpellsOnBar;
    private static int TotalSpellsSelected;
    public readonly ConfigWindow ConfigWindow;
    public readonly MainWindow MainWindow;

    public readonly WindowSystem WindowSystem = new("BetterInBlue");

    private TextButtonNode? openPluginWindow;
    private TextNode? spellsOnBarText;

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Services>();
        NativeController = new NativeController(Services.PluginInterface);
        BlueWindow = new AddonController<AddonAOZNotebook>(Services.PluginInterface, "AOZNotebook");
        BlueWindow.OnAttach += this.AttachNode;
        BlueWindow.OnDetach += this.DetachNodes;
        BlueWindow.OnRefresh += this.OnNodeUpdate;
        BlueWindow.OnUpdate += this.OnNodeUpdate;

        RaptureHotbar = Framework.Instance()->UIModule->GetRaptureHotbarModule();

        if (Services.PluginInterface.GetPluginConfig() is not Configuration tempConfig) {
            Configuration = new Configuration();
            for (uint i = 0; i < Configuration.HotbarsStandard.Length; i++)
                Configuration.HotbarsStandard[i] = !RaptureHotbar->IsHotbarShared(i);
            Configuration.Save();
        } else Configuration = tempConfig;

        this.MainWindow = new MainWindow(this);
        this.ConfigWindow = new ConfigWindow(this);

        this.WindowSystem.AddWindow(this.MainWindow);
        this.WindowSystem.AddWindow(this.ConfigWindow);

        Services.PluginInterface.UiBuilder.OpenMainUi += this.ToggleMainWindow;
        Services.PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigWindow;

        // Adds another button that is doing the same but for the main ui of the plugin

        Services.CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommandInternal) {
            HelpMessage = "Opens the main menu."
        });

        Services.PluginInterface.UiBuilder.Draw += this.DrawUi;
        Services.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;

        Action = Services.DataManager.GetExcelSheet<Action>();
        AozAction = Services.DataManager.GetExcelSheet<AozAction>();
        AozActionTransient = Services.DataManager.GetExcelSheet<AozActionTransient>();

        if (Services.ClientState.IsLoggedIn) Services.Framework.RunOnFrameworkThread(OnLogin);

        // Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "_ActionBar", this.HotbarUpdate);

// #if DEBUG
//         this.MainWindow.IsOpen = true;
//         // this.ConfigWindow.IsOpen = true;
// #endif

        Services.ClientState.Login += OnLogin;
        Services.ClientState.Logout += OnLogout;
        Services.Framework.Update += this.FrameworkUpdate;
    }

    public string Name => "Better in Blue";
    public static NativeController NativeController { get; set; } = null!;
    public static RaptureHotbarModule* RaptureHotbar { get; private set; }

    public static AddonController<AddonAOZNotebook> BlueWindow { get; set; } = null!;

    public void Dispose() {
        Services.ClientState.Login -= OnLogin;
        Services.ClientState.Logout -= OnLogout;

        Services.Framework.Update -= this.FrameworkUpdate;

        this.WindowSystem.RemoveAllWindows();
        this.MainWindow.Dispose();
        this.ConfigWindow.Dispose();

        Services.CommandManager.RemoveHandler(CommandName);

        BlueWindow.OnAttach -= this.AttachNode;
        BlueWindow.OnDetach -= this.DetachNodes;
        BlueWindow.OnRefresh -= this.OnNodeUpdate;
        BlueWindow.OnUpdate -= this.OnNodeUpdate;

        BlueWindow.Dispose();
        NativeController.Dispose();
    }

    private void ToggleMainWindow() {
        this.MainWindow.Toggle();
    }

    private void ToggleConfigWindow() {
        this.ConfigWindow.Toggle();
    }

    private static void OnLogin() {
        BlueWindow.Enable();
    }

    private static void OnLogout(int type, int code) {
        BlueWindow.Disable();
    }

    private List<uint> GetCurrentBluSpells() {
        var result = new List<uint>();
        for (var i = 0; i < 24; i++) {
            var id = ActionManager.Instance()->GetActiveBlueMageActionInSlot(i);
            if (id != 0)
                result.Add(id);
        }
        return result;
    }

    private void FrameworkUpdate(IFramework framework) {
        if (!Services.ClientState.IsLoggedIn) return;
        if (++Ticks < 5) return;
        Ticks = 0;

        var selected = this.GetCurrentBluSpells();
        TotalSpellsSelected = selected.Count;

        if (!SpellbookOpen) return;
        for (uint hotbarNum = 0; hotbarNum < 17; hotbarNum++) {
            if (RaptureHotbar->IsHotbarShared(hotbarNum)) continue;
            var maxSlots = hotbarNum >= 10 ? 12 : 16;
            for (uint slotNum = 0; slotNum < maxSlots; slotNum++) {
                var slot = RaptureHotbar->GetSlotById(hotbarNum, slotNum);
                if (slot->CommandType == HotbarSlotType.Empty)
                    continue;
                if (slot->OriginalApparentSlotType == HotbarSlotType.Action)
                    selected.Remove(slot->OriginalApparentActionId);
            }
        }
        SpellsOnBar = TotalSpellsSelected - selected.Count;
    }


    private void AttachNode(AddonAOZNotebook* addon) {
        SpellbookOpen = true;

        const int padding = 24;

        var spellNode = addon->GetNodeById(34);
        var buttonsNode = addon->GetNodeById(40);
        var spellsUsedCounter = addon->GetNodeById(37);

        buttonsNode->SetPositionFloat((spellNode->Width - ((144 * 3) + (padding * 2))) / 2f, 148);
        spellsUsedCounter->SetPositionFloat(spellsUsedCounter->X - 185f, spellsUsedCounter->Y);

        if (spellNode is not null) {
            this.openPluginWindow = new TextButtonNode {
                Position = new Vector2(buttonsNode->X + buttonsNode->Width + padding, buttonsNode->Y),
                Size = new Vector2(144.0f, 28.0f),
                IsVisible = true,
                NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled |
                            NodeFlags.EmitsEvents,
                Label = "Load Even More",
                Tooltip = "Added by Better in Blue"
            };
            this.openPluginWindow.BackgroundNode.PartsList[0].LoadTexture("ui/uld/ButtonA.tex", false);
            this.openPluginWindow.AddColor = new Vector3 {Z = 0.2f}; //slight blue tint

            this.openPluginWindow.AddEvent(AddonEventType.ButtonClick, _ => this.MainWindow.Toggle());
            NativeController.AttachNode(this.openPluginWindow, spellNode);

            this.spellsOnBarText = new TextNode {
                FontSize = 11,
                Position = new Vector2(spellNode->Width - 185, spellsUsedCounter->Y),
                Size = new Vector2(190, spellsUsedCounter->Height),
                FontType = FontType.MiedingerMed,
                AlignmentType = AlignmentType.Left,
                Tooltip = "Spells added to your hotbars",
                Text = "(00/00 on hotbars)",
                EnableEventFlags = true,
                TextColor = KnownColor.White.Vector(),
                TextOutlineColor = KnownColor.CadetBlue.Vector(),
                TextFlags = TextFlags.Edge
            };

            NativeController.AttachNode(this.spellsOnBarText, spellNode);
        }
    }

    private void OnNodeUpdate(AddonAOZNotebook* addon) {
        // Services.Log.Debug("OnNodeUpdate");
        if (this.spellsOnBarText is not null)
            this.spellsOnBarText.Text = $"({SpellsOnBar}/{TotalSpellsSelected} on hotbars)";
    }

    private void DetachNodes(AddonAOZNotebook* addon) {
        NativeController.DetachNode(this.openPluginWindow, () => {
            this.openPluginWindow?.Dispose();
            this.openPluginWindow = null;
        });

        NativeController.DetachNode(this.spellsOnBarText, () => {
            this.spellsOnBarText?.Dispose();
            this.spellsOnBarText = null;
        });

        SpellbookOpen = false;
    }

    public static IDalamudTextureWrap GetIcon(uint id) {
        if (id == 0) return Services.TextureProvider.GetFromGame("ui/uld/DragTargetA_hr1.tex").GetWrapOrEmpty();

        var row = AozAction.GetRow(id);
        var transient = AozActionTransient.GetRow(row.RowId);
        var icon = Services.TextureProvider.GetFromGameIcon(transient.Icon).GetWrapOrEmpty();
        return icon;
    }

    private void OnCommandInternal(string _, string args) {
        args = args.ToLower();
        this.OnCommand(args.Split(' ').ToList());
    }

    private void OnCommand(List<string> args) {
        switch (args[0]) {
            case "config":
            case "settings":
                this.OpenConfigUi();
                break;
            case "apply":
            case "load":
                ApplyLoadoutByName(args.Skip(1).ToList());
                break;

            default:
                this.MainWindow.IsOpen = true;
                break;
        }
    }

    private static void ApplyLoadoutByName(List<string> args) {
        var name = string.Join(" ", args).Trim();
        foreach (var loadout in Configuration.Loadouts)
            if (loadout.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                loadout.Apply();
    }

    private void DrawUi() {
        this.WindowSystem.Draw();
    }

    public void OpenConfigUi() {
        this.ConfigWindow.IsOpen = true;
    }

    public static uint AozToNormal(uint id) {
        return id == 0 ? 0 : AozAction.GetRow(id).Action.RowId;
    }

    public static uint NormalToAoz(uint id) {
        var res = AozAction.FirstOrNull(aoz => aoz.Action.RowId == id);
        if (res == null) throw new Exception("https://tenor.com/view/8032213");
        return res.Value.RowId;
    }
}
