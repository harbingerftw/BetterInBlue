using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace BetterInBlue.Windows;

public class ConfigWindow : Window, IDisposable {
    private Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Better in Blue Config") {
        this.plugin = plugin;

        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(350f, 400f),
            MaximumSize = new Vector2(700f, 700f)
        };
        Size = new Vector2(500, 400);
    }

    public override void Draw() {
        using var tabBar = ImRaii.TabBar("##ConfigTabs");
        if (!tabBar.Success)
            return;

        GeneralTab();
        NormalHotbarTab();
        CrossHotbarTab();
    }

    public void Dispose() { }

    private void GeneralTab() {
        using var tabItem = ImRaii.TabItem("General");
        if (!tabItem.Success)
            return;

        ImGui.TextColored(KnownColor.DeepSkyBlue.Vector(), "Applying Loadouts:");
        using (ImRaii.PushIndent(10.0f))
            if (ImGui.Checkbox("Double Click to apply loadouts##db_apply", ref Plugin.Configuration.DoubleClickApply))
                Plugin.Configuration.Save();

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(KnownColor.DeepSkyBlue.Vector(), "Hotbar Restoration:");

        using (ImRaii.PushIndent(10.0f)) {
            if (ImGui.Checkbox("Clear hotbar slots if empty in loadout", ref Plugin.Configuration.OverwriteActionsWithBlank))
                Plugin.Configuration.Save();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Hotbar contents will be cleared if the loadout is empty in that slot.");

            if (ImGui.Checkbox("Overwrite only Blue Mage actions on hotbars",
                               ref Plugin.Configuration.OverwriteOnlyBluActions))
                Plugin.Configuration.Save();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Blue Mage actions will also be cleared if the loadout is empty in that slot.");

            ImGuiHelpers.ScaledDummy(5.0f);

            if (Plugin.Configuration.OverwriteActionsWithBlank || !Plugin.Configuration.OverwriteOnlyBluActions) {
                using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange)) {
                    ImGui.Text("Warning!");
                    ImGui.SameLine();
                    using (ImRaii.PushFont(UiBuilder.IconFont)) {
                        ImGui.Text(FontAwesomeIcon.ExclamationTriangle.ToIconString());
                    }
                    ImGui.TextWrapped("This configuration could overwrite any actions on the selected hotbars! " +
                               "Make sure to only select hotbars that you want to chagne.");
                }
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(KnownColor.DeepSkyBlue.Vector(), "Native Spellbook Modifications:");
        using (ImRaii.PushIndent(10.0f))
            if (ImGui.Checkbox("Execute search on enter", ref Plugin.Configuration.SearchOnEnter))
                Plugin.Configuration.Save();

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Button("Import Game Spell Loadouts")) Plugin.ImportGamePresets(true);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Imports all game presets and saved hotbars");
    }

    private unsafe void NormalHotbarTab() {
        using var tabItem = ImRaii.TabItem("Standard Hotbars");
        if (!tabItem.Success)
            return;

        ImGui.Text("Select hotbars to be saved and restored with your loadouts.");
        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.Indent();

        var columns = (int) ((ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin()).X
                             / (150f * ImGui.GetIO().FontGlobalScale));
        if (ImGui.Checkbox("Save Hotbars", ref Plugin.Configuration.SaveHotbarsStandard))
            Plugin.Configuration.Save();

        ImGuiHelpers.ScaledDummy(10.0f);

        if (Plugin.Configuration.SaveHotbarsStandard) {
            ImGui.Columns(columns, "hotbarColumns");
            for (uint i = 0; i < Plugin.Configuration.HotbarsStandard.Length; i++) {
                var isShared = Plugin.RaptureHotbar->IsHotbarShared(i);
                using (ImRaii.Disabled(isShared)) {
                    if (ImGui.Checkbox($"Hotbar {i + 1}##syncBar_{i}",
                                       ref Plugin.Configuration.HotbarsStandard[i]))
                        Plugin.Configuration.Save();
                }

                if (isShared && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Shared Hotbars will not be saved or restored.");

                if (isShared && Plugin.Configuration.HotbarsStandard[i]) {
                    using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudYellow)) {
                        ImGui.SameLine();
                        ImGuiComponents.HelpMarker("Shared Hotbars will not be saved or restored.");
                    }
                }
                ImGui.NextColumn();
            }
            ImGui.Columns();
        }
    }

    private unsafe void CrossHotbarTab() {
        using var tabItem = ImRaii.TabItem("Cross Hotbars");
        if (!tabItem.Success)
            return;
        ImGui.Text("Select hotbars to be saved and restored with your loadouts.");
        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.Indent();

        if (ImGui.Checkbox("Save Cross Hotbars", ref Plugin.Configuration.SaveHotbarsCross))
            Plugin.Configuration.Save();
        var columns = (int) ((ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin()).X
                             / (150f * ImGui.GetIO().FontGlobalScale));

        ImGuiHelpers.ScaledDummy(10.0f);

        if (Plugin.Configuration.SaveHotbarsCross) {
            ImGui.Columns(columns, "crosshotbarColumns");
            for (uint i = 0; i < Plugin.Configuration.HotbarsCross.Length; i++) {
                var isShared = Plugin.RaptureHotbar->IsHotbarShared(i + 10);
                using (ImRaii.Disabled(isShared)) {
                    if (ImGui.Checkbox($"Cross Hotbar {i + 1}##syncCrossBar_{i}",
                                       ref Plugin.Configuration.HotbarsCross[i]))
                        Plugin.Configuration.Save();
                }

                if (isShared && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Shared Hotbars will not be saved or restored.");

                if (isShared && Plugin.Configuration.HotbarsCross[i]) {
                    using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudYellow)) {
                        ImGui.SameLine();
                        ImGuiComponents.HelpMarker("Shared Hotbars will not be saved or restored.");
                    }
                }
                ImGui.NextColumn();
            }
            ImGui.Columns();
        }

        ImGui.Unindent();
    }
}
