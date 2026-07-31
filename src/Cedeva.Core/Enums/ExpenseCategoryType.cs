namespace Cedeva.Core.Enums;

/// <summary>
/// Type of an <see cref="Entities.ExpenseCategory"/>. Expense = 0 keeps the historical default of
/// the bool it replaces (IsIncome = false).
/// </summary>
public enum ExpenseCategoryType
{
    Expense = 0,
    Income = 1,

    /// <summary>Internal transfer (e.g. cash-to-bank deposit) — excluded from the Entrées/Sorties totals.</summary>
    OffBalance = 2
}
