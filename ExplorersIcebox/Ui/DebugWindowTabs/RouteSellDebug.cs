using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class RouteSellDebug
    {
        public static void Draw()
        {
            ImGui.Text("這裡本來應該要有路線出售除錯畫面……如果我有做的話");
            if (IPC.NavmeshIPC.Installed)
            {
                ImGui.Text("Navmesh 已安裝，好耶！");
            }
            else
            {
                ImGui.Text("Navmesh 未安裝，噓～");
            }
        }
    }
}
