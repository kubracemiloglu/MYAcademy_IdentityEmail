using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class CreateMessageViewModel
    {
        public string? DraftMessageId { get; set; }
        [Required(ErrorMessage = "Alıcının e-posta adresini yazın.")][EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")][StringLength(256)]
        public string ReceiverEmail { get; set; } = string.Empty;
        [Required(ErrorMessage = "Bir konu yazın.")][StringLength(200, ErrorMessage = "Konu en fazla 200 karakter olabilir.")]
        public string Subject { get; set; } = string.Empty;
        [Required(ErrorMessage = "Mesajınızı yazın.")][StringLength(20000, ErrorMessage = "Mesaj en fazla 20.000 karakter olabilir.")]
        public string MessageDetail { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public List<Category> Categories { get; set; } = [];
    }
}
