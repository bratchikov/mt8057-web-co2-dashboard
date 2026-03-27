using System.Text;
using System.Text.Json;

namespace CO2DashboardTray;

/// <summary>
/// Клиент для взаимодействия с Go backend API
/// </summary>
public class DataClient
{
    private readonly HttpClient httpClient;
    private readonly string serverUrl;

    /// <summary>
    /// Конструктор DataClient
    /// </summary>
    /// <param name="serverUrl">URL сервера Go backend</param>
    public DataClient(string serverUrl)
    {
        this.serverUrl = serverUrl.TrimEnd('/');
        httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10); // Таймаут 10 секунд
    }

    /// <summary>
    /// Получить последнее значение CO2 и температуры
    /// </summary>
    /// <returns>Объект Reading с данными или null при ошибке</returns>
    public async Task<Reading?> GetLatestReadingAsync()
    {
        try
        {
            string url = $"{serverUrl}/api/data/latest?count=1";
            HttpResponseMessage response = await httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                Reading[]? readings = JsonSerializer.Deserialize<Reading[]>(json);
                
                if (readings != null && readings.Length > 0)
                {
                    return readings[0];
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при получении данных: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Проверить доступность сервера
    /// </summary>
    /// <returns>true если сервер доступен, иначе false</returns>
    public async Task<bool> IsServerAvailableAsync()
    {
        try
        {
            string url = $"{serverUrl}/api/data/latest?count=1";
            HttpResponseMessage response = await httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Модель данных считывания
/// </summary>
public class Reading
{
    /// <summary>
    /// Время измерения
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Температура (°C)
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Уровень CO2 (ppm)
    /// </summary>
    public int Co2 { get; set; }
}
