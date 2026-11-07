using System.Windows.Input;

namespace Kopilka.App.Presentation;

/// <summary>
/// Команда для привязки к кнопкам: обёртка над делегатами действия
/// и проверки доступности. Реализация ICommand своя, без MVVM-библиотек.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>Создаёт команду без параметров.</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(
            _ => execute(),
            canExecute is null ? null : _ => canExecute())
    {
    }

    /// <summary>Создаёт команду с параметром.</summary>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Вызывается явно, когда условия доступности команды изменились:
    /// подписчики (кнопки) перечитывают CanExecute.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CanExecuteChanged;
}
