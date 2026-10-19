using ExplorersIcebox.Scheduler.Tasks;
using ExplorersIcebox.Util;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class MiscInfoDebug
    {

        private static string InputValue = "0"; // The uint value to be edited
        private static ulong Result;

        public static void Draw()
        {
            // Input box
            ImGui.InputText("輸入數字", ref InputValue, 20);

            if (ImGui.Button("送出"))
            {
                // Try to parse the input to a ulong
                if (ulong.TryParse(InputValue, out Result))
                {
                    ImGui.Text($"輸入有效！解析結果: {Result}");
                }
                else
                {
                    ImGui.Text("輸入無效，請輸入有效的數字。");
                }
            }

            ImGui.Text($"目前數值: {InputValue}");

            if (ImGui.Button("更新節點ID"))
            {
                IslandHelper.UpdateShopCallback();
            }

            ImGui.SameLine();
            if (ImGui.Button("更新節點ID任務"))
            {
                Task_UpdateShop.Enqueue();
            }

            if (GenericHelpers.TryGetAddonMaster<MJIHud>("MJIHud", out var hud) && hud.IsAddonReady)
            {
                if (ImGui.Button("開啟無人島物品欄"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "Isleventory", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.Isleventory();
                }

                ImGui.SameLine();
                if (ImGui.Button("開啟工藝紀錄簿"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "SanctuaryCraftingLog", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.SanctuaryCraftingLog();
                }

                ImGui.SameLine();
                if (ImGui.Button("開啟採集紀錄簿"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "SanctuaryGatheringLog", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.SanctuaryGatheringLog();
                }

                ImGui.SameLine();
                if (ImGui.Button("管理隱居小屋"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "ManageHideaway", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.ManageHideaway();
                }

                ImGui.SameLine();
                if (ImGui.Button("材料分配"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "MaterialAllocation", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.MaterialAllocation();
                }

                ImGui.SameLine();
                if (ImGui.Button("管理寵物"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "ManageMinions", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.ManageMinions();
                }

                ImGui.SameLine();
                if (ImGui.Button("管理無人島庭具"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "ManageFurnishing", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.ManageFurnishing();
                }

                ImGui.SameLine();
                if (ImGui.Button("導覽"))
                {
                    if (AddonPressGuard.TryBeginPress("MJIHud", hud, "Guide", AddonPressGuard.RoutineRePressEscapeFrames))
                        hud.Guide();
                }
            }

            if (ImGui.BeginTable("Callback Values", 2, ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("項目");
                ImGui.TableSetupColumn("Callback");

                ImGui.TableHeadersRow();

                foreach (var item in ItemData.IslandItems)
                {
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(item.Value.ItemName);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"{item.Key}");
                        ImGui.EndTooltip();
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text($"{item.Value.SellSlot}");
                }

                ImGui.EndTable();
            }
        }
    }
}
