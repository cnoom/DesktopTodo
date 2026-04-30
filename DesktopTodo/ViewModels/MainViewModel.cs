using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopTodo.Interfaces;
using DesktopTodo.Models;
using DesktopTodo.Services;
using DesktopTodo.Helpers;

namespace DesktopTodo.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseService _db;
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private ObservableCollection<TaskItemViewModel> _rootTasks = new();

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private TaskItemViewModel? _selectedTask;

    // ---- 外观 ----
    [ObservableProperty]
    private double _backgroundOpacity;
    [ObservableProperty]
    private Color _backgroundColor;
    [ObservableProperty]
    private SolidColorBrush _windowBackground = null!;
    [ObservableProperty]
    private byte _red;
    [ObservableProperty]
    private byte _green;
    [ObservableProperty]
    private byte _blue;

    // ---- 分类 ----
    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();
    [ObservableProperty]
    private Category? _selectedCategory;

    // ---- 标签 ----
    [ObservableProperty]
    private ObservableCollection<Tag> _allTags = new();
    [ObservableProperty]
    private Tag? _selectedFilterTag;

    // ---- 开机自启 ----
    [ObservableProperty]
    private bool _isAutoStartEnabled;

    // ---- 空状态 ----
    [ObservableProperty]
    private bool _isEmptyState = true;

    // ---- 暗色主题检测 ----
    [ObservableProperty]
    private bool _isDarkTheme;

    // ---- 自适应文字颜色 ----
    [ObservableProperty]
    private SolidColorBrush _adaptiveForeground = null!;
    [ObservableProperty]
    private SolidColorBrush _adaptiveSecondaryForeground = null!;
    [ObservableProperty]
    private SolidColorBrush _adaptiveMutedForeground = null!;
    [ObservableProperty]
    private SolidColorBrush _adaptiveOverlayBrush = null!;
    [ObservableProperty]
    private SolidColorBrush _adaptiveTagFontBrush = null!;
    [ObservableProperty]
    private SolidColorBrush _adaptiveBorderBrush = null!;
    [ObservableProperty]
    private SolidColorBrush _adaptiveGuideLineBrush = null!;

    // ---- 自定义字号 ----
    [ObservableProperty]
    private double _fontSize;

    // ---- 任务字体颜色 ----
    [ObservableProperty]
    private Color _taskFontColor;
    [ObservableProperty]
    private SolidColorBrush _taskFontBrush = null!;
    [ObservableProperty]
    private byte _taskFontRed;
    [ObservableProperty]
    private byte _taskFontGreen;
    [ObservableProperty]
    private byte _taskFontBlue;

    public MainViewModel(IDatabaseService db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;

        // 加载设置
        BackgroundOpacity = _settings.BackgroundOpacity;
        BackgroundColor = _settings.BackgroundColor;
        Red = BackgroundColor.R;
        Green = BackgroundColor.G;
        Blue = BackgroundColor.B;
        IsAutoStartEnabled = AutoStartHelper.IsAutoStartEnabled;
        FontSize = _settings.FontSize;
        TaskFontColor = _settings.TaskFontColor;
        TaskFontRed = TaskFontColor.R;
        TaskFontGreen = TaskFontColor.G;
        TaskFontBlue = TaskFontColor.B;
        UpdateTaskFontBrush();

        UpdateBackgroundBrush();
        UpdateThemeDetection();

        // 初始化数据
        LoadCategories();
        SelectCategoryCommand.Execute(Categories.FirstOrDefault(c => c.Id == 0));
        LoadAllTags();
    }

    // ---- 外观逻辑 ----
    partial void OnBackgroundOpacityChanged(double value)
    {
        UpdateBackgroundBrush();
        _settings.BackgroundOpacity = value;
    }

    partial void OnBackgroundColorChanged(Color value)
    {
        UpdateBackgroundBrush();
        Red = value.R; Green = value.G; Blue = value.B;
        _settings.BackgroundColor = value;
        UpdateThemeDetection();
    }

    partial void OnRedChanged(byte value) => UpdateColorFromRgb();
    partial void OnGreenChanged(byte value) => UpdateColorFromRgb();
    partial void OnBlueChanged(byte value) => UpdateColorFromRgb();

    private void UpdateColorFromRgb() => BackgroundColor = Color.FromRgb(Red, Green, Blue);

    private void UpdateBackgroundBrush()
    {
        byte alpha = (byte)(BackgroundOpacity * 255);
        WindowBackground = new SolidColorBrush(Color.FromArgb(alpha, BackgroundColor.R, BackgroundColor.G, BackgroundColor.B));
    }

    /// <summary>
    /// 根据背景色亮度自动检测是否为暗色主题
    /// 使用 ITU-R BT.601 标准亮度公式：Y = 0.299*R + 0.587*G + 0.114*B
    /// </summary>
    private void UpdateThemeDetection()
    {
        double luminance = 0.299 * BackgroundColor.R + 0.587 * BackgroundColor.G + 0.114 * BackgroundColor.B;
        IsDarkTheme = luminance < 128;
        UpdateAdaptiveColors();
    }

    /// <summary>
    /// 根据主题自动切换所有文字颜色，避免自定义背景撞色
    /// </summary>
    private void UpdateAdaptiveColors()
    {
        if (IsDarkTheme)
        {
            AdaptiveForeground = new SolidColorBrush(Colors.White);
            AdaptiveSecondaryForeground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            AdaptiveMutedForeground = new SolidColorBrush(Color.FromRgb(128, 128, 128));
            AdaptiveOverlayBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
            AdaptiveTagFontBrush = new SolidColorBrush(Colors.Black);
            AdaptiveBorderBrush = new SolidColorBrush(Colors.Gray);
            AdaptiveGuideLineBrush = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
        }
        else
        {
            AdaptiveForeground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            AdaptiveSecondaryForeground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            AdaptiveMutedForeground = new SolidColorBrush(Color.FromRgb(140, 140, 140));
            AdaptiveOverlayBrush = new SolidColorBrush(Color.FromArgb(48, 0, 0, 0));
            AdaptiveTagFontBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            AdaptiveBorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            AdaptiveGuideLineBrush = new SolidColorBrush(Color.FromArgb(37, 0, 0, 0));
        }
    }

    // ---- 开机自启 ----
    partial void OnIsAutoStartEnabledChanged(bool value) => AutoStartHelper.SetAutoStart(value);

    // ---- 字号 ----
    partial void OnFontSizeChanged(double value) => _settings.FontSize = value;

    // ---- 任务字体颜色 ----
    partial void OnTaskFontColorChanged(Color value)
    {
        TaskFontRed = value.R;
        TaskFontGreen = value.G;
        TaskFontBlue = value.B;
        UpdateTaskFontBrush();
        _settings.TaskFontColor = value;
    }

    partial void OnTaskFontRedChanged(byte value) => UpdateTaskFontColorFromRgb();
    partial void OnTaskFontGreenChanged(byte value) => UpdateTaskFontColorFromRgb();
    partial void OnTaskFontBlueChanged(byte value) => UpdateTaskFontColorFromRgb();

    private void UpdateTaskFontColorFromRgb() => TaskFontColor = Color.FromRgb(TaskFontRed, TaskFontGreen, TaskFontBlue);
    private void UpdateTaskFontBrush() => TaskFontBrush = new SolidColorBrush(TaskFontColor);

    // ---- 视图刷新（由分类/标签过滤调用） ----
    public void RefreshCurrentView()
    {
        if (SelectedCategory == null || SelectedCategory.Id == 0)
            LoadTasks();
        else if (SelectedCategory.Id == -1)
            LoadTasksByCategory(null);
        else
            LoadTasksByCategory(SelectedCategory.Id);
    }

    public void LoadTasks() => BuildTreeFromList(_db.GetAllTasks());

    private void LoadTasksByCategory(int? categoryId) =>
        BuildTreeFromList(_db.GetAllTasks().Where(t => t.CategoryId == categoryId).ToList());

    private void BuildTreeFromList(List<TodoTask> tasks)
    {
        var lookup = tasks.ToDictionary(t => t.Id, t => new TaskItemViewModel(t, _db.UpdateTask));
        RootTasks.Clear();
        foreach (var task in tasks)
        {
            if (task.ParentTaskId == null || !lookup.ContainsKey(task.ParentTaskId.Value))
                RootTasks.Add(lookup[task.Id]);
            else
                lookup[task.ParentTaskId.Value].Children.Add(lookup[task.Id]);
        }
        foreach (var vm in lookup.Values) LoadTaskTags(vm);

        // 更新空状态
        IsEmptyState = RootTasks.Count == 0;

        // 更新分类任务计数
        UpdateCategoryTaskCounts();
    }

    /// <summary>
    /// 更新各分类的任务数量显示
    /// </summary>
    private void UpdateCategoryTaskCounts()
    {
        var allTasks = _db.GetAllTasks();
        var totalCount = allTasks.Count;

        foreach (var cat in Categories)
        {
            if (cat.Id == 0)
            {
                cat.TaskCount = totalCount;
            }
            else if (cat.Id == -1)
            {
                cat.TaskCount = allTasks.Count(t => t.CategoryId == null);
            }
            else
            {
                cat.TaskCount = allTasks.Count(t => t.CategoryId == cat.Id);
            }
        }
    }
    
}