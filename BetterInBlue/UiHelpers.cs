using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace BetterInBlue;

public static class UiHelpers {
    /// Renders a disabled icon button with a tooltip based on the button's state.
    /// <param name="icon">The icon to be displayed on the button.</param>
    /// <param name="disabled">A boolean indicating whether the button is disabled.</param>
    /// <param name="enabledText">The tooltip text to display when the button is enabled. Optional.</param>
    /// <param name="disabledText">The tooltip text to display when the button is disabled. Optional.</param>
    /// <returns>True if the button is clicked, and it is not disabled; otherwise, false.</returns>
    public static bool DisabledIconButtonWithTooltip(
        FontAwesomeIcon icon, bool disabled,
        string enabledText = "", string disabledText = ""
    ) {
        // https://github.com/Ottermandias/OtterGui/blob/78528f93ac253db0061d9a8244cfa0cee5c2f873/Util.cs#L300
        using var dis = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f, disabled);
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        var ret = ImGuiComponents.IconButton(icon);
        font.Pop();
        dis.Pop();

        var str = disabled ? disabledText : enabledText;
        if (!string.IsNullOrEmpty(str) && ImGui.IsItemHovered()) {
            using var tt = ImRaii.Tooltip();
            ImGui.TextUnformatted(str);
        }
        return ret && !disabled;
    }

    public static void ShowNotification(
        string content,
        string title = "",
        NotificationType type = NotificationType.Success
    ) {
        var notification = new Notification {
            Content = content,
            Title = title,
            Type = type
        };
        Services.NotificationManager.AddNotification(notification);
    }
}
