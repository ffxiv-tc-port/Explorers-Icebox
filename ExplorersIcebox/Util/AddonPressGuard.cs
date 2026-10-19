using Dalamud.Game.Addon.Lifecycle;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Callback = ECommons.Automation.Callback;

namespace ExplorersIcebox.Util;

/// <summary>
/// 「同一扇視窗的同一個按法,按過就不要再按,直到它真的收掉」的共用閘門。
/// </summary>
/// <remarks>
/// 🔴 <c>SelectString</c>／<c>MJIPouch</c>／<c>MJIDisposeShopShipping</c> 這類「按下即關」的窗被按下之後有
/// <b>「正在關閉中」的幾幀</b>:<c>TryGetAddonByName</c> 仍然回得到實例、<c>IsVisible</c> 與
/// <c>UldManager.LoadedState == Loaded</c> 也都還成立 —— 也就是說 <c>IsAddonReady</c>
/// <b>三關全過、擋不住這個窗口</b>。此時再對它 <c>Callback.Fire</c>／送 <c>ReceiveEvent</c>
/// 就是 <c>try</c>/<c>catch</c> 攔不到的原生 AccessViolationException ——
/// <b>唯一的防護是「不要送第二次」,不是「送了再接住」</b>。
/// <para>
/// ⚠️ 呼叫端原有的 <c>EzThrottler.Throttle(...)</c>(預設 500 毫秒)<b>不是</b>防護:
/// 它記的是「這把 key 上一次放行在哪個時刻」而不是「這扇窗已經按過」,<b>第一次呼叫必定放行</b>,
/// key 全域持久,而且每個呼叫點各用各的 key。低 FPS 下一扇窗關閉超過 500 毫秒、
/// 或子窗開得比節流窗慢,節流就會放行第二發。
/// </para>
/// <para>
/// 🔑 <b>做法</b>:按下之前先登記「哪個名字的哪一個實例(窗名＋位址)、被送過哪一組參數」,
/// 在觀察到那扇窗真的走完生命週期之前不准再送同一組。同名兩扇並存時各記各的。
/// 🔴 全程只做<b>位址等值比較,永遠不解參</b> —— 被記下的那個位址隨時可能已經失效。
/// </para>
/// <para>
/// 📌 <b>為什麼 key 要含「參數組」而不是只看實例位址</b>:<c>MJIDisposeShop</c> 逐件出售時對同一扇窗
/// 連送不同的 <c>(12, slot)</c>,窗不會因此關掉;只看位址會把下一件擋掉。
/// 例外是 <see cref="SingleAnswerAddons"/>:「回答一次即終結」的窗不管參數為何一律併成同一個 key。
/// 另外,終結按法(逃生口長於 <see cref="RoutineRePressEscapeFrames"/>)還熱著時,同一扇窗的任何按法都擋;
/// 這一發是終結按法時,同一扇窗任何還熱著的紀錄也都擋(<see cref="TryFindHotSibling"/>)。
/// </para>
/// <para>
/// <b>解除點</b>:登記時在同名清單裡找得到、之後從清單消失(每個 framework tick 輪詢);同位址 PostSetup
/// (這一幀才登記的不清;逐件出售時 <c>MJIDisposeShopShipping</c> 關掉再開常重用同一塊位址);
/// 常駐窗連續隱藏 <see cref="HiddenReleaseFrames"/> 幀;逃生口。PreFinalize 是<b>上鎖點</b>:鎖到位址消失或同位址重建。
/// ⚠️ 刻意<b>不</b>把 <c>PostRefresh</c> 也當解除點:它有可能在「關閉中」那幾幀觸發,等於把封鎖提早解除。
/// </para>
/// <para>
/// 🔴 <b>逃生口是刻意的</b>(<see cref="DefaultEscapeFrames"/>):萬一某扇窗既不 finalize
/// 也不重新 setup(例如上一次的 callback 根本沒生效、視窗就是還開著),沒有逃生口的話呼叫端會
/// <b>永遠</b>按不下去 —— 而本外掛的 <c>NeoTaskManager</c> 是 <c>abortOnTimeout: true, timeLimitMS: 20000</c>,
/// 卡住 20 秒會清掉<b>整條</b>佇列。用<b>幀數</b>而不是毫秒:危險窗口的長度本來就是以幀計的,遊戲卡頓時兩者一起拉長。
/// </para>
/// <para>
/// 📌 第一次看到某扇窗的某一組參數一律當場按下去;被擋下時各呼叫端本來就是輪詢重試,下個 tick 再來。
/// 被擋回 <see langword="false"/>,<b>永遠不回 <see langword="null"/></b>(NeoTaskManager 三態的 <c>null</c> 會清佇列)。
/// 任何不確定一律不放行。
/// </para>
/// <para>⚠️ 只在主執行緒使用(與呼叫端的 <c>EzThrottler</c> 同一個前提)。</para>
/// </remarks>
internal static unsafe class AddonPressGuard
{
    private const string Handle = "[AddonPressGuard]";

