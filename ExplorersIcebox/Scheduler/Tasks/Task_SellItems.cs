using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.Throttlers;
using ExplorersIcebox.Util;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using Callback = ECommons.Automation.Callback;

namespace ExplorersIcebox.Scheduler.Tasks
{
    internal static class Task_SellItems
    {
        public static void Enqueue()
        {
            var baseDict = EmbedRoutes.BaseRoutes["Base -> Shopkeep"];
            List<Vector3> waypoints = baseDict.Waypoints;
            ulong dataId = baseDict.TargetId;

            P.taskManager.Enqueue(() => MoveToNpc(waypoints), "Moving to NPC");
            P.taskManager.Enqueue(() => TargetV2(dataId), $"Target task: {dataId}");
            P.taskManager.Enqueue(() => InteractShopKeep(dataId), "Interacting w/ the material seller vendor");
            foreach (var item in IslandHelper.SellItems)
            {
                var itemId = item.Key;
                var sellAmount = item.Value;
                if (ItemData.AlwaysIgnoreSell.Contains(itemId))
                    { continue; }

                P.taskManager.Enqueue(() => SellToNpc(itemId, sellAmount), $"Selling {itemId}");
                P.taskManager.EnqueueDelay(700);
            }

            P.taskManager.Enqueue(() => LeaveNPC(waypoints), "Leaving the NPC");
        }

        internal static bool? MoveToNpc(List<Vector3> List)
        {
            // Insert the logic here post return to move to NPC
            var LastWP = List.Count - 1;
            if (PlayerHelper.GetDistanceToPlayer(List[LastWP]) < 0.5f)
            {
                return true;
            }
            else
            {
                if (!P.navmesh.IsRunning())
                {
                    if (EzThrottler.Throttle("Telling navmesh to move to spot"))
                    {
                        P.navmesh.MoveTo(new List<Vector3>(List), false);
                    }
                }
            }

            return false;
        }

        internal static unsafe bool? InteractShopKeep(ulong dataId)
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIDisposeShop", out var mjiShop) && IsAddonReady(mjiShop))
            {
                return true;
            }
            else if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectString", out var menu) && IsAddonReady(menu))
            {
                if (EzThrottler.Throttle("Selecting Export Materials"))
                {
                    // SelectString 選項按下即關;守衛擋下時照原本落到 return false,下一 tick 再來。
                    AddonPressGuard.TryFire("SelectString", menu, true, 0);
                }
            }
            else
            {
                IGameObject? gameObject = null;
                Utils.TryGetObjectByDataId(dataId, out gameObject);
                if (EzThrottler.Throttle($"Interacting w/ shop seller {dataId}"))
                {
                    Utils.InteractWithObject(gameObject);
                }
            }

            return false;
        }

        internal static bool? TargetV2(ulong dataId)
        {
            IGameObject? gameObject = null;
            var currentTarget = Svc.Targets.Target?.GameObjectId ?? 0;
            Utils.TryGetObjectByDataId(dataId, out gameObject);

        #if DEBUG
            Svc.Log.Debug($"GameObject == Null? [GameObject Doesn't exist]: {gameObject == null}");
            if (gameObject != null)
            {
                Svc.Log.Debug($"Gameobject is current target: {gameObject.IsTarget()}");
                Svc.Log.Debug($"GameObject is targetable: {gameObject.IsTargetable}");

            }
        #endif

            if (gameObject == null || gameObject.IsTarget() || !gameObject.IsTargetable)
            {
                return true;
            }
            else if (gameObject != null)
            {
                if (EzThrottler.Throttle($"Targeting: {dataId}"))
                {
                #if DEBUG
                    Svc.Log.Debug($"Targeting: {gameObject.Name}");
                #endif
                    Utils.TargetgameObject(gameObject);
                }
            }

            return false;
        }

