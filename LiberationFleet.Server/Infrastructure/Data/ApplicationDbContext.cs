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
    }
}
