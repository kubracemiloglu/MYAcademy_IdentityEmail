namespace MYAcademy_IdentityEmail.Entities
{
    // Katılımcıların yıldız, etiket ve çöp kutusu durumları birbirinden bağımsızdır.
    public class MessageUserState
    {
        public int MessageUserStateId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public string MessageId { get; set; } = string.Empty;
        public Message Message { get; set; } = null!;
        public bool IsStarred { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsPermanentlyDeleted { get; set; }
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}
