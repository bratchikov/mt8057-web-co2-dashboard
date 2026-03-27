using System.Diagnostics;
using System.Drawing;
using System.Timers;

namespace CO2DashboardTray;

/// <summary>
/// Основной класс tray-приложения
/// </summary>
public class TrayApp : IDisposable
{
    private NotifyIcon? notifyIcon;
    private System.Threading.Timer? timer;
    private ContextMenuStrip? contextMenu;
    private int previousCo2Value;
    
    private Settings? settings;
    private DataClient? dataClient;
    private IconManager? iconManager;
    
    // Для обработки DPI-изменений
    private Form? _hiddenForm;

    /// <summary>
    /// Инициализация приложения
    /// </summary>
    public void Initialize()
    {
        // Загрузка настроек
        LoadSettings();
        
        // Инициализация компонентов
        dataClient = new DataClient(settings!.ServerUrl);
        iconManager = new IconManager();
        
        // Инициализация previousCo2Value значением NormalThreshold
        previousCo2Value = settings!.NormalThreshold;
        
        // Создать скрытую форму для обработки DPI-событий
        CreateHiddenForm();
        
        // Создание NotifyIcon
        CreateNotifyIcon();
        
        // Создание таймера
        CreateTimer();
        
        // Обновление иконки при запуске
        try
        {
            UpdateTrayIcon();
            Trace.WriteLine("Tray icon initialized successfully");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error initializing tray icon: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Создать скрытую форму для обработки DPI-событий
    /// </summary>
    private void CreateHiddenForm()
    {
        _hiddenForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            WindowState = FormWindowState.Minimized,
            Opacity = 0
        };
        _hiddenForm.DpiChanged += OnDpiChanged;
        _hiddenForm.Show();
        _hiddenForm.Hide();
    }
    
    /// <summary>
    /// Обработчик изменения DPI
    /// </summary>
    private void OnDpiChanged(object? sender, DpiChangedEventArgs e)
    {
        if (notifyIcon != null && iconManager != null)
        {
            // Перегенерировать иконку при изменении DPI
            UpdateTrayIcon();
            Trace.WriteLine("DPI changed");
        }
    }

    /// <summary>
    /// Загрузить настройки
    /// </summary>
    public void LoadSettings()
    {
        settings = SettingsManager.LoadSettings();
    }

    /// <summary>
    /// Получить данные с сервера
    /// </summary>
    public void FetchData()
    {
        if (dataClient == null) return;
        
        var reading = dataClient.GetLatestReadingAsync().Result;
        if (reading != null)
        {
            UpdateTrayIcon(reading.Co2, reading.Temperature);
            previousCo2Value = reading.Co2;
        }
        else
        {
            // При ошибке подключения показать серую иконку
            UpdateDisconnectedIcon();
        }
    }

    /// <summary>
    /// Обновить иконку в трее
    /// </summary>
    public void UpdateTrayIcon(int? currentCo2 = null, double? temperature = null)
    {
        if (iconManager == null || notifyIcon == null) return;
        
        int co2 = currentCo2 ?? previousCo2Value;
        
        // Если сервер недоступен, показать серую иконку
        if (currentCo2 == null)
        {
            try
            {
                notifyIcon.Icon = iconManager.CreateDisconnectedIcon();
                notifyIcon.Text = "Сервер недоступен";
                Trace.WriteLine("Disconnected icon set");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error setting disconnected icon: {ex.Message}");
            }
            return;
        }
        
        // Создать иконку с текущим состоянием
        try
        {
            notifyIcon.Icon = iconManager.CreateIcon(co2, previousCo2Value);
            // Формат tooltip
            ArrowDirection arrowDir = iconManager.GetArrowDirection(co2, previousCo2Value);
            string arrowText = arrowDir switch
            {
                ArrowDirection.Rising => "↗",
                ArrowDirection.Stable => "→",
                ArrowDirection.Falling => "↘",
                _ => ""
            };
            notifyIcon.Text = $"CO2: {co2} ppm {arrowText} | Температура: {temperature?.ToString("F1")}°C";
            Trace.WriteLine($"Tray icon updated: CO2={co2}, arrow={arrowText}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error creating tray icon: {ex.Message}");
        }
    }

    /// <summary>
    /// Показать иконку отключения
    /// </summary>
    public void UpdateDisconnectedIcon()
    {
        if (iconManager == null || notifyIcon == null) return;
        
        try
        {
            notifyIcon.Icon = iconManager.CreateDisconnectedIcon();
            notifyIcon.Text = "Сервер недоступен";
            Trace.WriteLine("Disconnected icon set");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error setting disconnected icon: {ex.Message}");
        }
    }

    /// <summary>
    /// Создать NotifyIcon
    /// </summary>
    private void CreateNotifyIcon()
    {
        notifyIcon = new NotifyIcon();
        notifyIcon.Visible = true;
        
        // Создать контекстное меню
        contextMenu = new ContextMenuStrip();
        
        // Пункт Settings
        var settingsItem = new ToolStripMenuItem("Настройки");
        settingsItem.Click += SettingsMenuItem_Click;
        contextMenu.Items.Add(settingsItem);
        
        // Пункт Exit
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += ExitMenuItem_Click;
        contextMenu.Items.Add(exitItem);
        
        notifyIcon.ContextMenuStrip = contextMenu;
        
        // Обработка двойного клика
        notifyIcon.MouseDoubleClick += (sender, e) =>
        {
            // При двойном клике можно открыть настройки
            OpenSettingsFile();
        };
    }

    /// <summary>
    /// Создать таймер
    /// </summary>
    private void CreateTimer()
    {
        if (settings == null) return;
        
        timer = new System.Threading.Timer(OnTimerTick, null, 0, settings.PollingIntervalSeconds * 1000);
    }

    /// <summary>
    /// Обработчик таймера
    /// </summary>
    private void OnTimerTick(object? state)
    {
        FetchData();
    }

    /// <summary>
    /// Обработчик выхода
    /// </summary>
    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        Dispose();
        Application.Exit();
    }

    /// <summary>
    /// Обработчик открытия настроек
    /// </summary>
    private void SettingsMenuItem_Click(object? sender, EventArgs e)
    {
        OpenSettingsFile();
    }

    /// <summary>
    /// Открыть файл настроек
    /// </summary>
    private void OpenSettingsFile()
    {
        string filePath = SettingsManager.GetSettingsFilePath();
        if (File.Exists(filePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }

    /// <summary>
    /// Освободить ресурсы
    /// </summary>
    public void Dispose()
    {
        if (notifyIcon != null)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }
        
        timer?.Dispose();
        contextMenu?.Dispose();
        
        // Освободить скрытую форму
        _hiddenForm?.Dispose();
    }
}
