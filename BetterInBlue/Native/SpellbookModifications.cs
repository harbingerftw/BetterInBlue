using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace BetterInBlue.Native;

public unsafe class SpellbookModifications : IDisposable {
    private readonly Plugin plugin;
    private static RaptureHotbarModule* RaptureHotbar { get; set; }
    private AddonController<AddonAOZNotebook> BlueWindow { get; set; }
    private static bool SpellbookOpen;
    private static int SpellsOnBar;
    private static int TotalSpellsSelected;
    private static List<uint> SelectedSpells = [];
    private static int Ticks;

    private TextButtonNode? buttonOpenPlugin;
    private TextNode? textSpellsOnBar;
    private SearchInput? searchInput;

    private string? previousSearchString;
    private NotebookFilterFlags? previousFilterFlags;

    private bool enterPressed;

    public SpellbookModifications(Plugin plugin) {
        this.plugin = plugin;
        KamiToolKitLibrary.Initialize(Services.PluginInterface);
        BlueWindow = new AddonController<AddonAOZNotebook> {
            AddonName = "AOZNotebook",
            OnSetup = this.SetupNode,
            OnRefresh = this.OnNodeRefresh,
            OnUpdate = this.OnNodeRefresh,
            OnFinalize = this.OnFinalizeNode,
        };

        Services.Framework.Update += FrameworkUpdate;
        RaptureHotbar = RaptureHotbar = Framework.Instance()->UIModule->GetRaptureHotbarModule();
    }

    private void SetupNode(AddonAOZNotebook* addon) {
        SpellbookOpen = true;

        const int padding = 24;
        const int adjustedDown = 20;
        var spellNode = addon->GetNodeById(34);
        var buttonsNode = addon->GetNodeById(40);
        var spellsUsedCounter = addon->GetNodeById(37);
        var nodeActiveSpellBg = addon->GetWindowNodeById(15);

        var nodePageL = addon->GetWindowNodeById(20);
        var pageLCheckerboard = addon->GetWindowNodeById(19);
        var nodePageR = addon->GetWindowNodeById(21);
        var collisionActiveSpells = addon->GetWindowNodeById(23);
        var collisionBook = addon->GetWindowNodeById(25);
        var spellsGrid = addon->GetNodeById(5);

        if (nodePageL is null || nodePageR is null || nodeActiveSpellBg is null || collisionActiveSpells is null ||
            collisionBook is null || spellNode is null || buttonsNode is null || spellsUsedCounter is null ||
            pageLCheckerboard is null || spellsGrid is null) {
            Services.Log.Error("Failed to find nodes for Better in Blue");
            return;
        }

        var width = stackalloc ushort[1];
        var height = stackalloc ushort[1];
        addon->GetSize(width, height, false);
        height[0] += 60;
        addon->SetSize(width: *height, height: *width);
        Services.Log.Info($"addon size: {height[0]}, {width[0]}");

        if (*height <= 740) {
            nodeActiveSpellBg->SetPositionFloat(nodeActiveSpellBg->X, nodeActiveSpellBg->Y + adjustedDown);
            collisionActiveSpells->SetPositionFloat(collisionActiveSpells->X, collisionActiveSpells->Y + adjustedDown);
            spellNode->SetPositionFloat(spellNode->X, spellNode->Y + adjustedDown);
            spellsGrid->SetPositionFloat(spellsGrid->X, spellsGrid->Y + adjustedDown);
            var pageHeight = (ushort) (nodePageR->GetHeight() + adjustedDown);
            nodePageL->SetHeight(pageHeight);
            pageLCheckerboard->SetPositionFloat(pageLCheckerboard->X, pageLCheckerboard->Y + adjustedDown);
            nodePageR->SetHeight(pageHeight);
            collisionBook->SetHeight(pageHeight);

            // var nodeResults = addon->GetNodeById(35);
            // var nodeSpellDetails = addon->GetNodeById(36);

            buttonsNode->SetPositionFloat((spellNode->Width - ((144 * 3) + (padding * 2))) / 2f, 148);
            spellsUsedCounter->SetPositionFloat(spellsUsedCounter->X - 185f, spellsUsedCounter->Y);
        }

        var extra = $"{(Plugin.Configuration.SearchOnEnter ? "Press Enter to Search - " : "")}Added by Better In Blue";

        searchInput = new SearchInput(CreateDefaultTooltip(), new Vector2(285, 100)) {
            Position = new Vector2(52, 64),
            Size = new Vector2(290f, 28.0f),
            OnInputReceived = searchString => OnBarSearch(addon, searchString.ToString()),
            OnComplete = OnSearchComplete,
            ExtraTooltip = extra
        };

        if (!spellsGrid->IsVisible()) {
            searchInput.IsVisible = false;
        }

        searchInput.AttachNode((AtkUnitBase*) addon);

        if (spellNode is not null) {
            buttonOpenPlugin = new TextButtonNode {
                Position = new Vector2(buttonsNode->X + buttonsNode->Width + padding, buttonsNode->Y),
                Size = new Vector2(144.0f, 28.0f),
                IsVisible = true,
                NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled |
                            NodeFlags.EmitsEvents,
                String = "Load Even More",
                TextTooltip = "Added by Better in Blue",
                AddColor = new Vector3 {Z = 0.2f}, //slight blue tint
            };
            buttonOpenPlugin.BackgroundNode.PartsList[0]->UldAsset->AtkTexture.LoadTexture("ui/uld/ButtonA.tex");

            buttonOpenPlugin.AddEvent(AtkEventType.MouseClick, () => plugin.MainWindow.Toggle());
            buttonOpenPlugin.AttachNode(spellNode);

            textSpellsOnBar = new TextNode {
                FontSize = 11,
                Position = new Vector2(spellNode->Width - 185, spellsUsedCounter->Y),
                Size = new Vector2(190, spellsUsedCounter->Height),
                FontType = FontType.MiedingerMed,
                AlignmentType = AlignmentType.Left,
                TextTooltip = "Spells added to your hotbars",
                String = "(00/00 on hotbars)",
                TextColor = KnownColor.White.Vector(),
                TextOutlineColor = KnownColor.CadetBlue.Vector(),
                TextFlags = TextFlags.Edge
            };

            textSpellsOnBar.AttachNode(spellNode);
        }
    }