    /// <summary>
    /// 已經按過、那扇窗卻既沒消失也沒重建時,最多再等這麼多幀才允許補按一次。
    /// </summary>
    /// <remarks>
    /// 🔑 這不是節流 —— 真正的防護是「同一扇窗的同一組參數只按一次」,這個值只是防死鎖的逃生口。
    /// 90 幀(60fps 下約 1.5 秒)遠遠大於「關閉中的那幾幀」,補按永遠不會落在危險窗口內。
    /// 走到這裡代表「按了卻沒關掉」,寫 <c>Information</c>(要使用者回報的診斷)。
    /// 給「按下即關」的窗用:<c>SelectString</c>、<c>MJIPouch</c> 的關閉鈕、
    /// <c>MJIDisposeShop</c> 的關閉鈕、<c>MJIDisposeShopShipping</c> 的確認鈕、<c>ContextIconMenu</c>。
    /// </remarks>
    internal const int DefaultEscapeFrames = 90;

    /// <summary>
    /// 給「按一次翻一頁／開子窗,窗自己不會因為被按而消失」的多次互動窗用的短逃生口。
    /// </summary>
    /// <remarks>
    /// 關閉中的危險窗口 &lt; 10 幀,15 幀不落在裡面;每次多等 0.25 秒幾乎無感。
    /// 走這種逃生口是常態,寫 <c>Debug</c> 不洗版。
    /// 📌 本外掛用在 <c>MJIHud</c>(無人島常駐 HUD,按鈕只是開子窗)與 <c>MJIDisposeShop</c>
    /// 的「選商品」按法(選完商店窗仍開著)。
    /// 🔑 15 幀(60fps 下約 0.25 秒)<b>短於</b>這些站點原有的 500 毫秒 <c>EzThrottler</c>,
    /// 所以正常路徑的重試節奏完全不變 —— 守衛在這裡只擋「同一塊位址在 10 幀內被重按」。
    /// </remarks>
    internal const int RoutineRePressEscapeFrames = 15;

    /// <summary>輪詢同名實例的上限(遊戲同名實例上限);掃到第一個空的就提早停。</summary>
    private const int MaxAddonIndex = 256;

    /// <summary>
    /// 同一扇窗底下的「參數組」紀錄上限。萬一清理判準在某個版本失效,也不要讓表無限長大。
    /// </summary>
    /// <remarks>
    /// <c>MJIDisposeShop</c> 逐件出售時每件商品的 <c>(12, slot)</c> 都是不同的 key,會在商店開著的整段期間累積。
    /// 到頂時只淘汰已過自己逃生口的冷紀錄(<see cref="EvictCold"/>);全部還熱就這一發不送。
    /// </remarks>
    private const int PressKeyCap = 256;

    /// <summary>常駐窗按下後,要<b>連續</b>觀察到「還在清單裡、但已隱藏」這麼多幀才解除。</summary>
    /// <remarks>
    /// 🔴 這個數字不承重:真正的防護是 <see cref="TryBeginPress(string, AtkUnitBase*, string, int)"/> 對常駐窗多要求的
    /// 「送出前必須可見」,它與這裡的解除條件(連續不可見)互為邏輯反面 ⇒ 解除之後要再送,
    /// 中間必須有一次遊戲自己把窗重新 Show 起來,而正在拆除的窗不會被重新 Show。N 只決定要等多久。
    /// </remarks>
    private const int HiddenReleaseFrames = 20;

    /// <summary>每隔幾幀回收一次「已經不可能再擋住任何人」的冷紀錄。</summary>
    private const int SweepIntervalFrames = 60;

    /// <summary>冷紀錄回收的年齡下限(幀),遠大於任何逃生口:回收不改變任何一次「按／不按」的判定。</summary>
    private const int SweepMinAgeFrames = 1800;

    /// <summary>
    /// 「一扇窗一生只回答一次」的視窗:這些名字底下不管送什麼參數,一律併成同一個 key。
    /// </summary>
    /// <remarks>
    /// <c>SelectYesno</c>、<c>SelectIconString</c>:本插件目前沒有按它的站點,依艦隊慣例先列入,將來接上就自動併 key。
    /// <c>MJIDisposeShopShipping</c>:「確認出售」按下即關。每件商品送的 <c>(11, 數量)</c> 參數不同,但上一件的確認窗
    /// 還在關閉中時(<c>EnqueueDelay(700)</c> 只是時間間隔,不是結構守衛)再送一次就是對關閉中的窗重按 —— 所以要併 key。
    /// <c>SelectString</c>、<c>ContextIconMenu</c>:選一項就關(後者是收起),換一個選項就會打在關閉中的窗上。
    /// ⚠️ 只放<b>回答一次就結束</b>的窗。<c>MJIDisposeShop</c>(逐件送不同 slot)、<c>MJIHud</c>
    /// 這種「窗一直開著、刻意連送不同 callback」的<b>絕對不能</b>放進來,會把正常流程一起擋掉。
    /// </remarks>
    private static readonly HashSet<string> SingleAnswerAddons = new(StringComparer.Ordinal)
    {
        "SelectYesno",
        "MJIDisposeShopShipping",
        "SelectString",
        "SelectIconString",
        "ContextIconMenu",
    };

