namespace MYAcademy_IdentityEmail.Entities
{
    public class DraftMessage
    {
        public string DraftMessageId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public string ReceiverEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string MessageDetail { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
