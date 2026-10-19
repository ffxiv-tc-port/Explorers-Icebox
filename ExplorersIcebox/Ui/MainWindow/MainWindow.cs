using Dalamud.Interface.Utility.Raii;
using ECommons.ExcelServices;
using ExplorersIcebox.Scheduler;
using ExplorersIcebox.Scheduler.Tasks;
using ExplorersIcebox.Util;
using System.Collections.Generic;

namespace ExplorersIcebox.Ui.MainWindow;

internal class MainWindow : Window
{
    public MainWindow() :
        base($"Explorer's Icebox {P.GetType().Assembly.GetName().Version} ###Explorer'sIceboxMainWindow")
    {
        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new()
        {
            MinimumSize = new Vector2(300, 300),
            MaximumSize = new Vector2(2000, 2000)
        };
        P.windowSystem.AddWindow(this);
        AllowPinning = false;
    }

    public void Dispose() { }

    private static int ExtractNumber(string input)
    {
        // Use regex to find digits in the string
        var match = System.Text.RegularExpressions.Regex.Match(input, @"\d+");
        return match.Success ? int.Parse(match.Value) : int.MinValue; // or 0 if you prefer
    }

    public int selectedRoute = C.routeSelected;

    private readonly List<string> modeSelect = ["地面經驗值", "飛行經驗值", "素材採集"];
    private int selectedModeIndex = C.ModeSelected;
    private List<string> routeNames => EmbedRoutes.Routes.Keys.OrderBy(name => ExtractNumber(name)).ToList();

