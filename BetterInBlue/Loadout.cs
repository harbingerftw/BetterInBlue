using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace BetterInBlue;

[Serializable]
public struct HotbarSlot(uint id, HotbarSlotType type) {
    public uint CommandId = id;
    public HotbarSlotType CommandType = type;
}

[Serializable]
public struct HotbarConfig(uint id, HotbarSlot[] slots) {
    public uint Id = id;
    public HotbarSlot[] Slots = slots;
    [JsonIgnore] public int MaxSlots => this.Id < 10 ? 12 : 16;
    [JsonIgnore] public bool IsCrossHotbar => this.Id >= 10;
}

[Serializable]
public class Loadout(string name = "Unnamed Loadout") {
    public string Name { get; set; } = name;
    public uint[] Actions { get; set; } = new uint[24];

    public List<HotbarConfig> LoadoutHotbars { get; set; } = [];

    public static Loadout? FromPreset(string preset) {
        try {
            var bytes = Convert.FromBase64String(preset);
            var str = Encoding.UTF8.GetString(bytes);
            var obj = JsonConvert.DeserializeObject<Loadout>(str);
            if (obj != null) obj.LoadoutHotbars = [];
            return obj;
        } catch (Exception) {
            return null;
        }
    }

    public string ToPreset() {
        var str = JsonConvert.SerializeObject(this,
                                              Formatting.None,
                                              new JsonSerializerSettings {
                                                  ContractResolver = new NoHotbarsContractResolver()
                                              });

        var bytes = Encoding.UTF8.GetBytes(str);
        return Convert.ToBase64String(bytes);
    }

    public int ActionCount(uint id) {
        return this.Actions.Count(x => x == id);
    }

    public unsafe bool ActionUnlocked(uint id) {
        var normalId = Plugin.AozToNormal(id);
        var link = Plugin.Action.GetRow(normalId).UnlockLink;
        return UIState.Instance()->IsUnlockLinkUnlocked(link.RowId);
    }

    public bool CanApply() {
        // Must be BLU to apply (id = 36)
        if (Services.ClientState.LocalPlayer?.ClassJob.RowId != 36) return false;

        // Can't apply in combat
        if (Services.Condition[ConditionFlag.InCombat]) return false;

        foreach (var action in this.Actions) {
            // No out of bounds indexing
            if (action > Plugin.AozAction.Count) return false;

            if (action != 0) {
                // Can't have two actions in the same loadout
                if (this.ActionCount(action) > 1) return false;

                // Can't apply an action you don't have
                if (!this.ActionUnlocked(action)) return false;
            }
        }

        // aight we good
        return true;
    }

    public unsafe bool Apply() {
        var actionManager = ActionManager.Instance();

        var arr = new uint[24];
        for (var i = 0; i < 24; i++)
            arr[i] = Plugin.AozToNormal(this.Actions[i]);

        fixed (uint* ptr = arr) {
            var ret = actionManager->SetBlueMageActions(ptr);
            if (ret == false) return false;
        }

        this.ApplyToHotbars();

        return true;
    }

    public void SaveHotbars() {
        if (Plugin.Configuration.SaveHotbarsStandard)
            this.SaveHotbarSet(0, 10, 12, ref Plugin.Configuration.HotbarsStandard);

        if (Plugin.Configuration.SaveHotbarsCross)
            this.SaveHotbarSet(10, 17, 16, ref Plugin.Configuration.HotbarsCross);
    }

    private unsafe void SaveHotbarSet(uint start, uint end, uint maxSlots, ref bool[] enabled) {
        for (var hotbarNum = start; hotbarNum < end; hotbarNum++) {
            if (Plugin.RaptureHotbar->IsHotbarShared(hotbarNum)) continue;
            var index = hotbarNum - start;
            if (!enabled[index]) continue;
            var hotbarToSave = new HotbarConfig(hotbarNum, new HotbarSlot[maxSlots]);

            Services.Log.Verbose($"Saving hotbar {hotbarNum} ({index})");

            for (uint slotNum = 0; slotNum < maxSlots; slotNum++) {
                var slot = Plugin.RaptureHotbar->GetSlotById(hotbarNum, slotNum);
                hotbarToSave.Slots[slotNum] = new HotbarSlot(slot->CommandId, slot->CommandType);
            }
            this.LoadoutHotbars.Add(hotbarToSave);
        }
    }

    public unsafe void ApplyToHotbars() {
        foreach (var bar in this.LoadoutHotbars) {
            if (Plugin.RaptureHotbar->IsHotbarShared(bar.Id)) continue;

            if (bar.IsCrossHotbar) {
                if (!(Plugin.Configuration.HotbarsCross[bar.Id - 10] && Plugin.Configuration.SaveHotbarsCross))
                    continue;
            } else {
                if (!(Plugin.Configuration.HotbarsStandard[bar.Id] && Plugin.Configuration.SaveHotbarsStandard))
                    continue;
            }

            Services.Log.Verbose($"Restoring hotbar {bar.Id}");

            for (uint slotId = 0; slotId < bar.MaxSlots; slotId++) {
                Plugin.RaptureHotbar->SetAndSaveSlot(bar.Id,
                                                     slotId,
                                                     bar.Slots[slotId].CommandType,
                                                     bar.Slots[slotId].CommandId,
                                                     false,
                                                     false);
            }
        }
    }
}

public class NoHotbarsContractResolver : DefaultContractResolver {
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);

        if (property.PropertyName == "LoadoutHotbars") property.ShouldSerialize = _ => false;

        return property;
    }
}
