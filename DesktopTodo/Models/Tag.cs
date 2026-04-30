using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopTodo.Models;

public class Tag : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isSelected;

    public int Id { get; set; }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => Name;
}