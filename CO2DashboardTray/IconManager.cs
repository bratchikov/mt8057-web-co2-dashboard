using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CO2DashboardTray;

public enum TrendDirection { Rising, Stable, Falling }

public class IconManager
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
    
    // Цвета для разных уровней CO2
    private static readonly Color GreenColor = Color.FromArgb(46, 204, 113);
    private static readonly Color OrangeColor = Color.FromArgb(241, 196, 15);
    private static readonly Color RedColor = Color.FromArgb(231, 76, 60);
    
    public Icon CreateIcon(int currentCo2, int previousCo2)
    {
        Color color = GetColorByCo2Level(currentCo2);
        TrendDirection direction = GetTrendDirection(currentCo2, previousCo2);
        int size = CalculateIconSize(GetDpiScale());
        
        // КРИТИЧНО: Использовать Format32bppArgb для прозрачности
        using Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96, 96);
        
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            // Для маленьких размеров (16x16) отключаем AntiAlias - он не нужен и замедляет
            if (size > 16)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
            }
            
            // Прозрачный фон
            g.Clear(Color.Transparent);
            
            // Рисуем треугольник или круг программно
            DrawTrendIcon(g, size, color, direction);
        }
        
        // Создаём иконку без временного файла
        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon); // КРИТИЧНО: Освободить GDI-ресурс
        }
    }
    
    public Icon CreateDisconnectedIcon()
    {
        int size = CalculateIconSize(GetDpiScale());
        
        using Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96, 96);
        
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            
            int radius = size / 3;
            int cx = size / 2;
            using SolidBrush brush = new SolidBrush(Color.Gray);
            g.FillEllipse(brush, cx - radius, cx - radius, radius * 2, radius * 2);
        }
        
        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
    
    private void DrawTrendIcon(Graphics g, int size, Color color, TrendDirection direction)
    {
        float pad = size * 0.2f;
        float maxDim = size - pad * 2;
        
        using SolidBrush brush = new SolidBrush(color);
        
        switch (direction)
        {
            case TrendDirection.Rising:
                // Треугольник остриём вверх
                float risingPad = size * 0.25f;
                float risingSize = size - risingPad * 2;
                PointF[] risingTriangle = new PointF[]
                {
                    new(risingPad + risingSize / 2, risingPad),      // верхняя вершина
                    new(risingPad, risingPad + risingSize),         // левый нижний
                    new(risingPad + risingSize, risingPad + risingSize) // правый нижний
                };
                g.FillPolygon(brush, risingTriangle);
                break;
                
            case TrendDirection.Stable:
                // Круг
                float circleSize = size * 0.5f;
                float circleX = (size - circleSize) / 2;
                float circleY = (size - circleSize) / 2;
                g.FillEllipse(brush, circleX, circleY, circleSize, circleSize);
                break;
                
            case TrendDirection.Falling:
                // Треугольник остриём вниз
                float fallingPad = size * 0.25f;
                float fallingSize = size - fallingPad * 2;
                PointF[] fallingTriangle = new PointF[]
                {
                    new(fallingPad, fallingPad),                     // левый верхний
                    new(fallingPad + fallingSize, fallingPad),       // правый верхний
                    new(fallingPad + fallingSize / 2, fallingPad + fallingSize) // нижняя вершина
                };
                g.FillPolygon(brush, fallingTriangle);
                break;
        }
    }
    
    public TrendDirection GetTrendDirection(int current, int previous)
    {
        if (current > previous) return TrendDirection.Rising;
        if (current < previous) return TrendDirection.Falling;
        return TrendDirection.Stable;
    }
    
    private Color GetColorByCo2Level(int co2)
    {
        Settings settings = SettingsManager.LoadSettings();
        
        if (co2 <= settings.NormalThreshold) return GreenColor;
        if (co2 <= settings.WarningThreshold) return OrangeColor;
        return RedColor;
    }
    
    private float GetDpiScale()
    {
        using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96f;
    }
    
    private int CalculateIconSize(float dpiScale)
    {
        // Windows 11 использует следующие размеры для tray icons:
        // 100% → 16px, 125% → 20px, 150% → 24px, 175% → 28px, 200% → 32px
        return dpiScale switch
        {
            <= 1.00f => 16,
            <= 1.25f => 20,
            <= 1.50f => 24,
            <= 1.75f => 28,
            <= 2.00f => 32,
            <= 2.50f => 40,
            _ => 48
        };
    }
}
