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

    /// <summary>
    /// Показывает диалог редактирования существующей транзакции.
    /// Возвращает true, если изменения сохранены.
    /// </summary>
    bool ShowEditTransaction(long transactionId);

    /// <summary>Показывает окно управления справочником категорий.</summary>
    void ShowCategories();

    /// <summary>Показывает окно бюджетов на указанный месяц («ГГГГ-ММ»).</summary>
    void ShowBudgets(string month);

    /// <summary>
    /// Спрашивает подтверждение действия. Возвращает true,
    /// если пользователь ответил «Да».
    /// </summary>
    bool Confirm(string title, string message);
}
