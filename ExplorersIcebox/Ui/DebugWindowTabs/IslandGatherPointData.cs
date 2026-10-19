using ExplorersIcebox.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class IslandGatherPointData
    {
        private static readonly Dictionary<string, HashSet<ulong>> GatherNodeIds = new()
        {
            ["Agave Plant"] = new(),
            ["Bluish Rock"] = new(),
            ["Composite Rock"] = new(),
            ["Coral Formation"] = new(),
            ["Cotton Plant"] = new(),
            ["Crystal-banded Rock"] = new(),
            ["Glowing Fungus"] = new(),
            ["Island Apple Tree"] = new(),
            ["Island Crystal Cluster"] = new(),
            ["Large Shell"] = new(),
            ["Lightly Gnawed Pumpkin"] = new(),
            ["Mahogany Tree"] = new(),
            ["Mound of Dirt"] = new(),
            ["Multicolored Isleblooms"] = new(),
            ["Palm Tree"] = new(),
            ["Partially Consumed Cabbage"] = new(),
            ["Quartz Formation"] = new(),
            ["Rough Black Rock"] = new(),
            ["Seaweed Tangle"] = new(),
            ["Smooth White Rock"] = new(),
            ["Speckled Rock"] = new(),
            ["Stalagmite"] = new(),
            ["Submerged Sand"] = new(),
            ["Sugarcane"] = new(),
            ["Tualong Tree"] = new(),
            ["Wild Parsnip"] = new(),
            ["Wild Popoto"] = new(),
            ["Yellowish Rock"] = new(),
        };

        private static float Distance = 50f;
        private static int NodeCount = 0;

        public static void GatherPointDataDraw()
        {
            var objects = Svc.Objects.Where(e => e.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.CardStand);
            ImGui.DragFloat("與目標距離", ref Distance);

            foreach (var obj in objects)
            {
                if (PlayerHelper.GetDistanceToPlayer(obj.Position) > Distance)
                {
                    continue;
                }

                if (GatherNodeIds.TryGetValue(obj.Name.ToString(), out var idSet))
                {
                    idSet.Add(obj.GameObjectId);
                }
            }

            ImGui.Text($"已找到採集點總數: {NodeCount}");
            if (ImGui.Button("重置"))
            {
                NodeCount = GatherNodeIds.Sum(pair => pair.Value.Count);
            }

            if (ImGuiEx.TreeNode("採集點清單"))
            {
                if (ImGui.BeginTable("###ListofNodes", 4, ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("項目");
                    ImGui.TableSetupColumn("ID");
                    ImGui.TableSetupColumn("距離");

                    ImGui.TableHeadersRow();

                    foreach (var obj in objects)
                    {
                        if (PlayerHelper.GetDistanceToPlayer(obj.Position) > Distance)
                        {
                            continue;
                        }

                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text($"{obj.Name.ToString()}");

                        ImGui.TableNextColumn();
                        if (ImGui.Selectable($"{obj.DataId}###{obj.Name.ToString()}"))
                        {
                            ImGui.SetClipboardText($"{obj.DataId}");
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text($"{PlayerHelper.GetDistanceToPlayer(obj.Position)}");

                        ImGui.TableNextColumn();
                    }

                    ImGui.EndTable();
                }
            }

            if (ImGui.Button("清除清單"))
            {
                foreach (var entry in GatherNodeIds)
                {
                    entry.Value.Clear();
                }
            }

            foreach (var entry in GatherNodeIds)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"項目: {entry.Key} | 找到數量: {entry.Value.Count}");
                ImGui.SameLine();
                if (ImGui.Button($"複製清單 ###List_{entry.Key}"))
                {
                    string nodeIds = string.Empty;
                    foreach (var id in entry.Value)
                    {
                        nodeIds += id + ", ";
                    }
                    ImGui.SetClipboardText(nodeIds);
                }
            }
        }
    }
}
