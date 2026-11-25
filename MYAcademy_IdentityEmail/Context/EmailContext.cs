
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Context
{
    public class EmailContext : IdentityDbContext<AppUser>
    {
        public EmailContext() { }
        public EmailContext(DbContextOptions<EmailContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
            optionsBuilder.UseSqlServer(
                @"Server=(localdb)\MSSQLLocalDB;Initial Catalog=IdentityEmailDbNight;Integrated Security=True;TrustServerCertificate=True;");
            }
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageUserState> MessageUserStates { get; set; }
        public DbSet<DraftMessage> DraftMessages { get; set; }
        public DbSet<EmailVerificationState> EmailVerificationStates { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<MessageUserState>().HasIndex(x => new { x.UserId, x.MessageId }).IsUnique();
            builder.Entity<MessageUserState>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<MessageUserState>().HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<MessageUserState>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<DraftMessage>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<DraftMessage>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<EmailVerificationState>().HasKey(x => x.UserId);
            builder.Entity<EmailVerificationState>().HasOne(x => x.User).WithOne().HasForeignKey<EmailVerificationState>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}


