using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CO2DashboardTray;

public enum ArrowDirection { Rising, Stable, Falling }

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
        ArrowDirection direction = GetArrowDirection(currentCo2, previousCo2);
        int size = CalculateIconSize(GetDpiScale());
        
        // КРИТИЧНО: Использовать Format32bppArgb для прозрачности
        using Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96, 96);
        
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            // Настройка качества
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            
            // Прозрачный фон
            g.Clear(Color.Transparent);
            
            // Рисуем стрелку программно
            DrawArrow(g, size, color, direction);
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
    
    private void DrawArrow(Graphics g, int size, Color color, ArrowDirection direction)
    {
        float pad = size * 0.15f;
        float w = size - pad * 2;
        float h = size - pad * 2;
        
        PointF[] points = direction switch
        {
            ArrowDirection.Rising => new PointF[]
            {
                new(pad, pad + h * 0.7f),
                new(pad + w * 0.35f, pad + h * 0.7f),
                new(pad + w * 0.35f, pad + h * 0.35f),
                new(pad + w, pad),
                new(pad + w * 0.65f, pad),
                new(pad + w * 0.65f, pad + h * 0.35f),
                new(pad, pad + h * 0.35f),
            },
            ArrowDirection.Stable => new PointF[]
            {
                new(pad, pad + h * 0.35f),
                new(pad + w * 0.5f, pad + h * 0.35f),
                new(pad + w, pad + h * 0.5f),
                new(pad + w * 0.5f, pad + h * 0.65f),
                new(pad, pad + h * 0.65f),
            },
            ArrowDirection.Falling => new PointF[]
            {
                new(pad, pad),
                new(pad + w * 0.35f, pad),
                new(pad + w * 0.35f, pad + h * 0.35f),
                new(pad + w, pad + h * 0.7f),
                new(pad + w * 0.65f, pad + h * 0.7f),
                new(pad + w * 0.65f, pad + h * 0.35f),
                new(pad, pad + h * 0.35f),
            },
            _ => Array.Empty<PointF>()
        };
        
        using SolidBrush brush = new SolidBrush(color);
        g.FillPolygon(brush, points);
        
        // Тонкая обводка для лучшей видимости
        using Pen pen = new Pen(Color.FromArgb(64, 0, 0, 0), size / 24f);
        g.DrawPolygon(pen, points);
    }
    
    public ArrowDirection GetArrowDirection(int current, int previous)
    {
        if (current > previous) return ArrowDirection.Rising;
        if (current < previous) return ArrowDirection.Falling;
        return ArrowDirection.Stable;
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
