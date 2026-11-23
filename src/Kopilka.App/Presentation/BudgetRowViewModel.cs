using Kopilka.Core.Engines;

namespace Kopilka.App.Presentation;

/// <summary>
/// Строка панели бюджетов: категория с лимитом, лимит и потрачено
/// за месяц, остаток, «доступно в день» и статус. Все суммы готовы
/// к показу; статус вычислен движком BudgetEngine. Категория
/// с признаком лимита, но без бюджета на месяц, показывается строкой
/// «не задан» — иначе её отсутствие незаметно.
/// </summary>
public sealed record BudgetRowViewModel(
    long CategoryId,
    string CategoryName,
    bool HasBudget,
    long LimitKopecks,
    long SpentKopecks,
    BudgetSnapshot? Snapshot)
{
    /// <summary>Лимит или «не задан».</summary>
    public string LimitText => HasBudget ? MoneyText.Format(LimitKopecks) : "не задан";

    /// <summary>Потрачено за месяц.</summary>
    public string SpentText => MoneyText.Format(SpentKopecks);

    /// <summary>Остаток лимита; без бюджета — прочерк.</summary>
    public string RemainingText => Snapshot is null ? "—" : MoneyText.Format(Snapshot.RemainingKopecks);

    /// <summary>«Доступно в день»; нет у превышенного бюджета и без бюджета.</summary>
    public string AvailablePerDayText =>
        Snapshot?.AvailablePerDayKopecks is long available ? MoneyText.Format(available) : "—";

    /// <summary>Статус словом: «в норме», «внимание», «превышен» или «не задан».</summary>
    public string StatusText =>
        !HasBudget ? "не задан"
        : Snapshot!.Status == BudgetStatus.Ok ? "в норме"
        : Snapshot.Status == BudgetStatus.Attention ? "внимание"
        : "превышен";

    /// <summary>Цвет статуса: зелёный / янтарный / красный / серый.</summary>
    public string StatusColor =>
        !HasBudget ? "#888888"
        : Snapshot!.Status == BudgetStatus.Ok ? "#1E7B34"
        : Snapshot.Status == BudgetStatus.Attention ? "#B8860B"
        : "#B00020";
}
