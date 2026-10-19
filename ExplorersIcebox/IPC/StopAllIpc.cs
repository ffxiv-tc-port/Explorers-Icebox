using System.Threading;
using Dalamud.Plugin.Ipc;
using ExplorersIcebox.Enums;
using ExplorersIcebox.Scheduler;

namespace ExplorersIcebox.IPC;

/// <summary>急停端點 <c>ExplorersIcebox.StopAll</c>（Action&lt;string&gt;，參數是呼叫端署名，寫進記錄）。</summary>
/// <remarks>任何執行緒都可以呼叫、不會擲例外給呼叫端：在 framework 執行緒上當場停；其他執行緒只排隊，下一幀由 <see cref="ProcessPending"/> 停。
/// 停止走與主視窗「停止」按鈕相同的 <see cref="SchedulerMain.DisablePlugin"/>，不改、不存設定。
/// 刻意只提供停止、不提供開始：開始一律要使用者自己按。</remarks>
internal sealed class StopAllIpc : IDisposable
{
    internal const string EndpointName = "ExplorersIcebox.StopAll";

    private readonly ICallGateProvider<string, object> provider;

    // 其他執行緒排進來的停止要求（呼叫端署名）；null＝沒有。
    private string? pending;

    private volatile bool disposed;

    internal StopAllIpc()
    {
        provider = Svc.PluginInterface.GetIpcProvider<string, object>(EndpointName);
        provider.RegisterAction(Request);
    }

    private void Request(string? source)
    {
        if (disposed)
            return;

        var label = string.IsNullOrWhiteSpace(source) ? "?" : source.Trim();
        if (Svc.Framework.IsInFrameworkUpdateThread)
        {
            StopNow(label);
            return;
        }

        Interlocked.Exchange(ref pending, label);
    }

    /// <summary>只在 framework 執行緒呼叫。</summary>
    internal void ProcessPending()
    {
        var label = Interlocked.Exchange(ref pending, null);
        if (label != null && !disposed)
            StopNow(label);
    }

    private static void StopNow(string label)
    {
        try
        {
            var state = SchedulerMain.State;
            var queued = P.taskManager?.NumQueuedTasks ?? 0;

            // 沒在跑時不呼叫 DisablePlugin：閒置時它會順手停掉正在走的 vnavmesh，那時的導航是別人的。
            if (state == IceBoxState.Idle && queued == 0)
            {
                Svc.Log.Information($"[ExplorersIcebox] 收到急停（{label}）：原本就沒在跑，未動作。");
                return;
            }

            SchedulerMain.DisablePlugin();
            Svc.Log.Information($"[ExplorersIcebox] 收到急停（{label}）：已停止無人島自動化（原狀態 {state}、任務佇列 {queued} 項），並停下 vnavmesh 導航（若在走）。");
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[ExplorersIcebox] 急停（{label}）時發生例外");
        }
    }

    public void Dispose()
    {
        disposed = true;
        provider.UnregisterAction();
    }
}
