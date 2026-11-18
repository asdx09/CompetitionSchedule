using System.Windows.Forms;

namespace ScheduleLogic.TestRunner;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
#if !MONO_CHECK
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
