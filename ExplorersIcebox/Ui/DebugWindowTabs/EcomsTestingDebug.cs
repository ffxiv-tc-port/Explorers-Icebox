using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class EcomsTestingDebug
    {
        public static void Draw()
        {
            if (GenericHelpers.TryGetAddonMaster<MJIHud>("MJIHud", out var mjiHud) && mjiHud.IsAddonReady)
            {
                ImGui.Text($"目前等級: {mjiHud.SanctuaryRank}");
                ImGui.Text($"目前島嶼經驗值: {mjiHud.CurrentIslandXP} | {mjiHud.NextIslandLevelXP}");
                ImGui.Text($"居民 Cowrie: {mjiHud.IslandersCowrie}");
                ImGui.Text($"海員 Cowrie: {mjiHud.SeafarersCowrie}");
            }
        }
    }
}