        internal static unsafe bool? SellToNpc(int itemId, int sellAmount)
        {
            var callback = ItemData.IslandItems[itemId].SellSlot;

            // 🔴 就緒閘門(ShippingDialogReady):TryGetAddonByName 只判指標非 0,擋不住「窗剛建立、還沒載完」的那幾幀;
            //    這扇確認窗是上一 tick 才被建立的,守衛對這個全新位址沒有紀錄,節流的 key 每件商品不同也首次必放行。
            // 🔑 這與守衛擋的「關閉中」是**不同**的失效形狀(關閉中的那幾幀 IsAddonReady 反而全過),兩道防線互補。
            // 📌 不就緒時落到下面的 else if:那條路的節流(500 毫秒)與守衛(同位址同參數 15 幀)都還壓著剛才那次選商品,
            //    所以什麼都不會被送出,等同「這一 tick 空轉、下一 tick 再來」。
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIDisposeShopShipping", out var mjiShip) && ShippingDialogReady(mjiShip))
            {
                // 確認出售按下即關。MJIDisposeShopShipping 在守衛裡是「單答終結窗」(參數組併 key):
                // 上一件的確認窗還在關閉中時,下一件的 (11, 另一個數量) 不會再送到同一個位址上。
                // 🔴 守衛擋下時必須 return false —— return true 等於把這一件商品靜默跳過不賣。先用唯讀的 IsPressBlocked 問:
                //    ①不會白白消耗掉下面那把節流的額度(消耗掉的話下一 tick 節流回 false,又落回 return true 而漏掉這一件)
                //    ②最久等到逃生口 90 幀 ≈ 1.5 秒,遠短於 NeoTaskManager 的 20 秒逾時。
                if (AddonPressGuard.IsPressBlocked("MJIDisposeShopShipping", mjiShip))
                    return false;

                if (EzThrottler.Throttle($"Selling {itemId} | Amount: {sellAmount}"))
                    AddonPressGuard.TryFire("MJIDisposeShopShipping", mjiShip, true, 11, sellAmount);

                return true;
            }
            // 同一道就緒閘門(與 LeaveNPC 對同一扇 MJIDisposeShop 的判法一致);就緒與否兩條路都落到最後那個 return false。
            else if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIDisposeShop", out var mjiShop) && GenericHelpers.IsAddonReady(mjiShop))
            {
                // 選商品後商店窗仍開著(結構證據:LeaveNPC 在所有 SellToNpc 之後仍預期它在,送 Fire(1) 關它)
                // ⇒ 屬於「按了也不會關掉自己」的窗,用短逃生口。每件商品的 (12, slot) 是不同參數組,
                // 互不干擾,只擋「同一件在同一塊位址上於 15 幀內重送」。
                if (EzThrottler.Throttle($"Interacting with {itemId} shop sell"))
                    AddonPressGuard.TryFireRoutine("MJIDisposeShop", mjiShop, true, 12, callback);
            }

            return false;
        }

        /// <summary>
        /// 「確認出售」窗現在真的可以按嗎。不就緒時寫一行要使用者回報的診斷。
        /// </summary>
        /// <remarks>
        /// 🔑 這一行的有無直接回答「MJIDisposeShopShipping 真的會有『已經在 addon 清單裡、卻還沒就緒』
        /// 的那幾幀嗎」—— 那是加上這道閘門的理由,離線只能從結構推,實機 log 才看得到。
        /// <para>
        /// ⚠️ 這與「關閉中」無關:那幾幀 <c>IsAddonReady</c> 反而三關全過,擋它的是 <c>AddonPressGuard</c>。
        /// </para>
        /// </remarks>
        private static unsafe bool ShippingDialogReady(AtkUnitBase* addon)
        {
            if (GenericHelpers.IsAddonReady(addon)) return true;

            if (EzThrottler.Throttle("Icebox-ShippingDialog-NotReady", 10000))
                Svc.Log.Information("[出售] MJIDisposeShopShipping 已經在 addon 清單裡、但還沒就緒(ULD 未載完或尚未顯示),這一 tick 不送確認 callback,下一 tick 再來。");

            return false;
        }

        internal static unsafe bool? WaitForSell()
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIDisposeShopShipping", out var mjiShip))
            {
                if (!GenericHelpers.IsAddonReady(mjiShip))
                    return true;
            }

            return false;
        }

        internal static unsafe bool? LeaveNPC(List<Vector3> List)
        {
            var LastWP = List.Count - 1;
            if (PlayerHelper.GetDistanceToPlayer(IslandHelper.BaseStart) < 0.5f)
            {
                Svc.Log.Information("Leave NPC will complete after this");
                SchedulerMain.State = Enums.IceBoxState.RunRoute;
                return true;
            }
            else if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIDisposeShop", out var mjiShop) && IsAddonReady(mjiShop))
            {
                // 關閉鈕按下即關;關閉中的那幾幀 IsAddonReady 三關仍全過,只靠 500 毫秒節流擋不住重按。
                // 守衛擋下時照原本落到下一行的 return false,下一 tick 再來。
                if (EzThrottler.Throttle("Closing shop"))
                    AddonPressGuard.TryFire("MJIDisposeShop", mjiShop, true, 1);
                return false;
            }
            else if (!P.navmesh.IsRunning())
            {
                List<Vector3> reverseWp = new(List);

                reverseWp.Reverse();

                if (EzThrottler.Throttle("Telling navmesh to move to spot"))
                {
                    P.navmesh.MoveTo(new List<Vector3>(reverseWp), false);
                }
                return false;
            }
            else
            {
                return false;
            }
        }
    }
}
