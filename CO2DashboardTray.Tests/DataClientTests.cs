using CO2DashboardTray;

namespace CO2DashboardTray.Tests;

public class DataClientTests
{
    [Fact]
    public void TestJsonDeserialization()
    {
        // Пример JSON от сервера
        string json = "[{\"timestamp\":\"2026-03-27T14:19:26Z\",\"temperature\":27.725006,\"co2\":1274}]";
        
        // Десериализуем JSON
        Reading[]? readings = System.Text.Json.JsonSerializer.Deserialize<Reading[]>(json);
        
        // Проверяем, что десериализация прошла успешно
        Assert.NotNull(readings);
        Assert.Single(readings);
        
        // Проверяем значения
        var reading = readings[0];
        Assert.Equal(1274, reading.Co2);
        Assert.Equal(27.725006, reading.Temperature, 6);
    }
    
    [Fact]
    public async Task TestDataClientFetchAsync()
    {
        // Тестируем с реальным сервером
        var client = new DataClient("http://localhost:8072");
        
        var reading = await client.GetLatestReadingAsync();
        
        Assert.NotNull(reading);
        Assert.True(reading.Co2 > 0, $"Ожидалось, что CO2 > 0, но получено {reading.Co2}");
        Assert.True(reading.Temperature > 0, $"Ожидалось, что Temperature > 0, но получено {reading.Temperature}");
    }
}
