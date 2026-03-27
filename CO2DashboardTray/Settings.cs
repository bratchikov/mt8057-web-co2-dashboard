namespace CO2DashboardTray;

/// <summary>
/// Модель настроек приложения
/// </summary>
public class Settings
{
    /// <summary>
    /// URL сервера Go backend
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:8072";

    /// <summary>
    /// Интервал опроса сервера в секундах
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Порог нормального значения CO2 (ppm)
    /// </summary>
    public int NormalThreshold { get; set; } = 1000;

    /// <summary>
    /// Порог предупреждения CO2 (ppm)
    /// </summary>
    public int WarningThreshold { get; set; } = 1500;
}
