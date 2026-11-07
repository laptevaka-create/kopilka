using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kopilka.App.Presentation;

/// <summary>
/// Базовый класс моделей представления: уведомляет интерфейс
/// об изменении свойств через INotifyPropertyChanged. Сторонние
/// MVVM-библиотеки не используются — весь каркас умещается в классы
/// ViewModelBase и RelayCommand.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Записывает значение в поле и уведомляет интерфейс, если значение
    /// изменилось. Возвращает true, когда изменение произошло.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <summary>Уведомляет интерфейс о пересчёте вычисляемого свойства.</summary>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
