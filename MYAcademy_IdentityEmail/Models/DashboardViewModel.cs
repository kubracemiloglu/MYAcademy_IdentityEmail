using System.ComponentModel.DataAnnotations;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Models
{
    public class DashboardViewModel
    {
        public MailNavigationViewModel Counts { get; set; } = new();
        public List<string> Labels { get; set; } = [];
        public List<int> Received { get; set; } = [];
        public List<int> Sent { get; set; } = [];
        public List<Message> RecentMessages { get; set; } = [];
    }
}