    /// <summary>
    /// 已經<b>實機證實</b>是「常駐」的視窗名:關閉只是被設成不可見,位址永遠留在清單裡,
    /// PreFinalize 與 PostSetup 都不會再發生。只有這些窗名套用「連續隱藏才解除」。
    /// </summary>
    /// <remarks>
    /// 🔴 <b>這份名單同時是「送出前必須可見」的名單</b>(與「連續隱藏才解除」互為反面)。
    /// 把會被銷毀的窗誤收進來,等於在它拆除途中提早解除封鎖。
    /// </remarks>
    private static readonly HashSet<string> PersistentAddons = new(StringComparer.Ordinal)
    {
        "ContextMenu",
        "ContextIconMenu",
        "Talk",
    };

    /// <summary>紀錄身分:同名實例各自一筆。位址只做等值比較。</summary>
    private readonly record struct PressSlot(string AddonName, nint Address);

    /// <param name="EscapeFrames">登記當時的逃生口;判冷熱與「是不是終結按法」都用它。</param>
    private readonly record struct PressRecord(long Frame, int EscapeFrames);

    /// <summary>一扇窗(窗名＋位址)底下的全部參數組與解除狀態。刻意是 class:欄位要就地改。</summary>
    private sealed class SlotRecords
    {
        public Dictionary<string, PressRecord> ByKey { get; } = new(StringComparer.Ordinal);

        public long LastFrame;

        /// <summary>連續觀察到「還在清單裡、但已隱藏」的幀數;只有常駐窗會累加,看得見或重新按下就歸零。</summary>
        public int HiddenFrames;

        /// <summary>登記時在同名清單裡找得到這個位址;只有它成立才准用「從清單消失」解除。</summary>
        public bool Listed;

        /// <summary>看過 PreFinalize:在位址消失或同位址重建之前,任何按法都不准,也沒有逃生口。</summary>
        public bool Finalizing;
    }

    private static readonly Dictionary<PressSlot, SlotRecords> Pressed = new();

    private static readonly Dictionary<string, IAddonLifecycle.AddonEventDelegate> Watchers =
        new(StringComparer.Ordinal);

    private static readonly List<PressSlot> ReleaseBuf = [];
    private static readonly List<string> StaleKeysBuf = [];

    /// <summary>「這個窗名第一次走隱藏解除」只寫一行 Information,之後不再寫。</summary>
    private static readonly HashSet<string> HiddenReleaseReported = new(StringComparer.Ordinal);

    /// <summary>守衛自己的幀計數器。</summary>
    private static long frameCount;

    /// <summary>時鐘是否已訂閱。用 <see cref="Interlocked"/> 而不是 <see cref="bool"/>,理由見 <see cref="EnsureClock"/>。</summary>
    private static int clockSubscribed;

    /// <summary>
    /// 🔴🔴 <b>刻意不用 <c>Svc.PluginInterface.UiBuilder.FrameCount</c>。</b>
    /// </summary>
    /// <remarks>
    /// UI 隱藏期間(隱藏 UI 熱鍵、過場動畫、GPose)那個計數器<b>完全不前進</b>,但按下點(Framework.Update → 任務)照常每幀被叫到
    /// ⇒ 逃生口永遠不到期,任務會卡到 20 秒逾時、整條佇列被清掉。<c>Svc.Framework.Update</c> 不受 UI 隱藏影響,所以自己數。
    /// </remarks>
    private static long CurrentFrame => frameCount;

    /// <summary>
    /// 時鐘本體。<b>遞增必須是第一行、前面不可以有任何條件或提早 return</b> —— 少數一幀沒數到就等於逃生口被拉長。
    /// 之後才是每幀的解除輪詢與冷紀錄回收。
    /// </summary>
    private static void OnFrameworkUpdate(IFramework framework)
    {
        frameCount++;
        PollRecords();
        if (frameCount % SweepIntervalFrames == 0) SweepColdRecords();
    }

