using ECommons.Throttlers;
using ExplorersIcebox.Util;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ExplorersIcebox.Scheduler.Tasks
{
    internal static class Task_UpdateShop
    {
        public static void Enqueue()
        {
            P.taskManager.Enqueue(() => OpenPouch(), "Opening MJI Pouch");
            P.taskManager.Enqueue(() => UpdateCallbacks(), "Updating item callbacks");
            P.taskManager.Enqueue(() => ClosePouch(), "Closing MJI Pouch");
        }

        internal static unsafe bool? OpenPouch()
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIPouch", out var mjiPouch) && GenericHelpers.IsAddonReady(mjiPouch))
            {
                return true;
            }
            else if (GenericHelpers.TryGetAddonMaster<MJIHud>("MJIHud", out var mjiHud) && mjiHud.IsAddonReady)
            {
                if (EzThrottler.Throttle("Open MJI Inventory Pouch"))
                {
                    // MJIHud 是常駐 HUD,按物品欄鈕不會關掉它 ⇒ 用短逃生口(15 幀)。
                    // 15 幀 ≈ 0.25 秒,短於上面那道 500 毫秒節流,所以正常的重試節奏一點都沒變;
                    // 守衛罩的是「HUD 因離島／切區正在關閉」的那幾幀,以及同一扇 HUD 的另一種按法
                    // (Task_GatherMode 的 Fire(false, 11, 0))接力。守衛擋下時照原本落到 return false。
                    if (AddonPressGuard.TryBeginPress("MJIHud", mjiHud, "Isleventory", AddonPressGuard.RoutineRePressEscapeFrames))
                        mjiHud.Isleventory();
                }
            }

            return false;
        }

        internal static unsafe bool? UpdateCallbacks()
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIPouch", out var mjiPouch) && GenericHelpers.IsAddonReady(mjiPouch))
            {
                IslandHelper.UpdateShopCallback();
                return true;
            }

            return false;
        }

        internal static unsafe bool? ClosePouch()
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIPouch", out var mjiPouch))
            {
                if (GenericHelpers.IsAddonReady(mjiPouch))
                {
                    // 關閉鈕按下即關;關閉中的幾幀 IsAddonReady 仍為 true,只靠節流擋不住重按 —— 過守衛。
                    if (EzThrottler.Throttle("Closing the pouch"))
                        AddonPressGuard.TryFire("MJIPouch", mjiPouch, true, 1);
                }
                else
                {
                    return true;
                }
            }

            return false;
        }
    }
}
