using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using BetterInBlue.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes.Controllers;
using KamiToolKit.Nodes;
using KamiToolKit.Extensions;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;
using Action = Lumina.Excel.Sheets.Action;

namespace BetterInBlue;

public sealed unsafe class Plugin : IDalamudPlugin {
    public static Configuration Configuration = null!;

    public static ExcelSheet<Action> Action = null!;
    public static ExcelSheet<AozAction> AozAction = null!;
    public static ExcelSheet<AozActionTransient> AozActionTransient = null!;
    private static int Ticks;

    private static bool SpellbookOpen;

    private static int SpellsOnBar;
    private static int TotalSpellsSelected;
    private static List<uint> SelectedSpells = [];
    public readonly ConfigWindow ConfigWindow;
    public readonly MainWindow MainWindow;

    public readonly WindowSystem WindowSystem = new("BetterInBlue");

    private TextButtonNode? buttonOpenPlugin;
    private TextNode? textSpellsOnBar;
    private SearchInput? searchInput;

    public static string Name => "Better in Blue";
    private const string CommandName = "/pblue";
    public static RaptureHotbarModule* RaptureHotbar { get; private set; }
    public static AddonController<AddonAOZNotebook> BlueWindow { get; set; } = null!;
    public static AgentInterface* Agent { get; set; } = null!;
    public static Hook<AgentInterface.Delegates.ReceiveEvent>? ReceiveEventHook { get; set; }

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Services>();
        KamiToolKitLibrary.Initialize(Services.PluginInterface);
        BlueWindow = new AddonController<AddonAOZNotebook>("AOZNotebook");
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

// #if DEBUG
//         this.MainWindow.IsOpen = true;
//         // this.ConfigWindow.IsOpen = true;
// #endif

        // Services.AddonLifecycle.LogAddon("AOZNotebook", AddonEvent.PreReceiveEvent);
        Services.AddonLifecycle.LogAddon("AOZNotebookFilterSettings", AddonEvent.PreReceiveEvent,
                                         AddonEvent.PostReceiveEvent);

        Agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.AozNotebook);
        ReceiveEventHook = Services.GIP.HookFromAddress<AgentInterface.Delegates.ReceiveEvent>(
            Agent->VirtualTable->ReceiveEvent, AgentReceiveEvent);

        if (ReceiveEventHook.Address != nint.Zero) {
            ReceiveEventHook.Enable();
            Services.Log.Info("Hooked AOZNotebook ReceiveEvent");
        } else {
            Services.Log.Error("Failed to hook AOZNotebook ReceiveEvent");
        }

        // Agent->LogAgent(new LoggedAgentEvents {SuppressedEventKinds = [0, 1]});

        var vals = Enum.GetValues<NotebookFilterFlags>();

        foreach (var val in vals) {
            Services.Log.Info($"{val.ToString()} {(int) val}");
        }

        Services.ClientState.Login += OnLogin;
        Services.ClientState.Logout += OnLogout;
        Services.Framework.Update += this.FrameworkUpdate;
    }

    public static List<ulong> SuppressedEventKinds { get; set; } = [0];

    private static AtkValue* AgentReceiveEvent(
        AgentInterface* thisPtr, AtkValue* returnValue, AtkValue* values, uint valueCount, ulong eventKind
    ) {
        Services.Log.Info("Enter hook");
        try {
            var valueSpan = new Span<AtkValue>(values, (int) valueCount);
            if (!SuppressedEventKinds.Contains(eventKind)) {
                Services.Log.Info($"[{(nint) thisPtr:X}]: {eventKind}");
                valueSpan.PrintValues(2);
            }
        } catch (Exception e) {
            Services.Log.Error(e, "Exception in AgentReceiveEvent Logging Method");
        }


        return ReceiveEventHook!.Original(thisPtr, returnValue, values, valueCount, eventKind);
    }

    public void Dispose() {
        Services.ClientState.Login -= OnLogin;
        Services.ClientState.Logout -= OnLogout;

        // Services.AddonLifecycle.UnLogAddon("AOZNotebook");
        Services.AddonLifecycle.UnLogAddon("AOZNotebookFilterSettings");

        // Agent->UnLogAgent();
        ReceiveEventHook?.Dispose();

        Services.Framework.Update -= this.FrameworkUpdate;

        this.WindowSystem.RemoveAllWindows();
        this.MainWindow.Dispose();
        this.ConfigWindow.Dispose();

        Services.CommandManager.RemoveHandler(CommandName);

        BlueWindow.Dispose();
        KamiToolKitLibrary.Dispose();
    }

    private void ToggleMainWindow() {
        this.MainWindow.Toggle();
    }

    private void ToggleConfigWindow() {
        this.ConfigWindow.Toggle();
    }

    private static void OnLogin() {
        BlueWindow.Enable();
        if (Configuration.Loadouts.Count == 0) {
            ImportGamePresets();
        }
    }

    private static void OnLogout(int type, int code) {
        BlueWindow.Disable();
    }

    private static void GetCurrentBluSpells(ref List<uint> spellList) {
        spellList.Clear();
        for (var i = 0; i < 24; i++) {
            var id = ActionManager.Instance()->GetActiveBlueMageActionInSlot(i);
            if (id != 0)
                spellList.Add(id);
        }
    }

    private void FrameworkUpdate(IFramework framework) {
        if (!Services.ClientState.IsLoggedIn) return;
        if (++Ticks < 5) return;
        Ticks = 0;

        GetCurrentBluSpells(ref SelectedSpells);
        TotalSpellsSelected = SelectedSpells.Count;

        if (!SpellbookOpen) return;
        var selected = new HashSet<uint>(SelectedSpells);
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
        const int adjustedDown = 30;
        var spellNode = addon->GetNodeById(34);
        var buttonsNode = addon->GetNodeById(40);
        var spellsUsedCounter = addon->GetNodeById(37);

        //
        // foreach (var node in addon->WindowNode->Component->UldManager.Nodes) {
        //     Services.Log.Debug($"Node ID: {node.Value->NodeId}, Name: {node.Value->Type}");
        // }
        var nodeActiveSpellBg = addon->WindowNode->Component->UldManager.SearchNodeById(15);
        var nodePageL = addon->WindowNode->Component->UldManager.SearchNodeById(20);
        var nodePageR = addon->WindowNode->Component->UldManager.SearchNodeById(21);
        var collisionActiveSpells = addon->WindowNode->Component->UldManager.SearchNodeById(23);
        var collisionBook = addon->WindowNode->Component->UldManager.SearchNodeById(25);

        if (nodePageL is null || nodePageR is null || nodeActiveSpellBg is null || collisionActiveSpells is null ||
            collisionBook is null || spellNode is null || buttonsNode is null || spellsUsedCounter is null) {
            Services.Log.Error("Failed to find nodes for Better in Blue");
            return;
        }
        nodeActiveSpellBg->SetPositionFloat(nodeActiveSpellBg->X, nodeActiveSpellBg->Y + adjustedDown);
        collisionActiveSpells->SetPositionFloat(collisionActiveSpells->X, collisionActiveSpells->Y + adjustedDown);
        spellNode->SetPositionFloat(spellNode->X, spellNode->Y + adjustedDown);
        var pageHeight = (ushort) (nodePageR->GetHeight() + adjustedDown);
        nodePageL->SetHeight(pageHeight);
        nodePageR->SetHeight(pageHeight);
        collisionBook->SetHeight(pageHeight);

        var nodeResults = addon->GetNodeById(35);
        var nodeSpellDetails = addon->GetNodeById(36);

        var width = stackalloc short[1];
        var height = stackalloc short[1];

        addon->GetSize(width, height, false);
        height[0] += 50;

        Services.Log.Info($"{height[0]}, {width[0]}");

        addon->SetSize((ushort) *height, (ushort) *width);

        buttonsNode->SetPositionFloat((spellNode->Width - ((144 * 3) + (padding * 2))) / 2f, 148);
        spellsUsedCounter->SetPositionFloat(spellsUsedCounter->X - 185f, spellsUsedCounter->Y);

        this.searchInput = new SearchInput {
            Position = new Vector2(50, 50),
            Size = new Vector2(300f, 28.0f),
            OnInputReceived = searchString => OnBarSearch(addon, searchString.ToString()),
        };

        this.searchInput.AttachNode((AtkUnitBase*) addon);

        if (spellNode is not null) {
            this.buttonOpenPlugin = new TextButtonNode {
                Position = new Vector2(buttonsNode->X + buttonsNode->Width + padding, buttonsNode->Y),
                Size = new Vector2(144.0f, 28.0f),
                IsVisible = true,
                NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled |
                            NodeFlags.EmitsEvents,
                String = "Load Even More",
                Tooltip = "Added by Better in Blue",
                AddColor = new Vector3 {Z = 0.2f}, //slight blue tint
            };
            this.buttonOpenPlugin.BackgroundNode.PartsList[0]->UldAsset->AtkTexture.LoadTexture("ui/uld/ButtonA.tex");

            this.buttonOpenPlugin.AddEvent(AtkEventType.MouseClick, () => this.MainWindow.Toggle());
            this.buttonOpenPlugin.AttachNode(spellNode);

            this.textSpellsOnBar = new TextNode {
                FontSize = 11,
                Position = new Vector2(spellNode->Width - 185, spellsUsedCounter->Y),
                Size = new Vector2(190, spellsUsedCounter->Height),
                FontType = FontType.MiedingerMed,
                AlignmentType = AlignmentType.Left,
                Tooltip = "Spells added to your hotbars",
                String = "(00/00 on hotbars)",
                TextColor = KnownColor.White.Vector(),
                TextOutlineColor = KnownColor.CadetBlue.Vector(),
                TextFlags = TextFlags.Edge
            };

            this.textSpellsOnBar.AttachNode(spellNode);
        }
    }

    private void OnBarSearch(AddonAOZNotebook* addon, string searchString) {
        Services.Log.Info($"Searching for '{searchString}'");
        try {
            SendAgentFilter();
        } catch (Exception e) {
            Services.Log.Error(e, "Exception in OnBarSearch");
        }
    }

    private bool SendAgentFilter() {
        using var returnValue = new AtkValue();
        var command = stackalloc AtkValue[3];
        command[0].SetInt(0);
        command[1].SetInt((int) NotebookFilterFlags.Star1);
        command[2].SetManagedString("Water");

        Agent->ReceiveEvent(&returnValue, command, (uint) 3, 2);
        return false;
    }

    private void OnNodeUpdate(AddonAOZNotebook* addon) {
        if (this.textSpellsOnBar is not null)
            this.textSpellsOnBar.String = $"({SpellsOnBar}/{TotalSpellsSelected} on hotbars)";
    }

    private void DetachNodes(AddonAOZNotebook* addon) {
        this.buttonOpenPlugin?.Dispose();
        this.buttonOpenPlugin = null;

        this.textSpellsOnBar?.Dispose();
        this.textSpellsOnBar = null;

        this.searchInput?.Dispose();
        this.searchInput = null;

        SpellbookOpen = false;
    }

    public static IDalamudTextureWrap? GetIcon(uint id) {
        if (id == 0)
            return null;

        var found = AozAction.TryGetRow(id, out var row);
        if (!found)
            return null;
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
        var name = string.Join(" ", args).Trim().Trim('"', '\'');

        foreach (var loadout in Configuration.Loadouts)
            if (loadout.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)) {
                var errors = loadout.GetLoadoutErrors();
                if (errors.Count == 0) {
                    loadout.Apply();
                    UiHelpers.ShowNotification($"Loadout '{loadout.Name}' applied.");
                    return;
                }
                Services.ChatGui.PrintError($"Could not apply loadout - {errors.FirstOrDefault()}",
                                            Name, 705);
                return;
            }
        Services.ChatGui.PrintError("Could not find loadout.", Name, 705);
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

    public static bool AnyBluSpellOnCooldown() {
        var actionManager = ActionManager.Instance();
        foreach (var actionId in SelectedSpells) {
            var group = actionManager->GetRecastGroup(1, actionId);
            var detail = actionManager->GetRecastGroupDetail(group);
            if (detail != null && detail->Elapsed > 0) {
                return true;
            }
        }
        return false;
    }

    // Mighty Guard; Aetheric Mimicry: Tank, DPS, Healer; Basic Instinct
    public static readonly List<uint> PermanentBluStatuses = [1719, 2124, 2125, 2126, 2498];

    public static bool AnyBluStatusActive() {
        var player = Services.ObjectTable.LocalPlayer;
        if (player?.StatusList == null) {
            return false;
        }
        var character = (Character*) player.Address;
        var statusManager = character->GetStatusManager();

        for (var i = 0; i < statusManager->NumValidStatuses; i++) {
            ref var status = ref statusManager->Status[i];
            if (status.StatusId == 0) {
                continue;
            }
            if (PermanentBluStatuses.Contains(status.StatusId)) {
                return true;
            }
        }
        return false;
    }

    public static bool IsBluMage() {
        return Services.PlayerState.ClassJob.RowId == 36;
    }

    public static void ImportGamePresets(bool forceImportAll = false) {
        var m = AozNoteModule.Instance();
        if (m == null)
            return;
        Services.Log.Info("Importing game presets...");
        foreach (var set in m->ActiveSets) {
            var actions = set.ActiveActions.ToArray();
            if (actions.All(action => action == 0)) {
                continue;
            }
            // the game stores active actions using their Action ID, but we store the AOZ ID 
            actions = actions.ToArray().Select(NormalToAoz).ToArray();

            var loadout = new Loadout(set.CustomNameString) {Actions = actions};

            var hash = actions.FullHash();
            if (!forceImportAll && Configuration.Loadouts.Any(l => l.Actions.FullHash() == hash)) {
                Services.Log.Info($"Loadout {set.CustomNameString} already exists");
                continue;
            }

            Services.Log.Info($"Importing preset {set.CustomNameString}");

            for (uint hotbarNum = 0; hotbarNum < set.StandardHotBars.Length; hotbarNum++) {
                var config = ImportGamePresetHotbar(hotbarNum,
                                                    set.StandardHotBars[(int) hotbarNum].AozActionIds,
                                                    set.StandardHotBarMacroFlags[(int) hotbarNum].MacroFlags);
                if (config is not null) {
                    loadout.LoadoutHotbars.Add(config.Value);
                }
            }
            for (uint hotbarNum = 10; hotbarNum < set.CrossHotBars.Length; hotbarNum++) {
                var config = ImportGamePresetHotbar(hotbarNum,
                                                    set.CrossHotBars[(int) hotbarNum].AozActionIds,
                                                    set.CrossHotBarMacroFlags[(int) hotbarNum].MacroFlags);
                if (config is not null) {
                    loadout.LoadoutHotbars.Add(config.Value);
                }
            }
            Configuration.Loadouts.Add(loadout);
        }
        Configuration.Save();
    }

    private static HotbarConfig? ImportGamePresetHotbar(
        uint hotbarNum, Span<byte> spellIds, Span<bool> macroFlags
    ) {
        if (RaptureHotbar->IsHotbarShared(hotbarNum)) return null;
        var config = new HotbarConfig(hotbarNum, new HotbarSlot[spellIds.Length]);

        if (spellIds.ToArray().All(id => id == 0)) {
            return null;
        }
        for (var slotNum = 0; slotNum < spellIds.Length; slotNum++) {
            var id = spellIds[slotNum];
            var flag = macroFlags[slotNum];
            if (id == 0) {
                config.Slots[slotNum] = new HotbarSlot(0, HotbarSlotType.Empty);
                continue;
            }
            if (flag) {
                // if set this is actually a macro number that we must convert into a command id (RE'd)
                var macroId = id - 100 * ((id / 100) & 0x7FFFFFF);
                var commandId = (uint) (macroId + ((id / 100) << 8));
                config.Slots[slotNum] = new HotbarSlot(commandId, HotbarSlotType.Macro);
            } else {
                config.Slots[slotNum] = new HotbarSlot(AozToNormal(id), HotbarSlotType.Action);
            }
        }

        return config;
    }
}
