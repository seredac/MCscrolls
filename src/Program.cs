namespace MCscrolls;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "MCscrolls-SingleInstance", out bool isNew);
        if (!isNew)
            return;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Application.Exit();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Environment.Exit(1);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new App());
    }
}
