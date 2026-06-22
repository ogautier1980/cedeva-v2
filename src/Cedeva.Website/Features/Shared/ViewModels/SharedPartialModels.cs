namespace Cedeva.Website.Features.Shared.ViewModels;

/// <summary>Model for the _EmptyState partial (centered icon + message + optional create button).</summary>
public class EmptyStateModel
{
    public string Icon { get; set; } = "fa-inbox";
    public string Message { get; set; } = string.Empty;
    public string? CreateUrl { get; set; }
    public string? CreateText { get; set; }
}

/// <summary>Model for the _DeleteButton partial (inline confirm form + trash button).</summary>
public class DeleteButtonModel
{
    public int Id { get; set; }
    public string ConfirmMessage { get; set; } = string.Empty;
    public string Action { get; set; } = "Delete";
    public string? Controller { get; set; }

    /// <summary>When true, render a small table-row button; otherwise a normal-size button.</summary>
    public bool Small { get; set; } = true;
}
