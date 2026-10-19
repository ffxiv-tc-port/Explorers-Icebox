using ExplorersIcebox.Scheduler;
using ExplorersIcebox.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Scheduler.Tasks
{
    internal static class Task_GatherLoop
    {
        public static void Enqueue()
        {
            var totalLoops = IslandHelper.GoalLoopAmount;
            if (C.RunMaxLoops)
                totalLoops = IslandHelper.MaxRouteLoops;
            var remainingLoops = Math.Max(0, totalLoops - IslandHelper.RouteLapsDone);
            if (IslandHelper.RouteLapsDone > 0)
                Svc.Log.Information($"[Icebox] 路線中斷後重排：這一輪已跑完 {IslandHelper.RouteLapsDone}/{totalLoops} 圈，只排剩下的 {remainingLoops} 圈。");

            if (remainingLoops > 0)
            {
                Task_GatherMode.Enqueue();
                foreach (var wpList in IslandHelper.CurrentRoute.Value.BaseToLocation)
                {
                    Task_BaseToGather.Enqueue(wpList.Waypoints, wpList.Mount, wpList.Fly);
                }
            }

            for (var i = 0; i < remainingLoops; i++)
            {
                foreach (var entry in IslandHelper.CurrentRoute.Value.RouteWaypoints)
                {
                    Task_IslandInteract.Enqueue(entry.Waypoints, entry.TargetId, entry.Mount, entry.Fly);
                }
                P.taskManager.Enqueue(() => { IslandHelper.RouteLapsDone++; }, "Route lap done");
            }
            P.taskManager.Enqueue(() => CheckLoopCount(), "Checking loop count");
        }

        internal static bool? LoopCountUpdate(int currentLoops)
        {
            Svc.Log.Debug($"Maximum loop count: {IslandHelper.GoalLoopAmount}");
            Svc.Log.Debug($"Minimum Possible Loops: {IslandHelper.MaxRouteLoops}");
            Svc.Log.Debug($"Current loop count: {currentLoops}");
            var totalLoops = Math.Min(IslandHelper.GoalLoopAmount, IslandHelper.MaxRouteLoops) - currentLoops;
            Svc.Log.Debug($"Total loops expected: {totalLoops}");

            return true;
        }

        internal static bool? CheckLoopCount()
        {
            Svc.Log.Debug($"Current loop count: {IslandHelper.LoopCounter}");
            Svc.Log.Debug($"Max total loops: {IslandHelper.GoalLoopAmount}");
            int RepeatAmount = C.RunAmount;
            IslandHelper.LoopCounter += 1;
            IslandHelper.RouteLapsDone = 0;
            if (C.RunMultiple && IslandHelper.LoopCounter < RepeatAmount)
            {
                Svc.Log.Debug($"Run multiple loops were enabled. \n" +
                              $"Current Loop: {IslandHelper.LoopCounter} \n" +
                              $"Repeat Amount: {RepeatAmount}");
                SchedulerMain.State = Enums.IceBoxState.Start;
            }
            else
            {
                SchedulerMain.State = Enums.IceBoxState.EndProcess;
            }

            return true;
        }
    }
}