    private void OnSearchComplete(ReadOnlySeString search) {
        enterPressed = true;
        OnBarSearch(null, search.ToString());
        enterPressed = false;
    }

    private (ReadOnlySeString, ReadOnlySeString) OnBarSearch(AddonAOZNotebook* addon, string searchString) {
        // Services.Log.Info($"Searching for '{searchString}'");
        ReadOnlySeString text = searchString;
        ReadOnlySeString tooltip = CreateDefaultTooltip();
        try {
            var results = KeywordParser.TryParse(searchString, out var success);
            var inputBuilder = new SeStringBuilder();
            var tooltipBuilder = new SeStringBuilder();
            searchInput?.InputIsError = false;
            if (!success) {
                inputBuilder.PushColorType(KeywordParser.DefaultErrorColor)
                            .Append(searchString)
                            .PopColorType();
                searchInput?.InputIsError = true;
                return (inputBuilder.ToReadOnlySeString(), ReadOnlySeString.FromText("Invalid input!"));
            }

            var nonMatchString = "";
            NotebookFilterFlags flags = 0;
            bool shouldSearch = true;

            for (var index = 0; index < results.Count; index++) {
                var m = results[index];

                flags |= m.Flag;
                // Services.Log.Debug($"   => formatting: {m.Index} - {m.End} - {searchString.Length}");
                if (index == 0 && m.Index != 0) {
                    nonMatchString += searchString.AsSpan(0, m.Index).ToString();
                    inputBuilder.Append(searchString.AsSpan(0, m.Index));
                }

                inputBuilder.PushColorType(m.Color)
                            .Append(searchString.AsSpan(m.Index, m.Length))
                            .PopColorType();

                if (index + 1 < results.Count && results[index + 1].Index != m.End) {
                    // Services.Log.Debug($"inserting {results[index + 1].Index} {m.End}");
                    nonMatchString += searchString.AsSpan(m.End, results[index + 1].Index - m.End).ToString();
                    inputBuilder.Append(searchString.AsSpan(m.End, results[index + 1].Index - m.End));
                }

                // Services.Log.Debug($"end check {index} =? {results.Count - 1} && {m.End} =? {searchString.Length}");
                if (index == results.Count - 1 && m.End != searchString.Length) {
                    // Services.Log.Debug($"inserting at end {m.End} {searchString.Length} ({searchString.Length - m.End})");
                    nonMatchString += searchString.AsSpan(m.End, searchString.Length - m.End).ToString();
                    inputBuilder.Append(searchString.AsSpan(m.End, searchString.Length - m.End));
                }

                if (m.Partial) {
                    // Services.Log.Debug($"partial match: {m.Category}");
                    shouldSearch = false;
                    var cat = KeywordParser.Groups[m.Category];
                    var choices = cat.Flags;
                    for (var i = 0; i < choices.Length; i++) {
                        var flag = choices[i];
                        var flagName = cat.FlagNames[i];
                        Services.Log.Debug($" {flag} => {flag.GetDisplay()}");
                        tooltipBuilder.PushColorType(flag.GetColor())
                                      .PushEdgeColorType(29)
                                      .Append(flagName)
                                      .PopEdgeColorType()
                                      .PopColorType();

                        if (i < choices.Length - 1) {
                            tooltipBuilder.Append(", ");
                        }
                    }

                    tooltip = tooltipBuilder.ToReadOnlySeString();
                }
            }

            if (results.Count == 0) {
                inputBuilder.Append(searchString);
                nonMatchString = searchString;
            }

            text = inputBuilder.ToReadOnlySeString();
            nonMatchString = nonMatchString.Trim();
            // Services.Log.Debug($"non matched: {nonMatchString} || output text: {text.ToMacroString()}");
            // if (nonMatchString.Length > 0 || flags != 0) {
            if (shouldSearch) {
                if ((Plugin.Configuration.SearchOnEnter && this.enterPressed)
                    || (!Plugin.Configuration.SearchOnEnter &&
                        (previousSearchString != searchString || previousFilterFlags != flags))) {
                    plugin.SendAgentFilter(nonMatchString, flags);
                }
            }
            
            previousSearchString ??= searchString;
            previousFilterFlags ??= flags;
        } catch (Exception e) {
            Services.Log.Error(e, "Exception in OnBarSearch");
        }

        return (text, tooltip);
    }

