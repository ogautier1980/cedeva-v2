using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cedeva.Website.Infrastructure;

/// <summary>
/// Factors the list-page filter persistence shared by the index actions: store search/sort/page in
/// session on a query, redirect to a clean URL, then reload them (clearing on a plain navigation/F5).
/// The session keys keep the historical "{key}_SearchString" naming so existing sessions still work.
/// </summary>
public static class FilterStateExtensions
{
    /// <summary>
    /// If the request carried query parameters, stores them in session and signals a redirect to a
    /// clean URL. Returns true when the caller should `return RedirectToAction(nameof(Index))`.
    /// </summary>
    public static bool TryPersistFiltersForRedirect(
        this ISessionStateService session, Controller controller, string key, QueryParametersBase qp)
    {
        if (controller.Request.Query.Count == 0)
            return false;

        if (!string.IsNullOrWhiteSpace(qp.SearchString))
            session.Set($"{key}_SearchString", qp.SearchString, persistToCookie: false);
        if (!string.IsNullOrWhiteSpace(qp.SortBy))
            session.Set($"{key}_SortBy", qp.SortBy, persistToCookie: false);
        if (!string.IsNullOrWhiteSpace(qp.SortOrder))
            session.Set($"{key}_SortOrder", qp.SortOrder, persistToCookie: false);
        if (qp.PageNumber > 1)
            session.Set($"{key}_PageNumber", qp.PageNumber.ToString(), persistToCookie: false);

        controller.TempData[ControllerExtensions.KeepFiltersKey] = true;
        return true;
    }

    /// <summary>
    /// Loads the stored filters into <paramref name="qp"/>. On a plain navigation (no KeepFilters
    /// flag from a just-completed redirect) the stored filters are cleared first.
    /// </summary>
    public static void ApplyStoredFilters(
        this ISessionStateService session, Controller controller, string key, QueryParametersBase qp)
    {
        if (controller.TempData[ControllerExtensions.KeepFiltersKey] == null)
        {
            session.Clear($"{key}_SearchString");
            session.Clear($"{key}_SortBy");
            session.Clear($"{key}_SortOrder");
            session.Clear($"{key}_PageNumber");
        }

        qp.SearchString = session.Get($"{key}_SearchString");
        qp.SortBy = session.Get($"{key}_SortBy");
        qp.SortOrder = session.Get($"{key}_SortOrder");

        var pageNumberStr = session.Get($"{key}_PageNumber");
        if (!string.IsNullOrEmpty(pageNumberStr) && int.TryParse(pageNumberStr, out var pageNum))
            qp.PageNumber = pageNum;
    }
}