    /// <summary>
    /// 訂閱幀時鐘(可重複呼叫,只有第一次會真的訂閱)。
    /// </summary>
    /// <remarks>
    /// 🔴 用 <see cref="Interlocked.CompareExchange(ref int, int, int)"/> 而不是 <c>bool</c> 旗標:
    /// 重複訂閱<b>不是沒效果</b>,而是同一個 tick 讓計數器前進 2 ——
    /// 所有逃生口等於<b>對半砍</b>,會把補按往「關閉中」的危險窗口推。
    /// <para>
    /// 📌 呼叫時機有兩處,兩處都必要:①插件建構子裡,<b>在 <c>Svc.Framework.Update += Tick</c> 之前</b>——同一個插件的
    /// <c>Framework.Update</c> 處理常式共用一個 try/catch,排在前面的擲例外時後面的那個 tick 不會被呼叫,時鐘排第一個才不會被連累;
    /// ②<see cref="TryBeginPress(string, AtkUnitBase*, string, int)"/> 的<b>所有提早 return 之前</b>,當作後援。
    /// </para>
    /// </remarks>
    internal static void EnsureClock()
    {
        if (Interlocked.CompareExchange(ref clockSubscribed, 1, 0) != 0) return;

        Svc.Framework.Update += OnFrameworkUpdate;
    }

    /// <summary>
    /// 登記「即將對這扇視窗送出這一組參數」。<b>回 <see langword="false"/> ＝這一幀絕對不能送。</b>
    /// </summary>
    /// <param name="addonName">視窗名稱(解除封鎖的監聽器與輪詢都以它為準)。空字串一律不放行。</param>
    /// <param name="addon">目標實例。<b>只當作識別用的位址,本方法不解參。</b></param>
    /// <param name="pressKey">
    /// 這一次的「按法」(參數組)。同一扇窗上不同的按法互不干擾;要擋的是<b>同一個按法重複送</b>。
    /// 傳空字串代表「整扇窗只有一種按法」。<see cref="SingleAnswerAddons"/> 裡的窗會忽略這個參數。
    /// </param>
    /// <param name="escapeFrames">逃生口幀數,見 <see cref="DefaultEscapeFrames"/> 與 <see cref="RoutineRePressEscapeFrames"/>。</param>
    /// <remarks>
    /// ⚠️ 一定要放在條件式的<b>最後</b>一項(其他閘門都通過、確定要按了才呼叫)—— 它有副作用
    /// (記下這一次按壓)。放在前面會把「其實沒按」記成「按過了」,讓真正該按的那一次被擋到逃生口為止。
    /// </remarks>
    public static bool TryBeginPress(string addonName, AtkUnitBase* addon, string pressKey = "",
                                     int escapeFrames = DefaultEscapeFrames)
    {
        // 🔴 在所有提早 return 之前 —— 時鐘停住比擋不住重按更難查。
        EnsureClock();

        if (addon == null || string.IsNullOrEmpty(addonName)) return false;

        // 回答一次就結束的窗:不管是哪一條路徑、送的是什麼參數,一律算同一次按。
        if (SingleAnswerAddons.Contains(addonName))
            pressKey = string.Empty;

        var address = (nint)addon;
        // 🔴 常駐窗送出前必須可見:與「連續隱藏才解除」互為反面,兩者一組才是防護。
        if (PersistentAddons.Contains(addonName) && !PersistentAddonVisible(addonName, address))
        {
            if (EzThrottler.Throttle($"AddonPressGuard-PersistentHidden-{addonName}", 1000))
                Svc.Log.Information($"{Handle} 「{addonName}」是常駐窗,實例 0x{address:X} 這一幀不可見(或不在同名清單裡),不送 —— " +
                                    "它與「連續隱藏才解除」互為反面,兩者一組才是防護。");
            return false;
        }

        EnsureWatching(addonName);

        var frame = CurrentFrame;
        // 短逃生口的窗(常駐 HUD、翻頁式對話框)走逃生口是常態,log 降到 Debug 免得洗版。
        var routine = escapeFrames <= RoutineRePressEscapeFrames;
        var slot = new PressSlot(addonName, address);

        Pressed.TryGetValue(slot, out var records);

        if (records != null && records.Finalizing)
        {
            if (EzThrottler.Throttle($"AddonPressGuard-Hold-{addonName}", 1000))
                Svc.Log.Information($"{Handle} 「{addonName}」(實例 0x{address:X})已經進入銷毀(PreFinalize)、還沒從清單消失," +
                                    $"這一幀不送「{pressKey}」 —— 對關閉中的視窗送 callback 是攔不到的存取違規。");
            return false;
        }

        if (records != null && TryFindHotSibling(records, pressKey, frame, !routine, out var siblingKey, out var siblingRec))
        {
            LogSiblingHold(addonName, address, pressKey, siblingKey, siblingRec);
            return false;
        }

        if (records != null && records.ByKey.TryGetValue(pressKey, out var pressed))
        {
            var waited = frame - pressed.Frame;
            if (waited < escapeFrames)
            {
                // 🔴 這就是崩潰的那一幀。按下即關的窗寫要使用者回報的診斷(節流);多次互動窗只寫 Debug。
                if (routine)
                {
                    Svc.Log.Debug($"{Handle} 「{addonName}」(實例 0x{address:X},參數組「{pressKey}」)" +
                                  $"按過之後 {waited} 幀仍在,這一幀不再送。");
                }
                else if (EzThrottler.Throttle($"AddonPressGuard-Hold-{addonName}", 1000))
                {
                    Svc.Log.Information($"{Handle} 「{addonName}」(實例 0x{address:X},參數組「{pressKey}」)" +
                                        "按過之後還沒觀察到它收掉,這一幀不再送 callback —— " +
                                        "對關閉中的視窗送 callback 是攔不到的存取違規。");
                }

                return false;
            }

            // 逃生口:等了遠超過關閉所需的時間,窗仍在。視為那次按壓沒生效
            // (或這是另一扇窗重用了同一塊記憶體而兩條解除軌都沒抓到),放行補按一次。
            if (routine)
            {
                Svc.Log.Debug($"{Handle} 「{addonName}」(實例 0x{address:X},參數組「{pressKey}」)" +
                              $"按下後 {waited} 幀仍在,依多次互動窗的常態放行補按。");
            }
            else if (EzThrottler.Throttle($"AddonPressGuard-Release-{addonName}", 10000))
            {
                Svc.Log.Information($"{Handle} 「{addonName}」(實例 0x{address:X},參數組「{pressKey}」)" +
                                    $"按下後 {waited} 幀既沒有被銷毀也沒有重新建立,判定為「上一次按下沒生效」" +
                                    "而不是「正在關閉」,解除封鎖讓呼叫端重試。");
            }
        }

        if (records == null)
        {
            records = new SlotRecords();
            Pressed[slot] = records;
        }
        else if (records.ByKey.Count >= PressKeyCap && !records.ByKey.ContainsKey(pressKey) && !EvictCold(records, frame))
        {
            if (EzThrottler.Throttle($"AddonPressGuard-Full-{addonName}", 10000))
                Svc.Log.Information($"{Handle} 「{addonName}」底下 {records.ByKey.Count} 筆參數組紀錄全都還在逃生口內,這一發不送。");
            return false;
        }

        if (!records.Listed) records.Listed = IsListed(slot);
        records.ByKey[pressKey] = new PressRecord(frame, escapeFrames);
        records.LastFrame = frame;
        // 🔴 承重:隱藏解除是「從最後一次按下起算」連續隱藏幾幀。
        records.HiddenFrames = 0;
        // 跨外掛重按診斷：只在真的送出按壓時記一行，刻意不節流。
        Svc.Log.Verbose($"[按窗診斷] plugin=ExplorersIcebox addon={addonName} addr=0x{address:X} key={pressKey}");
        return true;
    }

