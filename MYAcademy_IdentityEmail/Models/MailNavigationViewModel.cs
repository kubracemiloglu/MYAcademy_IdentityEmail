using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class MailNavigationViewModel
    {
        public AppUser? User { get; set; }
        public int Inbox { get; set; }
        public int Unread { get; set; }
        public int Sent { get; set; }
        public int Starred { get; set; }
        public int Drafts { get; set; }
        public int Trash { get; set; }
        public List<Category> Categories { get; set; } = [];
        public Dictionary<int, int> CategoryCounts { get; set; } = [];
    }
}
