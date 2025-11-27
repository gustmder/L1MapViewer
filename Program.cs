using L1FlyMapViewer;
using L1MapViewer;
using System.Text;

namespace L1MapViewerCore;

static class Program
{
    [STAThread]
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        Application.Run(new MapForm());
    }
}