    /// <summary>
    /// 唯讀地問「現在對這扇窗送這一組參數，會不會被守衛擋下」。<b>不登記任何按壓。</b>
    /// </summary>
    /// <returns>
    /// <see langword="true"/> ＝現在按下去會被擋(等同 <see cref="TryBeginPress(string, AtkUnitBase*, string, int)"/> 會回 false)。
    /// </returns>
    /// <remarks>
    /// 🔑 <b>用途</b>:呼叫端在守衛之前還有<b>會被消耗掉的</b>前置閘門(本外掛是 <c>EzThrottler</c>)時,
    /// 先用這支問一次就能在還沒動用那些閘門之前直接回「這一輪不做」,不會白白吃掉一次節流額度。
    /// <para>
    /// ⚠️ 它<b>不能</b>取代 <see cref="TryBeginPress(string, AtkUnitBase*, string, int)"/>:真正要按的那一刻
    /// 仍然必須走登記路徑,否則就沒有人記下「按過了」。正確用法是「先問、再照原本流程按」。
    /// </para>
    /// </remarks>
    public static bool IsPressBlocked(string addonName, AtkUnitBase* addon, string pressKey = "",
                                      int escapeFrames = DefaultEscapeFrames)
    {
        EnsureClock();

        // 與 TryBeginPress 對齊:那邊對 null 也是回「不要按」。
        if (addon == null || string.IsNullOrEmpty(addonName)) return true;

        if (SingleAnswerAddons.Contains(addonName))
            pressKey = string.Empty;

        if (PersistentAddons.Contains(addonName) && !PersistentAddonVisible(addonName, (nint)addon)) return true;
        if (!Pressed.TryGetValue(new PressSlot(addonName, (nint)addon), out var records)) return false;
        if (records.Finalizing) return true;

        var frame = CurrentFrame;
        if (TryFindHotSibling(records, pressKey, frame, escapeFrames > RoutineRePressEscapeFrames, out _, out _))
            return true;

        return records.ByKey.TryGetValue(pressKey, out var pressed)
               && frame - pressed.Frame < escapeFrames;
    }

