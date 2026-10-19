using ECommons.Throttlers;
using ExplorersIcebox.Enums;
using ExplorersIcebox.IPC;
using ExplorersIcebox.Scheduler.Tasks;
using ExplorersIcebox.Util;
using static ExplorersIcebox.Enums.IceBoxState;

namespace ExplorersIcebox.Scheduler
{
    internal static unsafe class SchedulerMain
    {
        internal static bool EnablePlugin()
        {
            IslandHelper.LoopCounter = 0;
            IslandHelper.RouteLapsDone = 0;
            State = Start;
            return true;
        }
        /// <summary>停止自動化。已經停著就什麼都不做。</summary>
        /// <remarks>
        /// 🔴 這支會被 <c>ExplorersIcebox.Tick</c> 從「沒有 LocalPlayer」那條分支叫到，而那是每一幀。
        /// 底下的停止流程在「本來就停著」的時候全是空操作，卻會每幀對 vnavmesh 送一次 IPC：
        /// 它還沒註冊完（開場那兩秒）就是每幀一個 IpcNotReadyError。呼叫端也有一道同樣的閘門，這裡是第二層。
        /// </remarks>
        internal static bool DisablePlugin()
        {
            // 早退條件刻意連 vnavmesh 有沒有在跑一起判，這樣「早退」與「跑完整段」在行為上完全等價：
            // 使用者在閒置狀態按「停止」時，該停的路徑照樣會被停掉，不會因為這道閘門而少做事。
            if (State == Idle && P.taskManager.NumQueuedTasks == 0 && !NavmeshRunning())
                return true;

            IslandHelper.LoopCounter = 0;
            IslandHelper.RouteLapsDone = 0;
            P.taskManager.Abort();
            if (NavmeshRunning())
                P.navmesh.Stop();
            State = Idle;
            return true;
        }

        /// <summary>vnavmesh 有沒有裝、而且正在走路。沒裝／還沒註冊 IPC 一律回 false（fail-safe）。</summary>
        private static bool NavmeshRunning() => NavmeshIPC.Installed && P.navmesh.IsRunning();

        internal static IceBoxState State = Idle;

        internal static void Tick()
        {
            if (Throttles.GenericThrottle && P.taskManager.NumQueuedTasks == 0 && State != Idle)
            {
                switch (State)
                {
                    case IceBoxState.Start:
                        Task_ReturnToBase.Enqueue();
                        break;
                    case IceBoxState.CheckSell:
                        Task_SellCheck.Enqueue();
                        Task_UpdateShop.Enqueue();
                        break;
                    case IceBoxState.SellToNpc:
                        Svc.Log.Information($"NPC Sell State Active");
                        Task_SellItems.Enqueue();
                        break;
                    case IceBoxState.RunRoute:
                        Svc.Log.Information("Run Route State");
                        Task_GatherLoop.Enqueue();
                        break;
                    default:
                        Svc.Log.Information("Route has been completed, stopping");
                        DisablePlugin();
                        break;
                }
            }
        }
    }
}
