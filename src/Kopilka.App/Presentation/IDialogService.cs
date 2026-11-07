namespace Kopilka.App.Presentation;

/// <summary>
/// Сервис диалогов: модели представления не знают о View, поэтому
/// окна запрашиваются через интерфейс. В тестах подменяется фейком.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Показывает диалог добавления транзакции. Возвращает true,
    /// если транзакция сохранена, false — если ввод отменён.
    /// </summary>
    bool ShowAddTransaction();
}