    /// <summary>
    /// 同一扇窗(同名、同位址)上<b>別的按法</b>是否擋住這一發:終結按法還熱著時任何按法都擋;
    /// 這一發本身是終結按法(<paramref name="terminating"/>)時,任何還熱著的紀錄都擋。
    /// </summary>
    private static bool TryFindHotSibling(SlotRecords records, string pressKey, long frame,
                                          bool terminating, out string siblingKey, out PressRecord siblingRec)
    {
        foreach (var (k, rec) in records.ByKey)
        {
            if (string.Equals(k, pressKey, StringComparison.Ordinal)) continue;
            if (frame - rec.Frame >= rec.EscapeFrames) continue;
            if (terminating || rec.EscapeFrames > RoutineRePressEscapeFrames)
            {
                siblingKey = k;
                siblingRec = rec;
                return true;
            }
        }

        siblingKey = string.Empty;
        siblingRec = default;
        return false;
    }

    /// <summary>被同窗別的按法擋下:擋人的是終結按法時寫 Information(節流),只是多次互動那一級的短等待寫 Debug。</summary>
    private static void LogSiblingHold(string addonName, nint address, string pressKey, string siblingKey, PressRecord siblingRec)
    {
        if (siblingRec.EscapeFrames <= RoutineRePressEscapeFrames)
        {
            Svc.Log.Debug($"{Handle} 「{addonName}」(實例 0x{address:X})的參數組「{siblingKey}」剛按過,這一幀不送「{pressKey}」。");
        }
        else if (EzThrottler.Throttle($"AddonPressGuard-Hold-{addonName}", 1000))
        {
            Svc.Log.Information($"{Handle} 「{addonName}」(實例 0x{address:X})的參數組「{siblingKey}」按過之後還沒觀察到它收掉," +
                                $"這一幀不送「{pressKey}」 —— 對關閉中的視窗送 callback 是攔不到的存取違規。");
        }
    }

    /// <summary>移除這扇窗底下已過自己逃生口的紀錄(它們本來就會被放行,刪掉不改判定);回 true＝至少騰出一格。</summary>
    private static bool EvictCold(SlotRecords records, long frame)
    {
        var cold = records.ByKey.Where(kv => frame - kv.Value.Frame >= kv.Value.EscapeFrames).Select(kv => kv.Key).ToArray();
        foreach (var k in cold)
            records.ByKey.Remove(k);
        return cold.Length > 0;
    }

    /// <summary>
    /// <see cref="TryBeginPress(string, AtkUnitBase*, string, int)"/> 給 <c>AddonMaster</c> 按法用的多載。
    /// </summary>
    /// <remarks>
    /// <c>AddonMaster</c> 的按法（<c>Isleventory()</c> 這種）走的是模擬點擊／<c>ReceiveEvent</c>，
    /// 比送 callback <b>更早</b>踩到關閉中的窗，所以同一扇窗的這些按法必須過同一個守衛。
    /// 收 <see cref="IAddonMasterBase"/> 而不是指標，是為了讓 ImGui 偵錯視窗那種非 <c>unsafe</c> 的
    /// 呼叫端不必為了接守衛而改成 <c>unsafe</c>。
    /// </remarks>
    public static bool TryBeginPress(string addonName, IAddonMasterBase master, string pressKey = "",
                                     int escapeFrames = DefaultEscapeFrames)
        => master != null && TryBeginPress(addonName, master.Base, pressKey, escapeFrames);

    /// <summary>
    /// 先過守衛再 <see cref="Callback.Fire(AtkUnitBase*, bool, object[])"/>;參數組自動當作按法 key。
    /// 給<b>「按下即關」</b>的窗用(逃生口 <see cref="DefaultEscapeFrames"/>)。
    /// </summary>
    /// <returns>
    /// <see langword="true"/>＝這一幀真的送出去了;<see langword="false"/>＝被守衛擋下(這一輪沒按到,下一輪再來)。
    /// 呼叫端<b>不要</b>把它當成任務完成與否的訊號 —— 各站原本的回傳路徑不因此改變。
    /// </returns>
    /// <remarks>
    /// <paramref name="addon"/> 必須是<b>本幀</b>剛從 <c>TryGetAddonByName</c> 拿到的實例,
    /// 前置的 <c>IsAddonReady</c> 等閘門仍由呼叫端負責(本方法不重複檢查,也不改變它們)。
    /// </remarks>
    public static bool TryFire(string addonName, AtkUnitBase* addon, bool updateState, params object[] values)
    {
        if (!TryBeginPress(addonName, addon, BuildPressKey(updateState, values)))
            return false;

        Callback.Fire(addon, updateState, values);
        return true;
    }

