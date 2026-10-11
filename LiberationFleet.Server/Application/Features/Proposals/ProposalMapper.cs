using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalMapper
{
    public const string AnonymousAuthor = "Anonymous";

    public static ProposalListItemDto MapListItem(
        Proposal proposal,
        EncryptedContentEnvelope? envelope,
        ProposalCrewSettingChange? crewSettingChange = null,
        ProposalCrewRuleChange? crewRuleChange = null,
        ProposalCrewChatChange? crewChatChange = null,
        ProposalCrewmateKick? crewmateKick = null,
        ProposalCrewmateRejoin? crewmateRejoin = null,
        ProposalCrewJoinRequest? crewJoinRequest = null,
        ProposalCrewRoleChange? crewRoleChange = null,
        ProposalClaimPlaceholderIdentity? claimPlaceholderIdentity = null,
        ProposalCrewmatePermissionGrant? crewmatePermissionGrant = null,
        ProposalCrewmateAidStatChange? crewmateAidStatChange = null,
        string? currentUserVote = null,
        ProposalFleetRuleChange? fleetRuleChange = null,
        ProposalFleetSettingChange? fleetSettingChange = null,
        ProposalFleetJoinRequest? fleetJoinRequest = null,
        ProposalFleetKickCrew? fleetKickCrew = null,
        ProposalCrewApplyToFleet? crewApplyToFleet = null,
        ProposalCrewLeaveFleet? crewLeaveFleet = null,
        ProposalCrewStartSeason? crewStartSeason = null,
        ProposalFleetNotice? fleetNotice = null)
    {
        var dto = new ProposalListItemDto
        {
            Id = proposal.Id,
            AuthorUserId = 0,
            AuthorUsername = AnonymousAuthor,
            LastActivityAt = proposal.LastActivityAt,
            Status = proposal.Status.ToString(),
            Kind = proposal.Kind.ToString(),
            ApproveCount = proposal.ApproveCount,
            DisapproveCount = proposal.DisapproveCount,
            ApprovalTimerEndsAt = proposal.ApprovalTimerEndsAt,
            CurrentUserVote = currentUserVote
        };

        if (crewStartSeason is not null)
        {
            ApplyPlaintext(dto, crewStartSeason.Title, crewStartSeason.Description);
            return dto;
        }

        if (crewmateKick is not null)
        {
            ApplyPlaintext(dto, crewmateKick.Title, crewmateKick.Description);
            return dto;
        }

        if (crewJoinRequest is not null)
        {
            ApplyPlaintext(dto, crewJoinRequest.Title, crewJoinRequest.Description);
            return dto;
        }

        if (crewRoleChange is not null)
        {
            ApplyPlaintext(dto, crewRoleChange.Title, crewRoleChange.Description);
            return dto;
        }

        if (claimPlaceholderIdentity is not null)
        {
            ApplyPlaintext(dto, claimPlaceholderIdentity.Title, claimPlaceholderIdentity.Description);
            return dto;
        }

        if (crewmatePermissionGrant is not null)
        {
            ApplyPlaintext(dto, crewmatePermissionGrant.Title, crewmatePermissionGrant.Description);
            return dto;
        }

        if (crewmateAidStatChange is not null)
        {
            ApplyPlaintext(dto, crewmateAidStatChange.Title, crewmateAidStatChange.Description);
            return dto;
        }

        if (crewmateRejoin is not null)
        {
            ApplyPlaintext(dto, crewmateRejoin.Title, crewmateRejoin.Description);
            return dto;
        }

        if (crewChatChange is not null)
        {
            ApplyPlaintext(dto, crewChatChange.Title, crewChatChange.Description);
            return dto;
        }

        if (crewRuleChange is not null)
        {
            ApplyPlaintext(dto, crewRuleChange.Title, crewRuleChange.Description);
            return dto;
        }

        if (fleetRuleChange is not null)
        {
            ApplyPlaintext(
                dto,
                string.IsNullOrWhiteSpace(fleetRuleChange.RuleTitle) ? fleetRuleChange.Title : fleetRuleChange.RuleTitle,
                string.IsNullOrWhiteSpace(fleetRuleChange.RuleDescription) ? fleetRuleChange.Description : fleetRuleChange.RuleDescription);
            return dto;
        }

        if (fleetSettingChange is not null)
        {
            ApplyPlaintext(dto, fleetSettingChange.Title, fleetSettingChange.Description);
            dto.SettingField = fleetSettingChange.Field.ToString();
            dto.SettingNewValue = fleetSettingChange.NewValue;
            return dto;
        }

        if (fleetJoinRequest is not null)
        {
            ApplyPlaintext(dto, fleetJoinRequest.Title, fleetJoinRequest.Description);
            return dto;
        }

        if (fleetKickCrew is not null)
        {
            ApplyPlaintext(dto, fleetKickCrew.Title, fleetKickCrew.Description);
            return dto;
        }

        if (crewApplyToFleet is not null)
        {
            ApplyPlaintext(dto, crewApplyToFleet.Title, crewApplyToFleet.Description);
            return dto;
        }

        if (crewLeaveFleet is not null)
        {
            ApplyPlaintext(dto, crewLeaveFleet.Title, crewLeaveFleet.Description);
            return dto;
        }

        if (fleetNotice is not null)
        {
            ApplyPlaintext(dto, fleetNotice.Title, fleetNotice.Description);
            return dto;
        }

        if (crewSettingChange is not null)
        {
            ApplyPlaintext(dto, crewSettingChange.Title, crewSettingChange.Description);
            dto.SettingField = crewSettingChange.Field.ToString();
            dto.SettingNewValue = crewSettingChange.NewValue;
            return dto;
        }

        if (envelope is not null)
        {
            dto.HasEncryptedContent = true;
            dto.EncryptedPayload = CryptoMapper.MapPayload(envelope);
        }

        return dto;
    }

    public static ProposalDetailDto MapDetail(
        Proposal proposal,
        EncryptedContentEnvelope? envelope,
        IReadOnlyList<ProposalCommentDto> comments,
        int viewerUserId,
        ProposalCrewSettingChange? crewSettingChange = null,
        ProposalCrewRuleChange? crewRuleChange = null,
        ProposalCrewChatChange? crewChatChange = null,
        ProposalCrewmateKick? crewmateKick = null,
        ProposalCrewmateRejoin? crewmateRejoin = null,
        ProposalCrewJoinRequest? crewJoinRequest = null,
        ProposalCrewRoleChange? crewRoleChange = null,
        ProposalClaimPlaceholderIdentity? claimPlaceholderIdentity = null,
        ProposalCrewmatePermissionGrant? crewmatePermissionGrant = null,
        ProposalCrewmateAidStatChange? crewmateAidStatChange = null,
        string? currentUserVote = null,
        string? viewerAlias = null,
        int aliasRerollsRemaining = 0,
        ProposalFleetRuleChange? fleetRuleChange = null,
        ProposalFleetSettingChange? fleetSettingChange = null,
        ProposalFleetJoinRequest? fleetJoinRequest = null,
        ProposalFleetKickCrew? fleetKickCrew = null,
        ProposalCrewApplyToFleet? crewApplyToFleet = null,
        ProposalCrewLeaveFleet? crewLeaveFleet = null,
        ProposalCrewStartSeason? crewStartSeason = null,
        ProposalFleetNotice? fleetNotice = null)
    {
        var listItem = MapListItem(
            proposal,
            envelope,
            crewSettingChange,
            crewRuleChange,
            crewChatChange,
            crewmateKick,
            crewmateRejoin,
            crewJoinRequest,
            crewRoleChange,
            claimPlaceholderIdentity,
            crewmatePermissionGrant,
            crewmateAidStatChange,
            currentUserVote,
            fleetRuleChange,
            fleetSettingChange,
            fleetJoinRequest,
            fleetKickCrew,
            crewApplyToFleet,
            crewLeaveFleet,
            crewStartSeason,
            fleetNotice);
        var isSystemProposal = IsSystemProposal(proposal.Kind);
        var plaintextDescription = crewmateKick?.Description
            ?? crewJoinRequest?.Description
            ?? crewRoleChange?.Description
            ?? claimPlaceholderIdentity?.Description
            ?? crewmatePermissionGrant?.Description
            ?? crewmateAidStatChange?.Description
            ?? crewmateRejoin?.Description
            ?? crewChatChange?.Description
            ?? crewRuleChange?.Description
            ?? (string.IsNullOrWhiteSpace(fleetRuleChange?.RuleDescription)
                ? fleetRuleChange?.Description
                : fleetRuleChange.RuleDescription)
            ?? fleetSettingChange?.Description
            ?? fleetJoinRequest?.Description
            ?? fleetKickCrew?.Description
            ?? crewApplyToFleet?.Description
            ?? crewLeaveFleet?.Description
            ?? crewStartSeason?.Description
            ?? fleetNotice?.Description
            ?? crewSettingChange?.Description;
        var usesAnonymousComments = true;
        var canKickAuthor = proposal.Kind == ProposalKind.General
            && !proposal.FleetId.HasValue
            && proposal.AuthorUserId != viewerUserId;

        var isKickVoteTarget = crewmateKick is not null && crewmateKick.TargetUserId == viewerUserId;

        return new ProposalDetailDto
        {
            Id = listItem.Id,
            AuthorUserId = listItem.AuthorUserId,
            AuthorUsername = listItem.AuthorUsername,
            AuthorAvatarResourceId = listItem.AuthorAvatarResourceId,
            LastActivityAt = listItem.LastActivityAt,
            Status = listItem.Status,
            ApproveCount = listItem.ApproveCount,
            DisapproveCount = listItem.DisapproveCount,
            ApprovalTimerEndsAt = listItem.ApprovalTimerEndsAt,
            HasEncryptedContent = listItem.HasEncryptedContent,
            HasPlaintextContent = listItem.HasPlaintextContent,
            Title = listItem.Title,
            DescriptionPreview = listItem.DescriptionPreview,
            EncryptedPayload = listItem.EncryptedPayload,
            CurrentUserVote = listItem.CurrentUserVote,
            SettingField = listItem.SettingField,
            SettingNewValue = listItem.SettingNewValue,
            CreatedAt = proposal.CreatedAt,
            Description = plaintextDescription,
            CanEdit = !isSystemProposal && proposal.AuthorUserId == viewerUserId && !proposal.FleetId.HasValue,
            CanDelete = proposal.AuthorUserId == viewerUserId && proposal.Status == ProposalStatus.Pending,
            UsesAnonymousComments = usesAnonymousComments,
            CanKickAuthor = canKickAuthor,
            ViewerAlias = viewerAlias,
            AliasRerollsRemaining = aliasRerollsRemaining,
            IsKickVoteTarget = isKickVoteTarget,
            CanVote = proposal.Status == ProposalStatus.Pending && !isKickVoteTarget,
            Comments = comments
        };
    }

    public static ProposalCommentDto MapComment(
        ProposalComment comment,
        EncryptedContentEnvelope? envelope,
        int replyCount,
        int viewerUserId,
        bool usesAnonymousComments,
        IReadOnlyDictionary<int, string> nicknameByUserId,
        string? replyToUsername = null)
    {
        var isOwn = comment.AuthorUserId == viewerUserId;
        nicknameByUserId.TryGetValue(comment.AuthorUserId, out var nickname);

        return new ProposalCommentDto
        {
            Id = comment.Id,
            AuthorUserId = usesAnonymousComments ? 0 : comment.AuthorUserId,
            AuthorUsername = usesAnonymousComments && !string.IsNullOrWhiteSpace(nickname)
                ? nickname
                : usesAnonymousComments
                    ? AnonymousAuthor
                    : UserDisplay.Name(comment.AuthorUser),
            AuthorAvatarResourceId = usesAnonymousComments ? null : comment.AuthorUser?.AvatarResourceId,
            ParentCommentId = comment.ParentCommentId,
            ReplyToCommentId = comment.ReplyToCommentId,
            ReplyToUsername = replyToUsername,
            CreatedAt = comment.CreatedAt,
            ReplyCount = replyCount,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            Body = comment.Body,
            IsOwnComment = isOwn,
            CanKick = usesAnonymousComments && !isOwn
        };
    }

    public static ProposalStatus ParseStatus(string status) =>
        Enum.TryParse<ProposalStatus>(status, true, out var parsed) ? parsed : ProposalStatus.Pending;

    public static bool IsSystemProposal(ProposalKind kind) =>
        kind is ProposalKind.CrewSettingChange
            or ProposalKind.CrewRuleChange
            or ProposalKind.CrewChatChange
            or ProposalKind.CrewmateKick
            or ProposalKind.CrewmateSeasonKick
            or ProposalKind.CrewmateRejoin
            or ProposalKind.CrewJoinRequest
            or ProposalKind.CrewRoleChange
            or ProposalKind.ClaimPlaceholderIdentity
            or ProposalKind.CrewmatePermissionGrant
            or ProposalKind.CrewmateAidStatChange
            or ProposalKind.CrewApplyToFleet
            or ProposalKind.CrewLeaveFleet
            or ProposalKind.CrewStartSeason
            or ProposalKind.FleetJoinRequest
            or ProposalKind.FleetSettingChange
            or ProposalKind.FleetKickCrew
            or ProposalKind.FleetChatChange
            or ProposalKind.FleetRuleChange;

    /// <summary>
    /// Crew and fleet governance proposals stay visible even when the viewer and author have blocked each other.
    /// </summary>
    public static bool IsVisibleDespiteBlock(ProposalKind kind) =>
        IsSystemProposal(kind);

    private static void ApplyPlaintext(ProposalListItemDto dto, string title, string description)
    {
        dto.HasPlaintextContent = true;
        dto.Title = title;
        dto.DescriptionPreview = description;
    }

    /// <summary>
    /// Rewrites frozen proposal copy that was stored with Username so list/detail show DisplayName.
    /// Safe to call on tracked entities (persists on the next SaveChanges).
    /// </summary>
    public static void RefreshTargetDisplayNames(
        IReadOnlyDictionary<int, string> displayNamesByUserId,
        ProposalCrewmateAidStatChange? aidStat = null,
        ProposalCrewRoleChange? roleChange = null,
        ProposalCrewmatePermissionGrant? permissionGrant = null,
        ProposalCrewJoinRequest? joinRequest = null,
        ProposalCrewmateRejoin? rejoin = null,
        ProposalCrewmateKick? kick = null,
        ProposalClaimPlaceholderIdentity? claim = null)
    {
        if (aidStat is not null && displayNamesByUserId.TryGetValue(aidStat.TargetUserId, out var aidName))
        {
            aidStat.Title = $"Update aid stats for {aidName}";
            aidStat.Description = ReplaceNameAfterPrefix(
                aidStat.Description,
                "Adjust mutual-aid accounting for ",
                aidName);
        }

        if (roleChange is not null && displayNamesByUserId.TryGetValue(roleChange.TargetUserId, out var roleName))
        {
            if (roleChange.Title.StartsWith("Nominate ", StringComparison.Ordinal))
            {
                var asIdx = roleChange.Title.LastIndexOf(" as ", StringComparison.Ordinal);
                if (asIdx > 0)
                {
                    roleChange.Title = $"Nominate {roleName}{roleChange.Title[asIdx..]}";
                }
            }
            else if (roleChange.Title.StartsWith("Remove ", StringComparison.Ordinal)
                     && roleChange.Title.Contains(" from ", StringComparison.Ordinal))
            {
                var fromIdx = roleChange.Title.LastIndexOf(" from ", StringComparison.Ordinal);
                if (fromIdx > 0)
                {
                    roleChange.Title = $"{roleChange.Title[..(fromIdx + 6)]}{roleName}";
                }
            }

            roleChange.Description = roleChange.Description.Contains(" from ", StringComparison.Ordinal)
                ? ReplaceNameAfterPrefix(roleChange.Description, "from ", roleName, untilColonOrPeriod: true)
                : ReplaceNameAfterPrefix(roleChange.Description, "Nominate ", roleName, untilColonOrPeriod: true);
        }

        if (permissionGrant is not null
            && displayNamesByUserId.TryGetValue(permissionGrant.TargetUserId, out var grantName))
        {
            permissionGrant.Title = ReplaceNameAfterPrefix(permissionGrant.Title, " to ", grantName);
            permissionGrant.Description = ReplaceNameAfterPrefix(permissionGrant.Description, "Allow ", grantName);
        }

        if (joinRequest is not null
            && displayNamesByUserId.TryGetValue(joinRequest.ApplicantUserId, out var joinName))
        {
            joinRequest.ApplicantUsername = joinName;
            joinRequest.Title = $"Allow {joinName} to join";
            joinRequest.Description = ReplaceNameAfterPrefix(joinRequest.Description, prefix: null, joinName);
        }

        if (rejoin is not null && displayNamesByUserId.TryGetValue(rejoin.TargetUserId, out var rejoinName))
        {
            rejoin.Username = rejoinName;
            rejoin.Title = $"Allow {rejoinName} to rejoin";
            rejoin.Description = ReplaceNameAfterPrefix(rejoin.Description, "Allow ", rejoinName);
        }

        if (kick is not null
            && displayNamesByUserId.TryGetValue(kick.TargetUserId, out var kickName)
            && string.IsNullOrWhiteSpace(kick.AnonymousNickname))
        {
            if (kick.Title.StartsWith("Kick ", StringComparison.Ordinal))
            {
                kick.Title = $"Kick {kickName}";
            }
            else if (kick.Title.StartsWith("Remove ", StringComparison.Ordinal)
                     && kick.Title.Contains(" from season", StringComparison.Ordinal))
            {
                kick.Title = $"Remove {kickName} from season";
            }

            kick.Description = ReplaceNameAfterPrefix(kick.Description, "Remove ", kickName);
        }

        if (claim is not null
            && displayNamesByUserId.TryGetValue(claim.PlaceholderUserId, out var claimName))
        {
            claim.PlaceholderDisplayName = claimName;
            claim.Title = $"Claim identity of {claimName}";
            claim.Description = ReplaceNameAfterPrefix(claim.Description, prefix: null, claimName);
        }
    }

    private static string ReplaceNameAfterPrefix(
        string text,
        string? prefix,
        string displayName,
        bool untilColonOrPeriod = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (prefix is null)
        {
            // Replace a leading token up to space/colon (first word) when it looks like a username line.
            var end = text.IndexOfAny([' ', ':', '\n', '\r']);
            if (end <= 0)
            {
                return displayName;
            }

            return displayName + text[end..];
        }

        var idx = text.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0)
        {
            return text;
        }

        var nameStart = idx + prefix.Length;
        var rest = text[nameStart..];
        var nameEnd = untilColonOrPeriod
            ? rest.IndexOfAny([':', '.', '\n', '\r'])
            : rest.IndexOfAny([':', '\n', '\r']);
        if (nameEnd < 0)
        {
            return text[..nameStart] + displayName;
        }

        return text[..nameStart] + displayName + rest[nameEnd..];
    }
}