    private void OnNodeRefresh(AddonAOZNotebook* addon) {
        textSpellsOnBar?.String = $"({SpellsOnBar}/{TotalSpellsSelected} on hotbars)";
        if (searchInput is not null) {
            var spellsGrid = addon->GetNodeById(5);
            searchInput.IsVisible = spellsGrid != null && spellsGrid->IsVisible();
        }
    }

    private void OnFinalizeNode(AddonAOZNotebook* addon) {
        buttonOpenPlugin?.Dispose();
        buttonOpenPlugin = null;

        textSpellsOnBar?.Dispose();
        textSpellsOnBar = null;

        searchInput?.Dispose();
        searchInput = null;

        SpellbookOpen = false;
    }

    private void GetCurrentBluSpells(ref List<uint> spellList) {
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
                if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
                    continue;
                if (slot->OriginalApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action)
                    selected.Remove(slot->OriginalApparentActionId);
            }
        }

        SpellsOnBar = TotalSpellsSelected - selected.Count;
    }

    public bool AnyBluSpellOnCooldown() {
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

    private ReadOnlySeString CreateDefaultTooltip() {
        var val = new SeStringBuilder()
                  .Append("Search using spell names or <attribute>:<value> where attribute is: ")
                  .AppendNewLine();

        var groupsList = KeywordParser.Groups.ToList();
        for (var i = 0; i < groupsList.Count; i++) {
            var (_, group) = groupsList[i];
            val.PushColorType(28)
               .PushEdgeColorType(29)
               .Append(group.Category.GetDisplay())
               .PopEdgeColorType()
               .PopColorType();

            if (i < groupsList.Count - 1) {
                val.Append(", ");
            }
        }

        val.AppendNewLine()
           .Append("For example: 'aspect:fire'");

        return val.ToReadOnlySeString();
    }

    public void Enable() {
        BlueWindow.Enable();
    }

    public void Disable() {
        BlueWindow.Disable();
    }

    public void Dispose() {
        BlueWindow.Dispose();
        KamiToolKitLibrary.Dispose();

        Services.Framework.Update -= FrameworkUpdate;
    }
}
