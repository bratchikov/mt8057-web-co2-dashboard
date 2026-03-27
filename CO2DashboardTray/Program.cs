using System.Diagnostics;

namespace CO2DashboardTray;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Установить PerMonitorV2 для корректной работы DPI
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        
        // Настройка трейс-логирования
        Trace.Listeners.Add(new TextWriterTraceListener("trace.log"));
        Trace.AutoFlush = true;
        Trace.WriteLine("=== Application Started ===");
        
        // Запуск tray-приложения
        var trayApp = new TrayApp();
        trayApp.Initialize();
        
        // Приложение работает в фоне, без GUI форм
        // Application.Run() не вызывается, приложение сворачивается в трей
        Trace.WriteLine("Starting Application.Run()");
        Application.Run();
        Trace.WriteLine("Application.Run() finished");
    }
}
