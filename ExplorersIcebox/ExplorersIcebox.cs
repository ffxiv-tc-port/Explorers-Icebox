using ECommons.Automation.NeoTaskManager;
using ECommons.Logging;
using ExplorersIcebox.Config;
using ExplorersIcebox.IPC;
using ExplorersIcebox.Scheduler;
using ExplorersIcebox.Ui;
using ExplorersIcebox.Ui.MainWindow;
using ExplorersIcebox.Util;
using Pictomancy;

namespace ExplorersIcebox;

public sealed class ExplorersIcebox : IDalamudPlugin
{
    public string Name => "ExplorersIcebox";

    private static GeneralConfig? Config;
    private static GatherRoutes? GatherRoutesConfig;

    // Lazy-loaded static config accessor
    public static GeneralConfig C => Config ??= LoadConfig<GeneralConfig>();
    public static GatherRoutes G => GatherRoutesConfig ??= LoadConfig<GatherRoutes>();

    public static GatherRoutes EmbedRoutes => embeddedRoutes ??= LoadEmbeddedConfig<GatherRoutes>("ExplorersIcebox.Routes.CustomRoutes.yaml");
    private static GatherRoutes? embeddedRoutes;

    private static T LoadConfig<T>() where T : IYamlConfig, new()
    {
        var path = typeof(T).GetProperty("ConfigPath")!.GetValue(null)!.ToString()!;
        var config = YamlConfig.Load<T>(path);

        if (config == null)
        {
            PluginLog.Warning($"[{typeof(T).Name}] Config was null. Creating new default.");
            config = new T();
            YamlConfig.Save(config, path);
        }

        PluginLog.Information($"[{typeof(T).Name}] Loaded from {path}");
        return config;
    }

    private static T LoadEmbeddedConfig<T>(string resourceName) where T : IYamlConfig, new()
    {
        var config = YamlConfig.LoadFromResource<T>(resourceName);

        if (config == null)
        {
            PluginLog.Warning($"[{typeof(T).Name}] Embedded config was null. Returning new default.");
            config = new T();
        }

        PluginLog.Information($"[{typeof(T).Name}] Loaded from embedded resource: {resourceName}");
        return config;
    }



    internal static ExplorersIcebox P = null!;

    // Window Systems for the plugin (nice and neatly)
    internal WindowSystem windowSystem;
    internal MainWindow mainWindow;
    internal DebugWindow debugWindow;

    // Taskmanager from Ecommons (bless)
    internal TaskManager taskManager;

    // Internal IPC's from other plugins
    internal LifestreamIPC lifestream;
    internal NavmeshIPC navmesh;
    private StopAllIpc? stopAllIpc;

    public ExplorersIcebox(IDalamudPluginInterface pi)
    {
        P = this;
        ECommonsMain.Init(pi, P, ECommons.Module.DalamudReflector, ECommons.Module.ObjectFunctions, Module.SplatoonAPI);
        Util.File_Migration.UpdateItemConfig();

        PictoService.Initialize(pi);

        //IPC's that are used
        taskManager = new();
        lifestream = new();
        navmesh = new();

        // all the windows
        windowSystem = new();
        mainWindow = new();
        debugWindow = new();

        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += () =>
        {
            mainWindow.IsOpen = true;
        };
        /* 
        Svc.PluginInterface.UiBuilder.OpenConfigUi += () =>
        {
            
        }; 
        */
        EzCmd.Add("/explorersicebox", OnCommand, """
            Open plugin interface
            /icebox - alias for /explorersicebox
            /explorersicebox s|settings - Opens the workshop menu
            """);
        EzCmd.Add("/icebox", OnCommand);

        taskManager = new(new(abortOnTimeout: true, timeLimitMS: 20000, showDebug: true));

        Init();

        // 🔴 一定要在 Svc.Framework.Update += Tick 之前：同一個外掛內部的 Framework.Update 是
        // 單一 try/catch 包住整條多播委派，排在前面的處理常式擲例外時，後面所有的那個 tick 都不會被呼叫。
        // 守衛的逃生口靠這個時鐘計時，時鐘停住＝逃生口永不到期＝任務卡到 20 秒逾時清整條佇列。
        AddonPressGuard.EnsureClock();
        Svc.Framework.Update += Tick;
        stopAllIpc = new();
    }

    public void Init()
    {
        
    }

    private void Tick(object _)
    {
        stopAllIpc?.ProcessPending();
        if (Svc.Objects.LocalPlayer != null)
        {
            SchedulerMain.Tick();
        }
        // 🔴 沒有 LocalPlayer（登入畫面／過場／換區）時「只有真的有東西要停」才去停：無條件呼叫 DisablePlugin()
        //    等於每一幀重跑整段停止流程，開場 vnavmesh 的 IPC 還沒註冊時每次都擲 IpcNotReadyError。
        else if (SchedulerMain.State != Enums.IceBoxState.Idle || taskManager.NumQueuedTasks > 0)
        {
            SchedulerMain.DisablePlugin();
        }
    }

    public void Dispose()
    {
        Safe(() => stopAllIpc?.Dispose());
        Safe(() => Svc.Framework.Update -= Tick);
        Safe(AddonPressGuard.ForceTeardown);
        Safe(() => Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw);
        ECommonsMain.Dispose();
        PictoService.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args.EqualsIgnoreCaseAny("d", "debug"))
        {
            debugWindow.IsOpen = !debugWindow.IsOpen;
        }
        else if (args.EqualsIgnoreCaseAny("s", "settings"))
        {
            
        }
        else
        {
            mainWindow.IsOpen = !mainWindow.IsOpen;
        }
    }
}