    public override void Draw()
    {
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("選擇模式", modeSelect[selectedModeIndex]))
        {
            for (int i = 0; i < modeSelect.Count; i++)
            {
                bool isSelected = (i == selectedModeIndex);
                if (ImGui.Selectable(modeSelect[i], isSelected))
                {
                    C.ModeSelected = i;
                    selectedModeIndex = i;
                    C.Save();
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        bool DisableSelection = selectedModeIndex < modeSelect.Count - 1;

        int selectedRouteIndex = C.routeSelected;

        if (selectedModeIndex == 2)
        {
            using (ImRaii.Disabled(DisableSelection))
            {
                ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("選擇路線", RouteNameLoc.Translate(routeNames[selectedRouteIndex])))
                {
                    for (int i = 0; i < routeNames.Count; i++)
                    {
                        bool isSelected = (i == selectedRouteIndex);
                        if (ImGui.Selectable($"{RouteNameLoc.Translate(routeNames[i])}###route_{i}", isSelected))
                        {
                            selectedRouteIndex = i;
                            C.routeSelected = i;
                            C.Save();
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }
            }
        }

        if (selectedModeIndex == 0)
            selectedRouteIndex = 7;
        else if (selectedModeIndex == 1)
            selectedRouteIndex = 18;

        var routeSelected = EmbedRoutes.Routes.Where(x => x.Key == routeNames[selectedRouteIndex]).FirstOrDefault();

        if (EmbedRoutes.Routes.ContainsKey(routeSelected.Key))
        {
            Dictionary<string, IslandHelper.ItemGathered> routeItems = new();
            Dictionary<string, HashSet<ItemData.GatheringNode>> itemNodeMap = new();

            IslandHelper.CurrentRoute = routeSelected;

            foreach (var wp in routeSelected.Value.RouteWaypoints)
            {
                if (wp.TargetId != 0) // General check to make sure we're not looking for a null item
                {
                    var Node = ItemData.IslandNodeInfo.Where(x => x.Nodes.Contains(wp.TargetId)).FirstOrDefault();
                    if (Node != null)
                    {
                        foreach (var item in Node.ItemIds)
                        {
                            string itemName = ItemData.IslandItems[item].ItemName;
                            if (!routeItems.ContainsKey(itemName))
                            {
                                routeItems[itemName] = new IslandHelper.ItemGathered()
                                {
                                    Amount = 1,
                                    ItemId = item,
                                    GatherNodes = { Node.GatherName },
                                    IgnoreNode = false
                                };
                            }
                            else
                            {
                                routeItems[itemName].Amount += 1;
                                routeItems[itemName].GatherNodes.Add(Node.GatherName);
                            }

                            if (!itemNodeMap.ContainsKey(itemName))
                                itemNodeMap[itemName] = new();

                            itemNodeMap[itemName].Add(Node);
                        }
                    }
                }
            }

            foreach (var kvp in routeItems)
            {
                var itemName = kvp.Key;
                var gathered = kvp.Value;

                if (!itemNodeMap.TryGetValue(itemName, out var nodes)) continue;

                if (nodes.Count <= 1)
                {
                    gathered.IgnoreNode = false;
                }
                else
                {
                    gathered.IgnoreNode = nodes.Count > 1 && nodes.All(n => n.ItemIds.Count > 1);
                }
            }

            bool SkipSell = C.SkipSell;
        if (ImGui.Checkbox("略過出售物品", ref SkipSell))
            {
                C.SkipSell = SkipSell;
                C.Save();
            }

            bool RunMaxLoops = C.RunMaxLoops;
        if (ImGui.Checkbox("執行最多循環次數", ref RunMaxLoops))
            {
                C.RunMaxLoops = RunMaxLoops;
                C.Save();
            }
        ImGuiEx.HelpMarker("依目前要保留的數量執行可行的最多循環次數。\n" +
                           "適合只想盡量採集、不指定特定物品數量時使用。");
            if (RunMaxLoops)
                IslandHelper.GoalLoopAmount = IslandHelper.MaxRouteLoops;

            bool runMultiple = C.RunMultiple;
        if (ImGui.Checkbox("重複循環", ref runMultiple))
            {
                C.RunMultiple = runMultiple;
                C.Save();
            }
            if (runMultiple)
            {
                ImGui.SameLine();
                int RunAmount = C.RunAmount;
                ImGui.SetNextItemWidth(100);
                if (ImGui.InputInt("###RunMultipleAmount", ref RunAmount))
                {
                    C.RunAmount = RunAmount;
                    C.Save();
                }
                ImGui.SameLine();
        ImGui.Text("執行次數");
            }

            int MinItemKeep = C.MinimumItemKeep;

            using (ImRaii.Disabled(SkipSell))
            {
                ImGui.SetNextItemWidth(100);
        if (ImGui.SliderInt("保留此數量的物品", ref MinItemKeep, 0, 999))
                {
                    C.MinimumItemKeep = MinItemKeep;
                    C.Save();
                }
            }
            bool DisableButtons = IslandHelper.GoalLoopAmount > IslandHelper.MaxRouteLoops || IslandHelper.MaxRouteLoops == 0 || IslandHelper.GoalLoopAmount == 0;

            using (ImRaii.Disabled(DisableButtons))
            {
        if (ImGui.Button("開始"))
                {
                    SchedulerMain.EnablePlugin();
                }

                ImGui.SameLine();
        if (ImGui.Button("停止"))
                {
                    SchedulerMain.DisablePlugin();
                }
            }

            if (DisableButtons)
            {
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
            ImGuiEx.IconWithTooltip(FontAwesomeIcon.ExclamationTriangle, "目前設定無法安全執行。\n" +
                                                                                  "請調整要保留或要採集的物品數量。");
                ImGui.Spacing();
            }

            if (ImGui.BeginTable("Loop Info", 2, ImGuiTableFlags.SizingFixedFit))
            {
        ImGui.TableSetupColumn("循環次數");
        ImGui.TableSetupColumn("最多可執行次數");

                ImGui.TableHeadersRow();

                // Table Body
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text($"{IslandHelper.GoalLoopAmount}");

                ImGui.TableNextColumn();
                ImGui.Text($"{IslandHelper.MaxRouteLoops}");

                ImGui.EndTable();
            }

            ImGui.Separator();
            // Route Information

            IslandHelper.UpdateCounters(routeItems);

        if (ImGui.BeginTable("已採集物品", 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
            {
            ImGui.TableSetupColumn("物品");
            ImGui.TableSetupColumn("每輪數量");
            ImGui.TableSetupColumn("忽略");
            ImGui.TableSetupColumn("採集數量");
            ImGui.TableSetupColumn("目前數量");

                ImGui.TableHeadersRow();

                foreach (var item in routeItems)
                {
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text($"{ExcelItemHelper.GetName((uint)item.Value.ItemId)}");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"{item.Value.ItemId}");
                        ImGui.EndTooltip();
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text($"{item.Value.Amount}");

                    ImGui.TableNextColumn();
                    Utils.FancyCheckmark(item.Value.IgnoreNode);

                    ImGui.TableNextColumn();
                    var GatherAmount = C.ItemGatherAmount[item.Key];
                    ImGui.SetNextItemWidth(200);
                    using (ImRaii.Disabled(RunMaxLoops))
                    {
                        if (ImGui.SliderInt($"###GatherAmount_{item.Key}", ref GatherAmount, 0, 999))
                        {
                            C.ItemGatherAmount[item.Key] = GatherAmount;
                            C.Save();
                        }
                    }

                    ImGui.TableNextColumn();
                    if (PlayerHelper.GetItemCount(item.Value.ItemId, out var count))
                        ImGui.Text($"{count}");

                }

                ImGui.EndTable();
            }
        }

#if DEBUG
        foreach (var item in IslandHelper.SellItems)
        {
            ImGui.Text($"{ItemData.IslandItems[item.Key].ItemName} | {item.Value}");
        }
#endif
    }
}
