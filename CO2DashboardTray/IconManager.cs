using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CO2DashboardTray;

/// <summary>
/// Менеджер для генерации иконок в зависимости от состояния CO2
/// </summary>
public class IconManager
{
    /// <summary>
    /// Создать иконку с текущим состоянием
    /// </summary>
    /// <param name="currentCo2">Текущее значение CO2</param>
    /// <param name="previousCo2">Предыдущее значение CO2</param>
    /// <returns>Иконка с цветом и направлением стрелки</returns>
    public Icon CreateIcon(int currentCo2, int previousCo2)
    {
        // Определить цвет на основе порогов
        Color color = GetColorByCo2Level(currentCo2);
        
        // Определить направление стрелки
        string arrow = GetArrowDirection(currentCo2, previousCo2);
        
        // Получить DPI масштаб
        float dpiScale = GetDpiScale();
        
        // Рассчитать размер иконки
        int size = CalculateIconSize(dpiScale);
        
        // Создать Bitmap
        Bitmap bitmap = new Bitmap(size, size);
        using Graphics graphics = Graphics.FromImage(bitmap);
        
        // Настроить качество рендеринга
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        
        // Очистить фон (прозрачный)
        graphics.Clear(Color.Transparent);
        
        // Нарисовать стрелку
        DrawArrow(graphics, size, color, arrow);
        
        // Сохранить во временный файл и создать Icon
        string tempPath = Path.Combine(Path.GetTempPath(), $"co2_tray_{size}.ico");
        try
        {
            SaveBitmapAsIcon(bitmap, tempPath);
            return new Icon(tempPath);
        }
        finally
        {
            // Удалить временный файл
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Создать серую иконку (кружок без стрелки) для отключенного состояния
    /// </summary>
    /// <returns>Серая иконка кружка</returns>
    public Icon CreateDisconnectedIcon()
    {
        // Получить DPI масштаб
        float dpiScale = GetDpiScale();
        
        // Рассчитать размер иконки
        int size = CalculateIconSize(dpiScale);
        
        // Создать Bitmap
        Bitmap bitmap = new Bitmap(size, size);
        using Graphics graphics = Graphics.FromImage(bitmap);
        
        // Настроить качество рендеринга
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        
        // Очистить фон (прозрачный)
        graphics.Clear(Color.Transparent);
        
        // Нарисовать кружок
        int radius = size / 3;
        int centerX = size / 2;
        int centerY = size / 2;
        
        using SolidBrush brush = new SolidBrush(Color.Gray);
        graphics.FillEllipse(brush, centerX - radius, centerY - radius, radius * 2, radius * 2);
        
        // Сохранить во временный файл и создать Icon
        string tempPath = Path.Combine(Path.GetTempPath(), $"co2_disconnected_{size}.ico");
        try
        {
            SaveBitmapAsIcon(bitmap, tempPath);
            return new Icon(tempPath);
        }
        finally
        {
            // Удалить временный файл
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Сохранить Bitmap как ICO файл
    /// </summary>
    private void SaveBitmapAsIcon(Bitmap bitmap, string filePath)
    {
        // Используем более простой подход - создаем иконку напрямую из Bitmap
        // Windows Forms Icon класс не поддерживает PNG-вложенные ICO файлы
        // Поэтому используем GetHicon() для создания иконки напрямую
        
        using Icon icon = Icon.FromHandle(bitmap.GetHicon());
        using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        icon.Save(fs);
    }

    /// <summary>
    /// Определить направление стрелки
    /// </summary>
    /// <param name="current">Текущее значение CO2</param>
    /// <param name="previous">Предыдущее значение CO2</param>
    /// <returns>Стрелка: ↗ (рост), → (стабильно), ↘ (падение)</returns>
    public string GetArrowDirection(int current, int previous)
    {
        if (current > previous)
        {
            return "↗"; // Рост
        }
        else if (current == previous)
        {
            return "→"; // Стабильно
        }
        else
        {
            return "↘"; // Падение
        }
    }

    /// <summary>
    /// Получить цвет на основе уровня CO2
    /// </summary>
    /// <param name="co2">Значение CO2</param>
    /// <returns>Цвет: Green, Orange, Red</returns>
    private Color GetColorByCo2Level(int co2)
    {
        // Получить настройки
        Settings settings = SettingsManager.LoadSettings();
        
        if (co2 <= settings.NormalThreshold)
        {
            return Color.FromArgb(255, 0, 128, 0); // Зеленый
        }
        else if (co2 <= settings.WarningThreshold)
        {
            return Color.FromArgb(255, 255, 165, 0); // Оранжевый
        }
        else
        {
            return Color.FromArgb(255, 255, 0, 0); // Красный
        }
    }

    /// <summary>
    /// Нарисовать стрелку на графике
    /// </summary>
    /// <param name="graphics">Графический контекст</param>
    /// <param name="size">Размер иконки</param>
    /// <param name="color">Цвет стрелки</param>
    /// <param name="arrow">Направление стрелки</param>
    private void DrawArrow(Graphics graphics, int size, Color color, string arrow)
    {
        int fontSize = size / 2;
        int x = size / 2 - fontSize / 2;
        int y = size / 2 + fontSize / 4;
        
        using Font font = new Font("Segoe UI", fontSize, FontStyle.Bold);
        using SolidBrush brush = new SolidBrush(color);
        
        graphics.DrawString(arrow, font, brush, x, y);
    }

    /// <summary>
    /// Получить DPI масштаб
    /// </summary>
    /// <returns>Масштаб DPI</returns>
    private float GetDpiScale()
    {
        using Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);
        return graphics.DpiX / 96f; // 96 DPI - это 100% масштаб
    }

    /// <summary>
    /// Рассчитать размер иконки на основе DPI
    /// </summary>
    /// <param name="dpiScale">Масштаб DPI</param>
    /// <returns>Размер иконки (16, 20, 24, 32, 40, 48)</returns>
    private int CalculateIconSize(float dpiScale)
    {
        float baseSize = 16 * dpiScale;
        
        // Округлить до ближайшего стандартного размера
        if (baseSize <= 16) return 16;
        if (baseSize <= 20) return 20;
        if (baseSize <= 24) return 24;
        if (baseSize <= 32) return 32;
        if (baseSize <= 40) return 40;
        return 48;
    }
}
