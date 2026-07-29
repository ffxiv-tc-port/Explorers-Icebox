using ExplorersIcebox.Ui.DebugWindowTabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Ui;

internal class DebugWindow : Window
{
    public DebugWindow() : base ($"Explorer's IceBox Debug ###Explorer's Icebox Debug")
    {
        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 100),
        };
        P.windowSystem.AddWindow(this);
    }

    public void Dispose()
    {
        P.windowSystem.RemoveWindow(this);
    }

    // variables that hold the "ref"s for ImGui
    private string addonName = "default";
    private string inputValue = "0"; // The uint value to be edited
    private static ulong Result;

    private string[] debugTypes = ["玩家資訊", "Navmesh 除錯", "其他資訊", "路線出售", "目標資訊", "ImGui 測試", "無人島採集點搜尋", "無人島物品資訊", "路線編輯器 V4", "簡易路線建立器", "Picto 測試"];
    int selectedDebugIndex = 0; // This should be stored somewhere persistent

    public override void Draw()
    {
        float spacing = 10f;
        float leftPanelWidth = 200f;
        float rightPanelWidth = ImGui.GetContentRegionAvail().X - leftPanelWidth - spacing;
        float childHeight = ImGui.GetContentRegionAvail().Y;

        if (ImGui.BeginChild("DebugSelector", new System.Numerics.Vector2(leftPanelWidth, childHeight), true))
        {
            for (int i = 0; i < debugTypes.Length; i++)
            {
                bool isSelected = (selectedDebugIndex == i);
                string label = isSelected ? $"→ {debugTypes[i]}" : $"   {debugTypes[i]}"; // Add space for alignment

                if (ImGui.Selectable(label, isSelected))
                {
                    selectedDebugIndex = i;
                }
            }
            ImGui.EndChild();
        }

        ImGui.SameLine(0, spacing);

        if (ImGui.BeginChild("DebugContent", new System.Numerics.Vector2(rightPanelWidth, childHeight), true))
        {
            switch (selectedDebugIndex)
            {
                case 0: PlayerInfoDebug.Draw(); break;
                case 1: ImGui.Text("Navmesh 資訊尚待修正"); break;
                case 2: MiscInfoDebug.Draw(); break;
                case 3: RouteSellDebug.Draw(); break;
                case 4: TargetInfoDebug.Draw(); break;
                case 5: TestGuiDebug.Draw(); EcomsTestingDebug.Draw(); break;
                case 6: IslandGatherPointData.GatherPointDataDraw(); break;
                case 7: IslandItemInfoDebug.Draw(); break;
                case 8: RouteEditorV4Debug.Draw(); break;
                case 9: BaseRouteEditor.Draw(); break;
                case 10: PictoTestDebug.Draw(); break;
                default: ImGui.Text("未知的除錯檢視"); break;
            }

            ImGui.EndChild();
        }
    }
}
