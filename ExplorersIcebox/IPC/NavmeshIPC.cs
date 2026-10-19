using ECommons.EzIpcManager;
using ExplorersIcebox.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.IPC;

#nullable disable
public class NavmeshIPC
{
    public const string Name = "vnavmesh";
    // 這裡絕對不能指國際服的外掛庫：那裡的 vnavmesh 內部名與台服版完全相同，
    // 加進去會把 API15 的版本裝進台服環境並撞同一個已安裝鍵。一律指本艦隊的 feed。
    public const string Repo = "https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json";
    // 🔴 SafeWrapper.AnyException：vnavmesh 還沒註冊 IPC 時（開場那兩秒、或它根本沒安裝），
    //    呼叫任何一個端點都會擲 IpcNotReadyError。
    //    ⚠️ 用 AnyException 不用 IPCException：後者只攔 IpcNotReadyError，攔不住 IpcTypeMismatchError。
    public NavmeshIPC() => EzIPC.Init(this, Name, SafeWrapper.AnyException);
    public static bool Installed => Utils.HasPlugin(Name);

    [EzIPC("Nav.%m")] public readonly Func<bool> IsReady;
    [EzIPC("Nav.%m")] public readonly Func<float> BuildProgress;
    [EzIPC("Nav.%m")] public readonly Func<bool> Reload;
    [EzIPC("Nav.%m")] public readonly Func<bool> Rebuild;
    // 🔴 簽章必須與提供端一致：vnavmesh 的 Nav.Pathfind 回 Task<List<Vector3>>，不是 Vector3。
    //    型別對不上時 Dalamud 擲 IpcTypeMismatchError，本檔的 SafeWrapper.AnyException 會吞掉並回 default ⇒ 靜默失效。
    [EzIPC("Nav.%m")] public readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Pathfind;

    [EzIPC("SimpleMove.%m")] public readonly Func<Vector3, bool, bool> PathfindAndMoveTo;
    [EzIPC("SimpleMove.%m")] public readonly Func<bool> PathfindInProgress;

    [EzIPC("Path.%m")] public readonly Action<List<Vector3>, bool> MoveTo;
    [EzIPC("Path.%m")] public readonly Action Stop;
    [EzIPC("Path.%m")] public readonly Action<bool> SetAlignCamera;
    [EzIPC("Path.%m")] public readonly Func<bool> IsRunning;

    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, float, float, Vector3?> NearestPoint;
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, bool, float, Vector3?> PointOnFloor;
}
