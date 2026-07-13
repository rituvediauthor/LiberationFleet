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
    public DbSet<EmergencyRequest> EmergencyRequests => Set<EmergencyRequest>();
    public DbSet<EmergencySplitOffer> EmergencySplitOffers => Set<EmergencySplitOffer>();
    public DbSet<EmergencyGiftResponse> EmergencyGiftResponses => Set<EmergencyGiftResponse>();
    public DbSet<MonthlySurvivalThreshold> MonthlySurvivalThresholds => Set<MonthlySurvivalThreshold>();
    public DbSet<UserKeyBundle> UserKeyBundles => Set<UserKeyBundle>();
    public DbSet<UserPrivateKeyBackup> UserPrivateKeyBackups => Set<UserPrivateKeyBackup>();
    public DbSet<CrewKeyDistribution> CrewKeyDistributions => Set<CrewKeyDistribution>();
    public DbSet<EncryptedContentEnvelope> EncryptedContentEnvelopes => Set<EncryptedContentEnvelope>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalVote> ProposalVotes => Set<ProposalVote>();
    public DbSet<ProposalComment> ProposalComments => Set<ProposalComment>();
    public DbSet<ProposalCrewSettingChange> ProposalCrewSettingChanges => Set<ProposalCrewSettingChange>();
    public DbSet<ProposalCrewRuleChange> ProposalCrewRuleChanges => Set<ProposalCrewRuleChange>();
    public DbSet<ProposalCrewChatChange> ProposalCrewChatChanges => Set<ProposalCrewChatChange>();
    public DbSet<ProposalCrewmateKick> ProposalCrewmateKicks => Set<ProposalCrewmateKick>();
    public DbSet<ProposalCrewmateRejoin> ProposalCrewmateRejoins => Set<ProposalCrewmateRejoin>();
    public DbSet<ProposalCrewJoinRequest> ProposalCrewJoinRequests => Set<ProposalCrewJoinRequest>();
    public DbSet<ProposalCrewRoleChange> ProposalCrewRoleChanges => Set<ProposalCrewRoleChange>();
    public DbSet<ProposalClaimPlaceholderIdentity> ProposalClaimPlaceholderIdentities => Set<ProposalClaimPlaceholderIdentity>();
    public DbSet<ProposalCrewmatePermissionGrant> ProposalCrewmatePermissionGrants => Set<ProposalCrewmatePermissionGrant>();
    public DbSet<ProposalAnonymousAlias> ProposalAnonymousAliases => Set<ProposalAnonymousAlias>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumComment> ForumComments => Set<ForumComment>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMessage> ChatRoomMessages => Set<ChatRoomMessage>();
    public DbSet<VoiceParticipantSession> VoiceParticipantSessions => Set<VoiceParticipantSession>();
    public DbSet<UserRegisteredDevice> UserRegisteredDevices => Set<UserRegisteredDevice>();
    public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();
    public DbSet<CrewRule> CrewRules => Set<CrewRule>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ContentMention> ContentMentions => Set<ContentMention>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<UserMutedContent> UserMutedContents => Set<UserMutedContent>();
    public DbSet<UserHiddenContent> UserHiddenContents => Set<UserHiddenContent>();
    public DbSet<DirectConversation> DirectConversations => Set<DirectConversation>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<FallibleClickStats> FallibleClickStats => Set<FallibleClickStats>();
    public DbSet<FallibleClickUser> FallibleClickUsers => Set<FallibleClickUser>();
    public DbSet<LibraryCategory> LibraryCategories => Set<LibraryCategory>();
    public DbSet<LibraryOffering> LibraryOfferings => Set<LibraryOffering>();
    public DbSet<LibraryOfferingCategory> LibraryOfferingCategories => Set<LibraryOfferingCategory>();
    public DbSet<LibraryUnit> LibraryUnits => Set<LibraryUnit>();
    public DbSet<LibraryRequest> LibraryRequests => Set<LibraryRequest>();
    public DbSet<LibraryRequestMessage> LibraryRequestMessages => Set<LibraryRequestMessage>();
    public DbSet<LibraryMaintenanceRecord> LibraryMaintenanceRecords => Set<LibraryMaintenanceRecord>();

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
            entity.Property(e => e.IsUnclaimedPlaceholder).HasDefaultValue(false);
            entity.Property(e => e.IsCrewGiftRecipient).HasDefaultValue(false);
            entity.Property(e => e.PercentBonus).HasDefaultValue(0);
            entity.Property(e => e.PeopleRepresentedCount).HasDefaultValue(1);
            entity.Property(e => e.DisabilityLevel).HasDefaultValue(0);
            entity.Property(e => e.AdultContentPreference).HasDefaultValue(AdultContentPreference.Block);
            entity.Property(e => e.TwoFactorEnabled).HasDefaultValue(false);
            entity.Property(e => e.LockSettingsWithPassword).HasDefaultValue(false);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
        });

        modelBuilder.Entity<UserRegisteredDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.UserAgent).HasMaxLength(512);
            entity.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.RegisteredDevices)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SecurityAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.HasIndex(e => new { e.UserId, e.OccurredAt });
            entity.HasOne(e => e.User)
                .WithMany(u => u.SecurityAlerts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.RelatedDevice)
                .WithMany()
                .HasForeignKey(e => e.RelatedDeviceId)
                .OnDelete(DeleteBehavior.NoAction);
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
            entity.Property(e => e.AllowSurvivalThresholds).HasDefaultValue(true);
            entity.Property(e => e.RequireApprovalForEdits).HasDefaultValue(true);
            entity.Property(e => e.InNeedDefaultThreshold).HasPrecision(18, 2).HasDefaultValue(20m);
            entity.Property(e => e.LibraryOfThingsEnabled).HasDefaultValue(true);
            entity.Property(e => e.MemberCycleCapMode).HasDefaultValue(CycleCapMode.CapacityBased);
            entity.Property(e => e.MemberCycleCapFixedAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.MemberCycleCapMultiplier).HasPrecision(18, 4).HasDefaultValue(2m);
            entity.Property(e => e.NonMemberCycleCapMode).HasDefaultValue(CycleCapMode.CapacityBased);
            entity.Property(e => e.NonMemberCycleCapFixedAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.NonMemberCycleCapMultiplier).HasPrecision(18, 4).HasDefaultValue(0.5m);
            entity.Property(e => e.AllowCrewmateFileAttachments).HasDefaultValue(false);
            entity.Property(e => e.MinimumCrewmateTenureDaysForAttachments).HasDefaultValue(0);
            entity.Property(e => e.MinimumContributionForAttachments).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.MinimumCrewmateTenureDaysForProposals).HasDefaultValue(0);
            entity.Property(e => e.MinimumContributionForProposals).HasPrecision(18, 2).HasDefaultValue(0m);
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
            entity.Property(e => e.IsAdvocate).HasDefaultValue(false);
            entity.Property(e => e.IsDecentralizer).HasDefaultValue(false);
            entity.Property(e => e.IsCeremonialOrganizer).HasDefaultValue(false);
            entity.Property(e => e.IsModerator).HasDefaultValue(false);
            entity.Property(e => e.IsIntermediary).HasDefaultValue(false);
            entity.Property(e => e.IntermediaryFailedCompletions).HasDefaultValue(0);
            entity.Property(e => e.EmergencySacrificesThisSeason).HasDefaultValue(0);
            entity.Property(e => e.IsPlaceholderMember).HasDefaultValue(false);
            entity.Property(e => e.CanAttachFiles).HasDefaultValue(false);
            entity.Property(e => e.CanCreateProposals).HasDefaultValue(false);
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
            entity.HasOne<EmergencyRequest>()
                .WithMany()
                .HasForeignKey(e => e.EmergencyRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SeasonCycle>()
                .WithMany()
                .HasForeignKey(e => e.SeasonCycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SeasonCycle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CrewId, e.UserId, e.SeasonStartDate });
            entity.Property(e => e.CycleCapAtStart).HasPrecision(18, 2);
            entity.Property(e => e.CycleCapAtCompletion).HasPrecision(18, 2);
            entity.Property(e => e.TotalReceptionAmount).HasPrecision(18, 2);
            entity.Property(e => e.SurvivalThresholdReceived).HasPrecision(18, 2);
            entity.Property(e => e.CycleReceived).HasPrecision(18, 2);
            entity.Property(e => e.PriorityScoreAtSeasonStart).HasPrecision(18, 2);
            entity.Property(e => e.HasCycleStarted).HasDefaultValue(false);
            entity.Property(e => e.UsesSegmentCap).HasDefaultValue(false);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.EmergencyRequest)
                .WithMany()
                .HasForeignKey(e => e.EmergencyRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EmergencySplitOffer)
                .WithMany()
                .HasForeignKey(e => e.EmergencySplitOfferId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmergencyRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Purpose).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.AmountNeeded).HasPrecision(18, 2);
            entity.Property(e => e.AmountFulfilled).HasPrecision(18, 2);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.RequesterUser)
                .WithMany()
                .HasForeignKey(e => e.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmergencySplitOffer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.EmergencyRequest)
                .WithMany(r => r.SplitOffers)
                .HasForeignKey(e => e.EmergencyRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.OffererUser)
                .WithMany()
                .HasForeignKey(e => e.OffererUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmergencyGiftResponse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.EmergencyRequest)
                .WithMany(r => r.GiftResponses)
                .HasForeignKey(e => e.EmergencyRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GiverUser)
                .WithMany()
                .HasForeignKey(e => e.GiverUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Gift)
                .WithMany()
                .HasForeignKey(e => e.GiftId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.Kind).HasDefaultValue(ProposalKind.General);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProposalCrewSettingChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.NewValue).HasMaxLength(500);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewSettingChange)
                .HasForeignKey<ProposalCrewSettingChange>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalCrewRuleChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Nonce).HasMaxLength(500);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewRuleChange)
                .HasForeignKey<ProposalCrewRuleChange>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalCrewChatChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Purpose).HasMaxLength(2000);
            entity.Property(e => e.NameNonce).HasMaxLength(500);
            entity.Property(e => e.IsAdultContent).HasDefaultValue(false);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewChatChange)
                .HasForeignKey<ProposalCrewChatChange>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalCrewmateKick>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.AnonymousNickname).HasMaxLength(64).IsRequired();
            entity.Property(e => e.RevealedUsername).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewmateKick)
                .HasForeignKey<ProposalCrewmateKick>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalCrewmateRejoin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewmateRejoin)
                .HasForeignKey<ProposalCrewmateRejoin>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalCrewJoinRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.ApplicantUsername).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AcceptedRuleIdsJson).HasMaxLength(2000);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewJoinRequest)
                .HasForeignKey<ProposalCrewJoinRequest>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalCrewRoleChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.RolesJson).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewRoleChange)
                .HasForeignKey<ProposalCrewRoleChange>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalClaimPlaceholderIdentity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.PlaceholderDisplayName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.ClaimPlaceholderIdentity)
                .HasForeignKey<ProposalClaimPlaceholderIdentity>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalCrewmatePermissionGrant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProposalId).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.CrewmatePermissionGrant)
                .HasForeignKey<ProposalCrewmatePermissionGrant>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalAnonymousAlias>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProposalId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.ProposalId, e.Nickname }).IsUnique();
            entity.Property(e => e.Nickname).HasMaxLength(64).IsRequired();
            entity.HasOne(e => e.Proposal)
                .WithMany()
                .HasForeignKey(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.IsAdultContent).HasDefaultValue(false);
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

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Purpose).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.AnonymousModeEnabled).HasDefaultValue(false);
            entity.Property(e => e.IsAdultContent).HasDefaultValue(false);
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

        modelBuilder.Entity<VoiceParticipantSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CrewId });
            entity.HasIndex(e => e.ConnectionId);
            entity.Property(e => e.ConnectionId).HasMaxLength(128);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ChatRoom)
                .WithMany()
                .HasForeignKey(e => e.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CrewRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsPublic).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(10000);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RequesterUserId, e.AddresseeUserId }).IsUnique();
            entity.HasOne(e => e.Requester)
                .WithMany()
                .HasForeignKey(e => e.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Addressee)
                .WithMany()
                .HasForeignKey(e => e.AddresseeUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BlockerUserId, e.BlockedUserId }).IsUnique();
            entity.HasOne(e => e.Blocker)
                .WithMany()
                .HasForeignKey(e => e.BlockerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Blocked)
                .WithMany()
                .HasForeignKey(e => e.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ActionUrl).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ContentMention>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ContentType, e.ResourceId, e.MentionedUserId }).IsUnique();
            entity.HasIndex(e => new { e.MentionedUserId, e.CreatedAt });
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.MentionedUser)
                .WithMany()
                .HasForeignKey(e => e.MentionedUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Kind }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserMutedContent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ContentType, e.ResourceId }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserHiddenContent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ContentType, e.ResourceId }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DirectConversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserLowId, e.UserHighId }).IsUnique();
            entity.HasOne(e => e.UserLow)
                .WithMany()
                .HasForeignKey(e => e.UserLowId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UserHigh)
                .WithMany()
                .HasForeignKey(e => e.UserHighId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DirectMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt });
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FallibleClickStats>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasData(new FallibleClickStats
            {
                Id = 1,
                TotalClicks = 0,
                UniqueUserClicks = 0
            });
        });

        modelBuilder.Entity<FallibleClickUser>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LibraryCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
            entity.HasData(
                new LibraryCategory { Id = 1, Name = "Produce & Fresh Foods", SortOrder = 1 },
                new LibraryCategory { Id = 2, Name = "Meat & Seafood", SortOrder = 2 },
                new LibraryCategory { Id = 3, Name = "Dairy & Eggs", SortOrder = 3 },
                new LibraryCategory { Id = 4, Name = "Bakery & Bread", SortOrder = 4 },
                new LibraryCategory { Id = 5, Name = "Frozen Foods", SortOrder = 5 },
                new LibraryCategory { Id = 6, Name = "Pantry & Dry Goods", SortOrder = 6 },
                new LibraryCategory { Id = 7, Name = "Beverages", SortOrder = 7 },
                new LibraryCategory { Id = 8, Name = "Snacks & Candy", SortOrder = 8 },
                new LibraryCategory { Id = 9, Name = "Deli & Prepared Foods", SortOrder = 9 },
                new LibraryCategory { Id = 10, Name = "Health & Personal Care", SortOrder = 10 },
                new LibraryCategory { Id = 11, Name = "Baby & Childcare", SortOrder = 11 },
                new LibraryCategory { Id = 12, Name = "Pet Supplies", SortOrder = 12 },
                new LibraryCategory { Id = 13, Name = "Household & Cleaning", SortOrder = 13 },
                new LibraryCategory { Id = 14, Name = "Kitchen & Dining", SortOrder = 14 },
                new LibraryCategory { Id = 15, Name = "Home & Furniture", SortOrder = 15 },
                new LibraryCategory { Id = 16, Name = "Bedding & Bath", SortOrder = 16 },
                new LibraryCategory { Id = 17, Name = "Apparel & Accessories", SortOrder = 17 },
                new LibraryCategory { Id = 18, Name = "Shoes", SortOrder = 18 },
                new LibraryCategory { Id = 19, Name = "Tools & Hardware", SortOrder = 19 },
                new LibraryCategory { Id = 20, Name = "Garden & Outdoor", SortOrder = 20 },
                new LibraryCategory { Id = 21, Name = "Electronics", SortOrder = 21 },
                new LibraryCategory { Id = 22, Name = "Appliances", SortOrder = 22 },
                new LibraryCategory { Id = 23, Name = "Sports & Fitness", SortOrder = 23 },
                new LibraryCategory { Id = 24, Name = "Books, Movies & Music", SortOrder = 24 },
                new LibraryCategory { Id = 25, Name = "Toys & Games", SortOrder = 25 },
                new LibraryCategory { Id = 26, Name = "Automotive", SortOrder = 26 },
                new LibraryCategory { Id = 27, Name = "Office & School Supplies", SortOrder = 27 },
                new LibraryCategory { Id = 28, Name = "Pharmacy & Wellness", SortOrder = 28 },
                new LibraryCategory { Id = 29, Name = "Arts & Crafts", SortOrder = 29 },
                new LibraryCategory { Id = 30, Name = "Party & Seasonal", SortOrder = 30 },
                new LibraryCategory { Id = 31, Name = "Services & Skills", SortOrder = 31 },
                new LibraryCategory { Id = 32, Name = "Plumbing", SortOrder = 32 },
                new LibraryCategory { Id = 33, Name = "Electrical Work", SortOrder = 33 },
                new LibraryCategory { Id = 34, Name = "HVAC & AC", SortOrder = 34 },
                new LibraryCategory { Id = 35, Name = "House Cleaning", SortOrder = 35 },
                new LibraryCategory { Id = 36, Name = "Yard Work & Landscaping", SortOrder = 36 },
                new LibraryCategory { Id = 37, Name = "Child Care", SortOrder = 37 },
                new LibraryCategory { Id = 38, Name = "Car Maintenance & Repair", SortOrder = 38 },
                new LibraryCategory { Id = 39, Name = "Home Renovations", SortOrder = 39 },
                new LibraryCategory { Id = 40, Name = "Planning & Design", SortOrder = 40 },
                new LibraryCategory { Id = 41, Name = "Physical Training & Coaching", SortOrder = 41 },
                new LibraryCategory { Id = 99, Name = "Other", SortOrder = 99 });
        });

        modelBuilder.Entity<LibraryOffering>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TitleNormalized).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DescriptionPreview).HasMaxLength(200);
            entity.Property(e => e.UnitLabel).HasMaxLength(64);
            entity.Property(e => e.ThumbnailResourceId).HasMaxLength(64);
            entity.Property(e => e.ValuePerUnit).HasPrecision(18, 2);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.Crew)
                .WithMany()
                .HasForeignKey(e => e.CrewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatorUser)
                .WithMany()
                .HasForeignKey(e => e.CreatorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LibraryOfferingCategory>(entity =>
        {
            entity.HasKey(e => new { e.OfferingId, e.CategoryId });
            entity.HasOne(e => e.Offering)
                .WithMany(o => o.Categories)
                .HasForeignKey(e => e.OfferingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LibraryUnit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BrokenPendingConfirmation).HasDefaultValue(false);
            entity.Property(e => e.IsRetired).HasDefaultValue(false);
            entity.HasOne(e => e.Offering)
                .WithMany(o => o.Units)
                .HasForeignKey(e => e.OfferingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CurrentPossessorUser)
                .WithMany()
                .HasForeignKey(e => e.CurrentPossessorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LibraryRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PurposePreview).HasMaxLength(200);
            entity.HasOne(e => e.Unit)
                .WithMany(u => u.Requests)
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.RequesterUser)
                .WithMany()
                .HasForeignKey(e => e.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.UnitId, e.RequesterUserId, e.Status });
        });

        modelBuilder.Entity<LibraryRequestMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Request)
                .WithMany(r => r.Messages)
                .HasForeignKey(e => e.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.RequestId, e.CreatedAt });
        });

        modelBuilder.Entity<LibraryMaintenanceRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cost).HasPrecision(18, 2);
            entity.HasOne(e => e.Unit)
                .WithMany(u => u.MaintenanceRecords)
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ContributorUser)
                .WithMany()
                .HasForeignKey(e => e.ContributorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
