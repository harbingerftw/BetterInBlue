using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace BetterInBlue;

[Serializable]
public class Configuration : IPluginConfiguration {
    public bool[] HotbarsCross = new bool[8];
    public bool[] HotbarsStandard = new bool[10];
    public List<Loadout> Loadouts = [];
    public bool SaveHotbarsCross = false;
    public bool SaveHotbarsStandard = true;
    public int Version { get; set; } = 2;

    public void Save() {
        Services.PluginInterface.SavePluginConfig(this);
    }
}
