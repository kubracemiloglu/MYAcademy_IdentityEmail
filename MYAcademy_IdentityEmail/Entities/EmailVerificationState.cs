namespace MYAcademy_IdentityEmail.Entities
{
    public class EmailVerificationState
    {
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime LastSentAt { get; set; }
        public int FailedAttempts { get; set; }
    }
}
