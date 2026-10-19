using ECommons.Automation;
using ECommons.Throttlers;
using ExplorersIcebox.Util;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Callback = ECommons.Automation.Callback;

namespace ExplorersIcebox.Scheduler.Tasks
{
    internal static class Task_GatherMode
    {
        // Used to just swap to the gathering mode in island sanctuary. 
        // Trying to lessen the necessity of visland if possible, and just directly call the functions where I can
        public static void Enqueue()
        {
            P.taskManager.Enqueue(() => GatherMode());
        }

        internal unsafe static bool? GatherMode()
        {
            if (MJIManager.Instance()->CurrentMode == 1) // Gather Mode
                return true;
            else
            {
                if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var ContextAddon) && GenericHelpers.IsAddonReady(ContextAddon))
                {
                    if (EzThrottler.Throttle("Throttling the ContextIconMenu Callback to not Crash"))
                    {
                        // 只送「採集模式」這一發:選了之後選單自己收起(台服 ContextIconMenu 是常駐窗,收起＝隱藏)。
                        // 刻意不在同一幀補送 -1:選項處理常式在 Callback.Fire 裡就跑完,選單可能已進入關閉而三關仍過＝攔不到的存取違規。
                        AddonPressGuard.TryFire("ContextIconMenu", ContextAddon, true, 0, 1, 82042, 0, 0);
                    }
                }
                else if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIHud", out var MJIAddon) && GenericHelpers.IsAddonReady(MJIAddon))
                {
                    if (EzThrottler.Throttle("Throttling opening the island mode"))
                    {
                        // MJIHud 是常駐 HUD,按這顆開的是 ContextIconMenu、不會關掉 HUD 自己
                        // ⇒ 短逃生口(15 幀 ≈ 0.25 秒,短於上面那道 500 毫秒節流,重試節奏不變)。
                        AddonPressGuard.TryFireRoutine("MJIHud", MJIAddon, false, 11, 0);
                    }
                }
            }

            return false;
        }
    }
}
