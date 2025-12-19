using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace BetterInBlue.Windows;

public class ConfigWindow : Window, IDisposable {
    private Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Better in Blue Config") {
        this.plugin = plugin;

        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(350f, 400f),
            MaximumSize = new Vector2(700f, 700f)
        };
        this.Size = new Vector2(500, 400);
    }

    public void Dispose() { }

    public override unsafe void Draw() {
        //https://github.com/Caraxi/SimpleTweaksPlugin/blob/f71e8beaf1d81efcbee230e546dbe75d8f098522/Tweaks/SyncCrafterBars.cs#L29
        ImGui.Text("Select hotbars to be saved and restored with your loadouts.");
        ImGui.Dummy(new Vector2(0, 10));

        ImGui.Indent();

        var columns = (int) ((ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin()).X
                             / (150f * ImGui.GetIO().FontGlobalScale));
        if (ImGui.Checkbox("Save Hotbars", ref Plugin.Configuration.SaveHotbarsStandard))
            Plugin.Configuration.Save();
        ImGui.Dummy(new Vector2(0, 10));
        if (Plugin.Configuration.SaveHotbarsStandard) {
            ImGui.Columns(columns, "hotbarColumns", true);
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
            ImGui.Columns(1);
            ImGui.NewLine();
            ImGui.Separator();
        }

        if (ImGui.Checkbox("Save Cross Hotbars", ref Plugin.Configuration.SaveHotbarsCross))
            Plugin.Configuration.Save();
        ImGui.Dummy(new Vector2(0, 10));
        if (Plugin.Configuration.SaveHotbarsCross) {
            ImGui.Columns(columns, "crosshotbarColumns", true);
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
            ImGui.Columns(1);
        }
        ImGui.Unindent();

        ImGui.Dummy(new Vector2(0, 10));

        if (ImGui.Button("Import Game Presets")) Plugin.ImportGamePresets(true);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Imports all game presets and saved hotbars");

        if (ImGui.Checkbox("Double Click Apply loadouts##db_apply", ref Plugin.Configuration.DoubleClickApply))
            Plugin.Configuration.Save();
    }
}
