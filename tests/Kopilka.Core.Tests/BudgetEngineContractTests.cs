using Kopilka.Core.Engines;
using Xunit;

namespace Kopilka.Core.Tests;

/// <summary>
/// Контрактные тесты движка бюджетов против норматива
/// docs/specs/finance-rules.md: соседние копейки у порога 80 %,
/// «доступно в день» у «внимания», февраль 28/29 дней, деление
/// без потерь и с потерей копейки, неизменяемость снимка.
/// </summary>
public class BudgetEngineContractTests
{
    [Fact]
    public void Compute_ThresholdNeighbouringKopecks_AreExactAroundEightyPercent()
    {
        // Лимит 10 000,00 ₽: ровно 80 % = 8 000,00 ₽. Соседние копейки
        // расходятся ровно по границе: порог включительный.
        BudgetSnapshot plusOne = BudgetEngine.Compute(10_000_00, 8_000_01, 15, 30);
        BudgetSnapshot minusOne = BudgetEngine.Compute(10_000_00, 7_999_99, 15, 30);

        Assert.Equal(BudgetStatus.Attention, plusOne.Status);
        Assert.Equal(BudgetStatus.Ok, minusOne.Status);

        // Лимит 15 000,01 ₽: 80 % дробные (1 200 000,8 коп.). Наименьшая
        // сумма «внимания» — 1 200 001 коп., наибольшая «в норме» — 1 200 000.
        BudgetSnapshot fractionalBelow = BudgetEngine.Compute(15_000_01, 1_200_000, 15, 30);
        BudgetSnapshot fractionalAbove = BudgetEngine.Compute(15_000_01, 1_200_001, 15, 30);

        Assert.Equal(BudgetStatus.Ok, fractionalBelow.Status);
        Assert.Equal(BudgetStatus.Attention, fractionalAbove.Status);
    }

    [Fact]
    public void Compute_AtAttentionStatus_AvailablePerDayIsComputed()
    {
        // «Доступно в день» есть не только у «в норме»: ровно 80 %
        // потрачено, остаток 2 000,00 ₽ на 16 дней (15-е из 30 включительно).
        BudgetSnapshot snapshot = BudgetEngine.Compute(10_000_00, 8_000_00, 15, 30);

        Assert.Equal(BudgetStatus.Attention, snapshot.Status);
        Assert.Equal(2_000_00, snapshot.RemainingKopecks);
        Assert.Equal(12_500, snapshot.AvailablePerDayKopecks);
    }

    [Fact]
    public void Compute_February28And29DaysFromFirstDay_PerDayDiffer()
    {
        // currentDay = 1: весь месяц впереди. Тот же лимит 1 000,00 ₽
        // делится на 28 дней обычного февраля и на 29 високосного.
        BudgetSnapshot commonFebruary = BudgetEngine.Compute(1_000_00, 0, 1, 28);
        BudgetSnapshot leapFebruary = BudgetEngine.Compute(1_000_00, 0, 1, 29);

        Assert.Equal(BudgetStatus.Ok, commonFebruary.Status);
        Assert.Equal(BudgetStatus.Ok, leapFebruary.Status);
        Assert.Equal(3_571, commonFebruary.AvailablePerDayKopecks); // 100 000 / 28
        Assert.Equal(3_448, leapFebruary.AvailablePerDayKopecks);   // 100 000 / 29
    }

    [Fact]
    public void Compute_RemainderDivision_ExactAndWithOneKopeckLoss()
    {
        // Деление без потерь: остаток 2 700,00 ₽ ровно на 3 дня.
        BudgetSnapshot exact = BudgetEngine.Compute(30_000_00, 3_000_00, 28, 30);

        Assert.Equal(27_000_00, exact.RemainingKopecks);
        Assert.Equal(9_000_00, exact.AvailablePerDayKopecks);

        // Деление с потерей: 3 000,01 ₽ на 3 дня → 1 000,00 ₽;
        // 3 · 1 000,00 + 1 = 3 000,01 — копейка не «размазывается» по дням.
        BudgetSnapshot lossy = BudgetEngine.Compute(3_000_01, 0, 29, 31);

        Assert.Equal(3_000_01, lossy.RemainingKopecks);
        Assert.Equal(1_000_00, lossy.AvailablePerDayKopecks);
        Assert.Equal(3_000_01, 3 * lossy.AvailablePerDayKopecks!.Value + 1);
    }

    [Fact]
    public void Compute_SameInputs_ProduceEqualSnapshots()
    {
        // BudgetSnapshot — record: пересчёт с теми же входами даёт
        // равный снимок по значениям; поля читаются из него как есть.
        BudgetSnapshot first = BudgetEngine.Compute(10_000_00, 8_200_00, 15, 30);
        BudgetSnapshot second = BudgetEngine.Compute(10_000_00, 8_200_00, 15, 30);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.Equal(new BudgetSnapshot(BudgetStatus.Attention, 1_800_00, 11_250), first);
        Assert.Equal(BudgetStatus.Attention, first.Status);
        Assert.Equal(1_800_00, first.RemainingKopecks);
        Assert.Equal(11_250, first.AvailablePerDayKopecks);
    }
}
