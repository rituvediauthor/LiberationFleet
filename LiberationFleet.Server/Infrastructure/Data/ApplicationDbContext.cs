using Microsoft.EntityFrameworkCore;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IUnitOfWork
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Crew> Crews => Set<Crew>();
    public DbSet<CrewMembership> CrewMemberships => Set<CrewMembership>();
    public DbSet<UserPaymentPlatform> UserPaymentPlatforms => Set<UserPaymentPlatform>();
    public DbSet<CrewPaymentPlatform> CrewPaymentPlatforms => Set<CrewPaymentPlatform>();
    public DbSet<PaymentPlatform> PaymentPlatforms => Set<PaymentPlatform>();
    public DbSet<Gift> Gifts => Set<Gift>();
    public DbSet<SeasonCycle> SeasonCycles => Set<SeasonCycle>();
    public DbSet<MonthlySurvivalThreshold> MonthlySurvivalThresholds => Set<MonthlySurvivalThreshold>();
    public DbSet<UserKeyBundle> UserKeyBundles => Set<UserKeyBundle>();
    public DbSet<UserPrivateKeyBackup> UserPrivateKeyBackups => Set<UserPrivateKeyBackup>();
    public DbSet<CrewKeyDistribution> CrewKeyDistributions => Set<CrewKeyDistribution>();
    public DbSet<EncryptedContentEnvelope> EncryptedContentEnvelopes => Set<EncryptedContentEnvelope>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalVote> ProposalVotes => Set<ProposalVote>();
    public DbSet<ProposalComment> ProposalComments => Set<ProposalComment>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumComment> ForumComments => Set<ForumComment>();
    public DbSet<ProjectPost> ProjectPosts => Set<ProjectPost>();
    public DbSet<ProjectComment> ProjectComments => Set<ProjectComment>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMessage> ChatRoomMessages => Set<ChatRoomMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.InNeedOfAid).HasDefaultValue(true);
            entity.Property(e => e.PercentBonus).HasDefaultValue(0);
        });

        modelBuilder.Entity<PaymentPlatform>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
            entity.HasData(
                new PaymentPlatform { Id = 1, Name = "PayPal", SortOrder = 1 },
                new PaymentPlatform { Id = 2, Name = "Cash App", SortOrder = 2 },
                new PaymentPlatform { Id = 3, Name = "Venmo", SortOrder = 3 },
                new PaymentPlatform { Id = 4, Name = "Zelle", SortOrder = 4 },
                new PaymentPlatform { Id = 5, Name = "Other", SortOrder = 5 });
        });

        modelBuilder.Entity<CrewPaymentPlatform>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => new { e.CrewId, e.Name }).IsUnique();
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPaymentPlatform>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Handle).IsRequired().HasMaxLength(128);
            entity.Property(e => e.IsPreferred).HasDefaultValue(false);
            entity.HasOne(e => e.User)
                .WithMany(u => u.PaymentPlatforms)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CrewPaymentPlatform)
                .WithMany(p => p.UserAccounts)
                .HasForeignKey(e => e.CrewPaymentPlatformId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.Token).IsRequired();
        });

        modelBuilder.Entity<Crew>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JoinCode).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.JoinCode).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ZipCode).HasMaxLength(10);
            entity.Property(e => e.SeasonStarted).HasDefaultValue(false);
            entity.Property(e => e.SeasonMemberCycleCap).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.SeasonNonMemberCycleCap).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CrewMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CrewId }).IsUnique();
            entity.Property(e => e.IsOrganizer).HasDefaultValue(false);
            entity.Property(e => e.IsHonoraryMember).HasDefaultValue(false);
            entity.Property(e => e.EstimatedMonthlyContribution).HasPrecision(18, 2);
            entity.Property(e => e.IsSeasonReady).HasDefaultValue(false);
            entity.Property(e => e.IsInSeason).HasDefaultValue(false);
            entity.Property(e => e.CurrentPriorityScore).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.HasOne(e => e.User)
                .WithMany(u => u.CrewMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Crew)
                .WithMany(c => c.Memberships)
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Gift>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.IsSurvivalThreshold).HasDefaultValue(false);
            entity.Property(e => e.CountsTowardReception).HasDefaultValue(true);
            entity.Property(e => e.IsCustomGift).HasDefaultValue(false);
            entity.Property(e => e.CountsTowardContribution).HasDefaultValue(true);
            entity.Property(e => e.ReceptionApplied).HasDefaultValue(false);
            entity.Property(e => e.VerificationStatus).HasDefaultValue(GiftVerificationStatus.Pending);
            entity.HasOne(e => e.CrewPaymentPlatform)
                .WithMany(p => p.Gifts)
                .HasForeignKey(e => e.CrewPaymentPlatformId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GiverUser)
                .WithMany()
                .HasForeignKey(e => e.GiverUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RecipientUser)
                .WithMany()
                .HasForeignKey(e => e.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.MiddlemanUser)
                .WithMany()
                .HasForeignKey(e => e.MiddlemanUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InitiatedGift)
                .WithMany()
                .HasForeignKey(e => e.InitiatedGiftId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SeasonCycle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CrewId, e.UserId, e.SeasonStartDate }).IsUnique();
            entity.Property(e => e.CycleCapAtStart).HasPrecision(18, 2);
            entity.Property(e => e.TotalReceptionAmount).HasPrecision(18, 2);
            entity.Property(e => e.SurvivalThresholdReceived).HasPrecision(18, 2);
            entity.Property(e => e.CycleReceived).HasPrecision(18, 2);
            entity.Property(e => e.PriorityScoreAtSeasonStart).HasPrecision(18, 2);
            entity.Property(e => e.HasCycleStarted).HasDefaultValue(false);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonthlySurvivalThreshold>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CrewId, e.UserId, e.Year, e.Month }).IsUnique();
            entity.Property(e => e.ThresholdAmount).HasPrecision(18, 2);
            entity.Property(e => e.ReceivedAmount).HasPrecision(18, 2);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserKeyBundle>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.IdentityPublicKey).IsRequired();
            entity.Property(e => e.KeyVersion).HasDefaultValue(1);
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<UserKeyBundle>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPrivateKeyBackup>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Salt).IsRequired();
            entity.Property(e => e.Iv).IsRequired();
            entity.Property(e => e.Ciphertext).IsRequired();
            entity.Property(e => e.KeyVersion).HasDefaultValue(1);
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<UserPrivateKeyBackup>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CrewKeyDistribution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CrewId, e.UserId, e.KeyVersion }).IsUnique();
            entity.Property(e => e.WrappedCrewKey).IsRequired();
            entity.Property(e => e.WrapNonce).IsRequired();
            entity.Property(e => e.KeyVersion).HasDefaultValue(1);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.WrappedByUser)
                .WithMany()
                .HasForeignKey(e => e.WrappedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EncryptedContentEnvelope>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ContentType, e.ResourceId }).IsUnique();
            entity.Property(e => e.ResourceId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Nonce).IsRequired();
            entity.Property(e => e.Ciphertext).IsRequired();
            entity.Property(e => e.KeyVersion).HasDefaultValue(1);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasDefaultValue(ProposalStatus.Pending);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProposalVote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProposalId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Proposal)
                .WithMany(p => p.Votes)
                .HasForeignKey(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Proposal)
                .WithMany(p => p.Comments)
                .HasForeignKey(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(e => e.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ForumPost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ForumComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.ForumPost)
                .WithMany(p => p.Comments)
                .HasForeignKey(e => e.ForumPostId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(e => e.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectPost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.ProjectPost)
                .WithMany(p => p.Comments)
                .HasForeignKey(e => e.ProjectPostId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(e => e.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatRoomMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(e => new { e.ChatRoomId, e.Id });
            entity.HasOne(e => e.ChatRoom)
                .WithMany(r => r.Messages)
                .HasForeignKey(e => e.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
