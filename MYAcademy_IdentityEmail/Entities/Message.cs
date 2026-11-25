using System.ComponentModel;

namespace MYAcademy_IdentityEmail.Entities
{
    public class Message
    {
        public string MessageId { get; set; }
        public string ReceiverEmail { get; set; }
        public string SenderEmail { get; set; }
        public string Subject { get; set; }
        public DateTime SentDate { get; set; } 
        public string MessageDetail { get; set; }
        public bool IsRead { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
    }
}
