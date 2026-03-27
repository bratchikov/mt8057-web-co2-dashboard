namespace CO2DashboardTray;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        
        // Запуск tray-приложения
        var trayApp = new TrayApp();
        trayApp.Initialize();
        
        // Приложение работает в фоне, без GUI форм
        // Application.Run() не вызывается, приложение сворачивается в трей
        Application.Run();
    }
}
