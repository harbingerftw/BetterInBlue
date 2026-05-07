using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace BetterInBlue;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 2;
    
    public bool[] HotbarsCross = new bool[8];
    public bool[] HotbarsStandard = new bool[10];
    public List<Loadout> Loadouts = [];
    public bool SaveHotbarsCross = false;
    public bool SaveHotbarsStandard = true;
    public bool OverwriteOnlyBluActions = false;
    public bool OverwriteActionsWithBlank = false;
    
    public bool DoubleClickApply = true;
    public bool SearchOnEnter = false;

    public void Save() {
        Services.PluginInterface.SavePluginConfig(this);
    }
}
