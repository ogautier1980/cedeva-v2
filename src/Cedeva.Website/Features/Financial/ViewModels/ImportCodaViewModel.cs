using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Financial.ViewModels;

public class ImportCodaViewModel
{
    [Display(Name = "Field.CodaFile")]
    [Required]
    public IFormFile? CodaFile { get; set; }
}

public class CodaFileListItemViewModel
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime ImportDate { get; set; }
    public DateTime StatementDate { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal OldBalance { get; set; }
    public decimal NewBalance { get; set; }
    public int TransactionCount { get; set; }
    public int ReconciledCount { get; set; }
    public int UnreconciledCount { get; set; }
}
