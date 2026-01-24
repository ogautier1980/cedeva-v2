using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class SendEmailViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Veuillez sélectionner un destinataire.")]
    [Display(Name = "Destinataire")]
    public string SelectedRecipient { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le sujet est obligatoire.")]
    [StringLength(255, ErrorMessage = "Le sujet ne peut pas dépasser 255 caractères.")]
    [Display(Name = "Sujet")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le message est obligatoire.")]
    [StringLength(1024, ErrorMessage = "Le message ne peut pas dépasser 1024 caractères.")]
    [Display(Name = "Message")]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "Pièce jointe (optionnel)")]
    public IFormFile? AttachmentFile { get; set; }

    public List<SelectListItem> RecipientOptions { get; set; } = new();
}
