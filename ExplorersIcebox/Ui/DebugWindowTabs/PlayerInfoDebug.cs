using Dalamud.Interface.Colors;
using ExplorersIcebox.Scheduler.Tasks;
using ExplorersIcebox.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class PlayerInfoDebug
    {
        private static string AddonName = "default";

        public static void Draw()
        {
            ImGui.Text($"一般資訊");
            ImGui.Text($"地區ID: " + Svc.ClientState.TerritoryType);
            ImGui.Text($"目標: " + Svc.Targets.Target);
            ImGui.InputText("##Addon Visible", ref AddonName, 100);
            ImGui.SameLine();
            ImGui.Text($"Addon 可見: ");
            ImGui.SameLine();
            if (AddonHelper.IsAddonActive(AddonName))
            {
                FontAwesome.Print(ImGuiColors.HealerGreen, FontAwesome.Check);
            }
            else if (!AddonHelper.IsAddonActive(AddonName))
            {
                FontAwesome.Print(ImGuiColors.DalamudRed, FontAwesome.Cross);
            }
            ImGui.Text($"Navmesh 資訊");
            var player = Svc.Objects.LocalPlayer;
            if (player != null)
            {
                ImGui.Text($"玩家座標: " + player.Position);
            }
            ImGui.Text($"Navmesh 建置進度: " + P.navmesh.BuildProgress());//working ipc
            ImGui.Text($"目前任務剩餘時間: {P.taskManager.RemainingTimeMS}");
            ImGui.Text($"目前任務: {P.taskManager.CurrentTask}");
            ImGui.Text($"目前任務數量: {P.taskManager.NumQueuedTasks}");
            if (ImGui.Button("切換至無人島模式"))
            {
                Task_GatherMode.Enqueue();
            }

        }
    }
}