    /// <summary>
    /// 同 <see cref="TryFire"/>,但給<b>「按了也不會關掉自己」</b>的窗用
    /// (逃生口 <see cref="RoutineRePressEscapeFrames"/>,被擋時只寫 <c>Debug</c>)。
    /// </summary>
    /// <remarks>
    /// 用在 <c>MJIHud</c>(常駐 HUD)與 <c>MJIDisposeShop</c> 的「選商品」按法:
    /// 這些窗被按之後開的是子窗,自己留著,呼叫端本來就會每 500 毫秒重送到子窗出現為止。
    /// 15 幀短於那 500 毫秒,所以<b>正常重試節奏一點都沒變</b>,擋掉的只有「同一塊位址在 10 幀內重按」
    /// —— 也就是這扇窗因為離島／切區而正在關閉的那個危險窗口。
    /// </remarks>
    public static bool TryFireRoutine(string addonName, AtkUnitBase* addon, bool updateState, params object[] values)
    {
        if (!TryBeginPress(addonName, addon, BuildPressKey(updateState, values), RoutineRePressEscapeFrames))
            return false;

        Callback.Fire(addon, updateState, values);
        return true;
    }

    /// <summary>
    /// 把 <c>Callback.Fire</c> 的參數組壓成穩定的「按法」字串。
    /// </summary>
    /// <remarks>
    /// 用不變文化格式化,免得數字在別的地區設定下變成不同的字串(那會讓同一個按法被當成兩種)。
    /// </remarks>
    internal static string BuildPressKey(bool updateState, object[]? args)
    {
        if (args == null || args.Length == 0) return updateState ? "T" : "F";

        StringBuilder sb = new(updateState ? "T" : "F");
        foreach (var arg in args)
        {
            sb.Append('|');
            sb.Append(arg switch
            {
                null => "null",
                IFormattable form => form.ToString(null, CultureInfo.InvariantCulture),
                _ => arg.ToString() ?? string.Empty,
            });
        }

        return sb.ToString();
    }

