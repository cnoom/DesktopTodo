using System;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace DesktopTodo.Services;

public class SettingsService : ISettingsService
{
    private const string SettingsFileName = "appsettings.json";
    private readonly string _settingsPath;

    public double BackgroundOpacity { get; set; } = 0.8;
    public Color BackgroundColor { get; set; } = Color.FromRgb(240, 240, 240);
    public double FontSize { get; set; } = 12.0;
    public Color TaskFontColor { get; set; } = Colors.White;

    // 窗口位置和尺寸，使用 NaN 表示"未保存过"
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = double.NaN;
    public double WindowHeight { get; set; } = double.NaN;

    // 迷你模式图标位置，NaN 表示"未保存过"（使用默认位置）
    public double MiniModeLeft { get; set; } = double.NaN;
    public double MiniModeTop { get; set; } = double.NaN;

    public SettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopTodo");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, SettingsFileName);
        MigrateFromLegacyPath();
        Load();
    }

    /// <summary>
    /// 从旧路径（程序目录）迁移设置文件到新路径（AppData）
    /// </summary>
    private void MigrateFromLegacyPath()
    {
        if (File.Exists(_settingsPath)) return;

        var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
        if (File.Exists(legacyPath))
        {
            try
            {
                File.Copy(legacyPath, _settingsPath, overwrite: false);
            }
            catch
            {
                // 迁移失败不影响启动
            }
        }
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("BackgroundOpacity", out var op))
                BackgroundOpacity = op.GetDouble();
            if (root.TryGetProperty("BackgroundColor", out var bc))
            {
                var color = (Color)ColorConverter.ConvertFromString(bc.GetString()!);
                BackgroundColor = color;
            }
            if (root.TryGetProperty("FontSize", out var fs))
                FontSize = fs.GetDouble();
            if (root.TryGetProperty("TaskFontColor", out var tfc))
            {
                var color = (Color)ColorConverter.ConvertFromString(tfc.GetString()!);
                TaskFontColor = color;
            }
            if (root.TryGetProperty("WindowLeft", out var wl))
                WindowLeft = wl.GetDouble();
            if (root.TryGetProperty("WindowTop", out var wt))
                WindowTop = wt.GetDouble();
            if (root.TryGetProperty("WindowWidth", out var ww))
                WindowWidth = ww.GetDouble();
            if (root.TryGetProperty("WindowHeight", out var wh))
                WindowHeight = wh.GetDouble();
            if (root.TryGetProperty("MiniModeLeft", out var mml) && mml.ValueKind != JsonValueKind.Null)
                MiniModeLeft = mml.GetDouble();
            if (root.TryGetProperty("MiniModeTop", out var mmt) && mmt.ValueKind != JsonValueKind.Null)
                MiniModeTop = mmt.GetDouble();
        }
        catch { }
    }

    public void Save()
    {
        var obj = new
        {
            BackgroundOpacity,
            BackgroundColor = BackgroundColor.ToString(),
            FontSize,
            TaskFontColor = TaskFontColor.ToString(),
            WindowLeft = ToNullable(WindowLeft),
            WindowTop = ToNullable(WindowTop),
            WindowWidth = ToNullable(WindowWidth),
            WindowHeight = ToNullable(WindowHeight),
            MiniModeLeft = ToNullable(MiniModeLeft),
            MiniModeTop = ToNullable(MiniModeTop)
        };
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    /// <summary>
    /// NaN 转为 null（避免 JSON 中出现 "NaN" 字符串）
    /// </summary>
    private static double? ToNullable(double value) => double.IsNaN(value) ? null : value;
}