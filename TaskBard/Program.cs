namespace TaskBard;

static class Program
{
    private const string MutexName = "Global\\TaskBard_SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running
            return;
        }

        ApplicationConfiguration.Initialize();
        var config = AppConfig.Load();
        Application.Run(new TrayApplicationContext(config));
    }
}