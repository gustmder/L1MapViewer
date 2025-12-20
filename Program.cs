using System.Text;
using L1FlyMapViewer;
using L1MapViewer;
using L1MapViewer.CLI;
using System.Text;

namespace L1MapViewerCore;

static class Program
{
    // 效能 Log 開關（供 MapForm 讀取）
    public static bool PerfLogEnabled { get; private set; } = false;

    [STAThread]
    static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 檢查是否啟用效能 Log
        var argsList = args.ToList();
        if (argsList.Contains("--perf-log"))
        {
            PerfLogEnabled = true;
            argsList.Remove("--perf-log");
            args = argsList.ToArray();
            Console.WriteLine("[PERF-LOG] Enabled");
        }

        // 檢查是否為 CLI 模式
        if (args.Length > 0 && args[0].ToLower() == "-cli")
        {
            return CliHandler.Execute(args);
        }

        // GUI 模式
        ApplicationConfiguration.Initialize();
        Application.Run(new MapForm());
        return 0;
    }
}
