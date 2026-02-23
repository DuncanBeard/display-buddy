namespace TaskbarAlignmentTool;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var config = AppConfig.Load();
        Application.Run(new TrayApplicationContext(config));
    }
}