    /// <summary>外掛卸載時硬拆時鐘與所有監聽器(不留指向本組件的委派)。</summary>
    /// <remarks>
    /// 本 pin 的插件層級服務在卸載時會自動拆監聽器與 <c>Framework.Update</c> 訂閱,
    /// 這裡再拆一次是讓拆除順序明確、不依賴那條隱含行為。
    /// </remarks>
    internal static void ForceTeardown()
    {
        if (Interlocked.CompareExchange(ref clockSubscribed, 0, 1) == 1)
            Svc.Framework.Update -= OnFrameworkUpdate;

        foreach (var (addonName, handler) in Watchers)
        {
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, addonName, handler);
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, addonName, handler);
        }

        Watchers.Clear();
        Pressed.Clear();
    }

    /// <summary>這一幀在同名清單裡對某個位址的觀察。</summary>
    /// <remarks>🔴 <b>零值必須是「不解除」的答案</b>:查詢本身不成立時維持封鎖。</remarks>
    private enum AddonPresence
    {
        /// <summary>查詢不成立(名稱空、位址 0、同名實例超過掃描上限)。<b>這是零值</b>。</summary>
        Unknown = 0,

        /// <summary>掃完同名清單都沒有這個位址＝那扇窗已經收乾淨。</summary>
        NotListed = 1,

        Visible = 2,

        Hidden = 3,
    }

    /// <summary>掃該名稱的全部索引(1..<see cref="MaxAddonIndex"/>,掃到第一個空的停),只比位址。</summary>
    /// <remarks>
    /// 🔴 解參考的是 <c>GetAddonByName</c> 這一幀交回來的指標,而且只在 <paramref name="readVisibility"/> 時才讀;
    /// 被記下的位址永遠只做等值比較。不讀可見性時,找到一律回 <see cref="AddonPresence.Visible"/>。
    /// </remarks>
    private static AddonPresence LookUp(string addonName, nint address, bool readVisibility)
    {
        if (string.IsNullOrEmpty(addonName) || address == 0) return AddonPresence.Unknown;

        for (var i = 1; i <= MaxAddonIndex; i++)
        {
            var unit = Svc.GameGui.GetAddonByName<AtkUnitBase>(addonName, i);
            if (unit == null) return AddonPresence.NotListed;
            if ((nint)unit != address) continue;

            if (!readVisibility) return AddonPresence.Visible;
            return unit->IsVisible ? AddonPresence.Visible : AddonPresence.Hidden;
        }

        return AddonPresence.Unknown;
    }

    private static bool IsListed(PressSlot slot) =>
        LookUp(slot.AddonName, slot.Address, false) is AddonPresence.Visible or AddonPresence.Hidden;

    /// <summary>找不到就回 <see langword="false"/>(＝不准送):這支只給放行端用,查歪一律表現成「這一輪不送」。</summary>
    private static bool PersistentAddonVisible(string addonName, nint address)
        => LookUp(addonName, address, readVisibility: true) == AddonPresence.Visible;

    /// <summary>
    /// 每幀的解除路徑:①登記時找得到、現在從同名清單消失 ⇒ 解除(含 PreFinalize 鎖);
    /// ②常駐窗連續隱藏 <see cref="HiddenReleaseFrames"/> 幀 ⇒ 解除。
    /// </summary>
    /// <remarks>
    /// ⚠️ 判準刻意<b>不</b>用「視窗看起來還 ready 嗎」或「文字還對不對」(常駐窗例外):關閉中的那幾幀三關全過、
    /// 文字也可能讀壞(U+FFFD),拿那些當「窗不見了」會在最危險的那幾幀把封鎖解除掉,等於沒有這道防線。
    /// </remarks>
    private static void PollRecords()
    {
        if (Pressed.Count == 0) return;

        ReleaseBuf.Clear();
        foreach (var (slot, records) in Pressed)
        {
            var persistent = PersistentAddons.Contains(slot.AddonName);
            var presence = LookUp(slot.AddonName, slot.Address, persistent);
            var present = presence != AddonPresence.NotListed;
            if (!present)
            {
                if (records.Listed) ReleaseBuf.Add(slot);
                else records.HiddenFrames = 0;
                continue;
            }

            if (!persistent || records.Finalizing) continue;
            if (presence != AddonPresence.Hidden)
            {
                records.HiddenFrames = 0;
                continue;
            }

            if (++records.HiddenFrames < HiddenReleaseFrames) continue;

            ReleaseBuf.Add(slot);
            if (HiddenReleaseReported.Add(slot.AddonName))
                Svc.Log.Information($"{Handle} 「{slot.AddonName}」是常駐視窗(隱藏而不銷毀)," +
                                    $"按下記號改由「連續隱藏 {HiddenReleaseFrames} 幀」解除。這一行每個視窗名每次遊戲只寫一次。");
        }

        foreach (var slot in ReleaseBuf) Pressed.Remove(slot);
        ReleaseBuf.Clear();
    }

    /// <summary>回收「已經不可能再擋住任何人」的紀錄(不改變任何一次「按／不按」的判定);上鎖的不動。</summary>
    private static void SweepColdRecords()
    {
        if (Pressed.Count == 0) return;

        ReleaseBuf.Clear();
        foreach (var (slot, records) in Pressed)
        {
            if (records.Finalizing || frameCount - records.LastFrame < SweepMinAgeFrames) continue;
            ReleaseBuf.Add(slot);
        }

        foreach (var slot in ReleaseBuf) Pressed.Remove(slot);
        ReleaseBuf.Clear();
    }

    /// <summary>
    /// 第一次守護某個 addon 名稱時掛上監聽器:PostSetup 清該位址「不是這一幀才登記的」紀錄,PreFinalize 把該位址上鎖。
    /// </summary>
    /// <remarks>
    /// 掛上去之後就不再拆(只在 <see cref="ForceTeardown"/> 拆):動態掛／拆比較容易留下懸空的監聽器。
    /// 🔴🔴 <b>只動位址對得上的那一筆,絕對不可以整個名字一起清</b>:#A 關閉中的同一幀第二扇同名窗 #B 被建立時,
    /// 不能連坐清掉 #A 的紀錄(下一幀解到 #A 的按下點就會查無紀錄而放行)。
    /// 🔴 PostSetup 的同幀豁免:監聽器之間的呼叫順序不保證,在 PostSetup 處理常式裡按下的紀錄不能被當場清掉。
    /// </remarks>
    private static void EnsureWatching(string addonName)
    {
        if (Watchers.ContainsKey(addonName)) return;

        IAddonLifecycle.AddonEventDelegate handler = (type, args) =>
        {
            var address = args.Addon.Address;
            if (address == 0) return;
            var slot = new PressSlot(addonName, address);

            if (type == AddonEvent.PreFinalize)
            {
                LockFinalizing(slot);
                return;
            }

            if (!Pressed.TryGetValue(slot, out var records)) return;

            records.Finalizing = false;
            StaleKeysBuf.Clear();
            foreach (var (key, rec) in records.ByKey)
            {
                if (rec.Frame == frameCount) continue;
                StaleKeysBuf.Add(key);
            }

            foreach (var key in StaleKeysBuf) records.ByKey.Remove(key);
            StaleKeysBuf.Clear();
            if (records.ByKey.Count == 0) Pressed.Remove(slot);
        };

        Watchers[addonName] = handler;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, addonName, handler);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, addonName, handler);
    }

    /// <summary>該位址進入銷毀:鎖到它從清單消失或同位址重建。守護過的窗名即使沒按過也鎖。</summary>
    private static void LockFinalizing(PressSlot slot)
    {
        if (Pressed.TryGetValue(slot, out var records))
        {
            records.Finalizing = true;
            return;
        }

        Pressed[slot] = new SlotRecords { Listed = true, Finalizing = true, LastFrame = frameCount };
    }
}
