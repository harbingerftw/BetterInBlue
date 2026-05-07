using System;
using Dalamud.Utility;
using Lumina.Text.ReadOnly;

namespace BetterInBlue;

public enum NotebookCategory {
    [Category(RowId = 13100)] Type,
    [Category(RowId = 13105)] Aspect,
    [Category(RowId = 13113)] Rank,
    [Category(RowId = 13119)] Target,

    [Category(RowId = 13128, DisplayPriority = 1)]
    Status,
}

[Flags]
public enum NotebookFilterFlags {
    //Type
    [NotebookFlag(Category = NotebookCategory.Type, RowId = 13101)]
    Magical = 1,

    [NotebookFlag(Category = NotebookCategory.Type, RowId = 13102)]
    Slashing = 1 << 1,

    [NotebookFlag(Category = NotebookCategory.Type, RowId = 13103)]
    Piercing = 1 << 2,

    [NotebookFlag(Category = NotebookCategory.Type, RowId = 13104)]
    Blunt = 1 << 3,

    //Aspect
    [NotebookFlag(Category = NotebookCategory.Aspect, RowId = 13106, Color = 12)]
    Fire = 1 << 4,

    [NotebookFlag(Category = NotebookCategory.Aspect, RowId = 13107, Color = 34)]
    Ice = 1 << 5,

    [NotebookFlag(Category = NotebookCategory.Aspect, RowId = 13108, Color = 40)]
    Wind = 1 << 6,

    [NotebookFlag(Category = NotebookCategory.Aspect, RowId = 13109, Color = 31)]
    Earth = 1 << 7,

    [NotebookFlag(Category = NotebookCategory.Aspect, RowId = 13110, Color = 48)]
    Lightning = 1 << 8,

    [NotebookFlag(Category = NotebookCategory.Aspect, RowId = 13111, Color = 57)]
    Water = 1 << 9,

    [NotebookFlag(Category = NotebookCategory.Aspect, RowId = 13112, Color = 4)]
    None = 1 << 10,

    //Rank
    [NotebookFlag(Category = NotebookCategory.Rank, RowId = 13114, Name = "1", DisplayPriority = 1)]
    Star1 = 1 << 11,

    [NotebookFlag(Category = NotebookCategory.Rank, RowId = 13115, Name = "2", DisplayPriority = 1)]
    Star2 = 1 << 12,

    [NotebookFlag(Category = NotebookCategory.Rank, RowId = 13116, Name = "3", DisplayPriority = 1)]
    Star3 = 1 << 13,

    [NotebookFlag(Category = NotebookCategory.Rank, RowId = 13117, Name = "4", DisplayPriority = 1)]
    Star4 = 1 << 14,

    [NotebookFlag(Category = NotebookCategory.Rank, RowId = 13118, Name = "5", DisplayPriority = 1)]
    Star5 = 1 << 15,

    //Target
    [NotebookFlag(Category = NotebookCategory.Target, RowId = 13120, Name = "Enemy")]
    TargetEnemy = 1 << 16,

    [NotebookFlag(Category = NotebookCategory.Target, RowId = 13121, Name = "Ally")]
    TargetAlly = 1 << 17,

    //Status Inflicted
    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12472)]
    Slow = 1 << 18,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12473, Name = "Petrification", DisplayPriority = 1)]
    Petrification = 1 << 19,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12473, Name = "Freeze", DisplayPriority = 1)]
    Freeze = 1 << 19,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12474)]
    Paralysis = 1 << 20,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12475)]
    Interruption = 1 << 21,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12476)]
    Blind = 1 << 22,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12477)]
    Stun = 1 << 23,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12478)]
    Sleep = 1 << 24,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12479)]
    Bind = 1 << 25,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12480)]
    Heavy = 1 << 26,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12481, Name = "Flat", DisplayPriority = 1)]
    FlatDamage = 1 << 27,

    [NotebookFlag(Category = NotebookCategory.Status, RowId = 12481, Name = "Death", DisplayPriority = 1)]
    Death = 1 << 27
}

[AttributeUsage(AttributeTargets.Field)]
public class CategoryAttribute : Attribute {
    public required uint RowId { get; set; }
    public uint DisplayPriority { get; set; }
    public string? Name { get; set; }
}

[AttributeUsage(AttributeTargets.Field)]
public class NotebookFlagAttribute : Attribute {
    public required NotebookCategory Category { get; set; }
    public required uint RowId { get; set; }
    public string? Name { get; set; }
    public uint Color { get; set; }

    public uint DisplayPriority { get; set; }

    public override string ToString() {
        return $"{Category} ({RowId})";
    }
}

public static class NotebookFilterFlagsExtension {
    public static ReadOnlySeString GetAddonLogId(this NotebookCategory category) {
        var a = category.GetAttribute<CategoryAttribute>();
        return Plugin.GetAddonLogId(a?.RowId ?? 0);
    }

    public static ReadOnlySeString GetDisplay(this NotebookCategory category) {
        var a = category.GetAttribute<CategoryAttribute>();
        return a?.DisplayPriority switch {
            0 => category.GetAddonLogId(),
            _ => category.ToString()
        };
    }

    public static string GetName(this NotebookFilterFlags flag) {
        var a = flag.GetAttribute<NotebookFlagAttribute>();
        return a?.Name ?? flag.ToString();
    }

    public static uint GetColor(this NotebookFilterFlags flag) {
        var a = flag.GetAttribute<NotebookFlagAttribute>();
        return a?.Color ?? 0;
    }


    public static ReadOnlySeString GetAddonLogId(this NotebookFilterFlags flag) {
        var a = flag.GetAttribute<NotebookFlagAttribute>();
        return Plugin.GetAddonLogId(a?.RowId ?? 0);
    }

    public static ReadOnlySeString GetDisplay(this NotebookFilterFlags flag) {
        var a = flag.GetAttribute<NotebookFlagAttribute>();
        return a?.DisplayPriority switch {
            0 => flag.GetAddonLogId(),
            1 => flag.GetName(),
            _ => flag.ToString()
        };
    }
}
