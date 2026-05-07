using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace BetterInBlue.Windows;

public class MainWindow : Window, IDisposable {
    private readonly Plugin plugin;
    private int editing;
    private string searchFilter = string.Empty;
    private Loadout? selectedLoadout;
    private bool shouldOpen;
    private int dragDropIndex = -1;
    private Action? endDropAction;

    public MainWindow(Plugin plugin) : base("Better in Blue") {
        this.plugin = plugin;

        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(500, 400),
            MaximumSize = new Vector2(-1, -1)
        };
        Size = new Vector2(500, 400);
    }

    public void Dispose() {
    }

    public override void Draw() {
        var cra = ImGui.GetContentRegionAvail();
        var sidebar = cra with { X = Math.Max(cra.X * 0.25f, 200f) };
        var editor = cra with { X = cra.X * 0.75f };

        DrawSidebar(sidebar);
        ImGui.SameLine();
        DrawEditor(editor);

        if (shouldOpen) {
            ImGui.OpenPopup("ActionContextMenu");
            shouldOpen = false;
        }

        DrawContextMenu();

        endDropAction?.Invoke();
        endDropAction = null;
    }

    private unsafe void DrawSidebar(Vector2 size) {
        if (ImGui.BeginChild("Sidebar", size, true)) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
                Plugin.Configuration.Loadouts.Add(
                    new Loadout($"Unnamed Loadout {Plugin.Configuration.Loadouts.Count + 1}"));
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Create empty loadout.");
            ImGui.SameLine();


            if (UiHelpers.DisabledIconButtonWithTooltip(
                    FontAwesomeIcon.FileCirclePlus,
                    !Plugin.IsBluMage(),
                    "Create preset from current spell loadout and hotbars",
                    "Must be on Blue Mage to create loadout."
                )) {
                var loadout = new Loadout($"Unnamed Loadout {Plugin.Configuration.Loadouts.Count + 1}");
                loadout.SaveHotbars();
                for (var i = 0; i < 24; i++)
                    loadout.Actions.SetValue(
                        Plugin.NormalToAoz(ActionManager.Instance()->GetActiveBlueMageActionInSlot(i)), i);
                Plugin.Configuration.Loadouts.Add(loadout);
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport)) {
                var maybeLoadout = Loadout.FromPreset(ImGui.GetClipboardText());
                if (maybeLoadout != null) {
                    Plugin.Configuration.Loadouts.Add(maybeLoadout);
                    Plugin.Configuration.Save();
                }
                else
                    UiHelpers.ShowNotification("Failed to load preset from clipboard.", type: NotificationType.Error);
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Load a preset from the clipboard.");
            ImGui.SameLine();

            ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - ImGui.GetFrameHeight(),
                ImGui.GetCursorPosY()));

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog)) {
                plugin.OpenConfigUi();
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open the config window.");
            ImGui.Separator();

            for (var index = 0; index < Plugin.Configuration.Loadouts.Count; index++) {
                var loadout = Plugin.Configuration.Loadouts[index];
                var label = $"{loadout.Name}##{loadout.GetHashCode()}";
                if (ImGui.Selectable(label, loadout == selectedLoadout, ImGuiSelectableFlags.AllowDoubleClick)) {
                    selectedLoadout = loadout;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                        var errors = loadout.GetLoadoutErrors();
                        if (errors.Count == 0) {
                            if (selectedLoadout.Apply()) {
                                UiHelpers.ShowNotification($"Loadout '{selectedLoadout.Name}' applied.");
                            }
                            else
                                UiHelpers.ShowNotification(
                                    "You should have gotten an error message on screen explaining why. If not, please report this!",
                                    "Failed to apply loadout",
                                    NotificationType.Error);
                        }
                        else {
                            Services.ChatGui.PrintError($"Could not apply loadout - {errors.FirstOrDefault()}",
                                Plugin.Name, 705);
                        }
                    }
                }

                DrawDragDrop(Plugin.Configuration.Loadouts, index);
            }

            ImGui.EndChild();
        }
    }

    private void DrawEditor(Vector2 size) {
        if (selectedLoadout == null) {
            var first = Plugin.Configuration.Loadouts.FirstOrDefault();
            if (first != null) selectedLoadout = first;
            else return;
        }

        var green = new Vector4(0.2f, 0.5f, 0.2f, 1);


        if (ImGui.BeginChild("Editor", size)) {
            var applyErrors = selectedLoadout.GetLoadoutErrors();

            using (ImRaii.PushColor(ImGuiCol.Button, green)
                       .Push(ImGuiCol.ButtonActive, green)
                       .Push(ImGuiCol.ButtonHovered, green.Lighten(0.1f))) {
                using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f, applyErrors.Count != 0)) {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Apply")) {
                        if (selectedLoadout.Apply()) {
                            UiHelpers.ShowNotification($"Loadout '{selectedLoadout.Name}' applied.");
                        }
                        else {
                            UiHelpers.ShowNotification(
                                "You should have gotten an error message on screen explaining why. If not, please report this!",
                                "Failed to apply loadout",
                                NotificationType.Error);
                        }
                    }
                }

                if (ImGui.IsItemHovered()) {
                    using (ImRaii.Tooltip()) {
                        if (applyErrors.Count == 0)
                            ImGui.Text("Apply the current loadout & saved hotbars.");
                        else {
                            ImGui.Text("You must meet all of the following conditions to apply:");
                            applyErrors.ForEach((e) => ImGui.TextColored(KnownColor.Red.Vector(), $"- {e}"));
                        }
                    }
                }
            }

            ImGui.SameLine();

            var canDelete = ImGui.GetIO().KeyCtrl;
            if (UiHelpers.DisabledIconButtonWithTooltip(
                    FontAwesomeIcon.Trash,
                    !canDelete,
                    "Delete Loadout (you can't undo this!)",
                    "Delete Loadout. Hold Ctrl to enable the delete button."
                )) {
                Plugin.Configuration.Loadouts.Remove(selectedLoadout);
                Plugin.Configuration.Save();

                selectedLoadout = null;
                ImGui.EndChild();
                return;
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileExport)) {
                ImGui.SetClipboardText(selectedLoadout.ToPreset());
                UiHelpers.ShowNotification("Copied loadout to clipboard\n" +
                                           "Consider sharing it in #preset-sharing in the Dalamud Discord server!");
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy spell loadout to your clipboard.");

            ImGui.Dummy(new Vector2(0, 10));
            // Can't ref the damn name, I hate getter/setters
            var name = selectedLoadout.Name;
            if (ImGui.InputText("Name", ref name, 256)) {
                selectedLoadout.Name = name;
                Plugin.Configuration.Save();
            }

            ImGui.Separator();

            for (var i = 0; i < 12; i++) {
                DrawSpellSlot(i);
                ImGui.SameLine();
            }

            ImGui.NewLine();

            for (var i = 12; i < 24; i++) {
                DrawSpellSlot(i);
                ImGui.SameLine();
            }

            ImGui.NewLine();
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 40));

            var isBluMage = Plugin.IsBluMage();

            using (ImRaii.Disabled(!isBluMage || selectedLoadout.LoadoutHotbars.Count == 0)) {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.PlayCircle, "Apply Hotbars")) {
                    selectedLoadout.ApplyToHotbars();
                    UiHelpers.ShowNotification($"Hotbars from '{selectedLoadout.Name}' applied.");
                }
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Apply the hotbars in this preset to your current hotbars.");

            ImGui.SameLine();

            using (ImRaii.Disabled(!isBluMage))
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Updated Saved Hotbars")) {
                    selectedLoadout.LoadoutHotbars.Clear();
                    selectedLoadout.SaveHotbars();
                    UiHelpers.ShowNotification($"Hotbars saved to preset '{selectedLoadout.Name}'.");
                    Plugin.Configuration.Save();
                }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save your current hotbars to this loadout.");

            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Text("Hotbars Saved:");
            ImGui.SameLine();

            var bars =
                selectedLoadout.LoadoutHotbars.Select(x => x.Id >= 10
                    ? $"Cross Hotbar {x.Id - 9}"
                    : $"Hotbar {x.Id + 1}").ToList();
            var color = KnownColor.LightGray.Vector();
            if (bars.Count == 0)
                ImGui.Text("None");
            else {
                var maxWindowSize = ImGui.GetWindowContentRegionMax();

                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 0, 0, 1))
                           .Push(ImGuiCol.Button, color)
                           .Push(ImGuiCol.ButtonActive, color)
                           .Push(ImGuiCol.ButtonHovered, color)) {
                    foreach (var barStr in bars) {
                        var startCursor = ImGui.GetCursorPos();
                        ImGui.SmallButton(barStr);
                        var previousSize = ImGui.GetItemRectSize();

                        if (maxWindowSize.X - (startCursor.X + previousSize.X) <= 120) {
                            ImGui.SetCursorPos(new Vector2(0, ImGui.GetCursorPosY() + (previousSize.Y / 2.5f)));
                        }
                        else {
                            ImGui.SameLine();
                        }
                    }
                }
            }


            ImGui.EndChild();
        }
    }

    private void DrawDragDrop(List<Loadout> list, int index) {
        const string dragDropLabel = "BlueLoadoutDragDrop";

        using (var target = ImRaii.DragDropTarget()) {
            if (target.Success && !ImGui.AcceptDragDropPayload(dragDropLabel).IsNull) {
                if (dragDropIndex >= 0) {
                    var i = dragDropIndex;
                    endDropAction = () => list.Move(i, index);
                }

                dragDropIndex = -1;
            }
        }


        using (var source = ImRaii.DragDropSource()) {
            if (source) {
                ImGui.Text($"Move {list[index].Name} here...");
                if (ImGui.SetDragDropPayload(dragDropLabel, null)) {
                    dragDropIndex = index;
                }
            }
        }
    }


    private void DrawSpellSlot(int index) {
        if (selectedLoadout == null) return;
        var current = selectedLoadout.Actions[index];
        var icon = Plugin.GetIcon(current)
                   ?? Services.TextureProvider.GetFromGame("ui/uld/DragTargetA_hr1.tex").GetWrapOrEmpty();

        ImGui.Image(icon.Handle, new Vector2(48, 48));
        if (ImGui.IsItemHovered() && current != 0) {
            var action = Plugin.AozToNormal(current);
            ImGui.SetTooltip($"{Plugin.Action.GetRow(action).Name.ExtractText()} (#{current})" +
                             $"\n\n(Left click to change action; right click to remove)");
        }
        else if (ImGui.IsItemHovered()) ImGui.SetTooltip("Left click to add action.");

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            editing = index;
            searchFilter = string.Empty;
            // Why does OpenPopup not work here? I dunno!
            shouldOpen = true;
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            selectedLoadout.Actions[index] = 0;
            Plugin.Configuration.Save();
        }
    }

    private void DrawContextMenu() {
        if (ImGui.BeginPopup("ActionContextMenu")) {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##Search", "Search...", ref searchFilter, 256);

            if (ImGui.BeginChild("ActionList", new Vector2(340, 256))) {
                foreach (var actionTransient in Plugin.AozActionTransient.OrderBy(x => x.Number)) {
                    if (actionTransient.RowId == 0) continue;
                    var action = Plugin.Action.GetRow(actionTransient.RowId);
                    var aozAction = Plugin.AozAction.GetRow(actionTransient.RowId);

                    var listName = aozAction.Action.Value.Name.ToString();
                    var listId = actionTransient.Number;
                    var listIcon = Plugin.GetIcon(action.RowId)
                                   ?? Services.TextureProvider
                                       .GetFromGame("ui/uld/DragTargetA_hr1.tex")
                                       .GetWrapOrEmpty();

                    var validInt = int.TryParse(searchFilter, out _);

                    var meetsSearchFilter = string.IsNullOrEmpty(searchFilter)
                                            || (validInt && listId.ToString().StartsWith(searchFilter))
                                            || listName.Contains(searchFilter,
                                                StringComparison.CurrentCultureIgnoreCase);
                    if (!meetsSearchFilter) continue;

                    var rowHeight = ImGui.GetTextLineHeightWithSpacing();

                    ImGui.Image(listIcon.Handle, new Vector2(rowHeight, rowHeight));
                    ImGui.SameLine();

                    var tooManyOfAction = selectedLoadout!.ActionCount(action.RowId) > 0;
                    var notUnlocked = !selectedLoadout!.ActionUnlocked(action.RowId);

                    var locked = tooManyOfAction || notUnlocked;
                    var flags = locked
                        ? ImGuiSelectableFlags.Disabled
                        : ImGuiSelectableFlags.None;
                    // var act = Plugin.AozActionTransient.GetRow(action.RowId);
                    if (ImGui.Selectable($"{listName} #{listId}", false, flags)) {
                        selectedLoadout!.Actions[editing] = action.RowId;
                        Plugin.Configuration.Save();
                        ImGui.CloseCurrentPopup();
                    }

                    // Can't hover a disabled Selectable, other UI element it is then
                    if (locked) {
                        ImGui.SameLine();
                        {
                            using var icon = ImRaii.PushFont(UiBuilder.IconFont);
                            using var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                            ImGui.TextUnformatted(FontAwesomeIcon.ExclamationTriangle.ToIconString());
                        }
                        if (ImGui.IsItemHovered()) {
                            var str = "Issues:\n";

                            if (tooManyOfAction)
                                str += "- This loadout already has this action, so you can't add it twice.";

                            if (notUnlocked)
                                str += "- You haven't unlocked this action yet.";

                            ImGui.SetTooltip(str.Trim());
                        }
                    }
                }

                ImGui.EndChild();
            }

            ImGui.EndPopup();
        }
    }
}
