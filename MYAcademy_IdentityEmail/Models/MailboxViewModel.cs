using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class MailboxViewModel
    {
        public string Folder { get; set; } = "Inbox";
        public string Title { get; set; } = "Gelen Kutusu";
        public string? Search { get; set; }
        public string Filter { get; set; } = "all";
        public string Sort { get; set; } = "newest";
        public int? CategoryId { get; set; }
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public List<Message> Messages { get; set; } = [];
        public Dictionary<string, MessageUserState> States { get; set; } = [];
        public Dictionary<string, AppUser> People { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<Category> Categories { get; set; } = [];
        public Message? SelectedMessage { get; set; }
    }
}
