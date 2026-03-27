using System.Text.Json;

namespace CO2DashboardTray;

/// <summary>
/// Менедер для загрузки и сохранения настроек
/// </summary>
public static class SettingsManager
{
    private const string SettingsFileName = "appsettings.json";

    /// <summary>
    /// Загрузить настройки из файла appsettings.json
    /// </summary>
    /// <returns>Объект Settings с загруженными значениями</returns>
    public static Settings LoadSettings()
    {
        string filePath = GetSettingsFilePath();
        
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                Settings? settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке настроек: {ex.Message}");
            }
        }
        
        // Если файл не найден или ошибка, вернуть настройки по умолчанию
        return new Settings();
    }

    /// <summary>
    /// Сохранить настройки в файл appsettings.json
    /// </summary>
    /// <param name="settings">Объект Settings для сохранения</param>
    public static void SaveSettings(Settings settings)
    {
        string filePath = GetSettingsFilePath();
        
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении настроек: {ex.Message}");
        }
    }

    /// <summary>
    /// Получить путь к файлу настроек
    /// </summary>
    /// <returns>Полный путь к appsettings.json</returns>
    public static string GetSettingsFilePath()
    {
        // Путь к файлу настроек - рядом с исполняемым файлом
        string exePath = AppContext.BaseDirectory;
        return Path.Combine(exePath, SettingsFileName);
    }
}
