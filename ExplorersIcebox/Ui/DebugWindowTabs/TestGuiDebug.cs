using ExplorersIcebox.Scheduler.Tasks;
using ExplorersIcebox.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class TestGuiDebug
    {
        private static int Position = 0;
        private static string PopupText = string.Empty;

        public static void Draw()
        {
            if (ImGui.Button("返回無人島營地"))
            {
                Task_ReturnToBase.Enqueue();
            }

            if (ImGui.Button("開啟玩家搜尋"))
            {
                Utils.OpenPlayerSearch(15);
            }
            if (ImGui.Button("開啟好友清單"))
            {
                Utils.OpenPlayerSearch(13);
            }
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("###Position", ref Position);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            ImGui.InputText("###TextTest", ref PopupText, 100);

            if (ImGui.Button("測試文字"))
            {
                Utils.ShowText(Position, PopupText);
            }
        }
    }
}
