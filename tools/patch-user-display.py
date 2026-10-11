from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

REPLACEMENTS = [
    (
        "LiberationFleet.Server/Application/Features/Library/LibraryMapper.cs",
        [
            (
                "HolderUsername = request.Unit.CurrentPossessorUser.Username,",
                "HolderUsername = UserDisplay.Name(request.Unit.CurrentPossessorUser),",
            ),
            (
                "RequesterUsername = request.RequesterUser?.Username ?? string.Empty,",
                "RequesterUsername = UserDisplay.Name(request.RequesterUser),",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Library/LibraryTaskMapper.cs",
        [
            (
                'CreatorUsername = task.CreatorUser?.Username ?? "Crewmate",',
                'CreatorUsername = string.IsNullOrWhiteSpace(UserDisplay.Name(task.CreatorUser)) ? "Crewmate" : UserDisplay.Name(task.CreatorUser),',
            ),
            (
                "ClaimedByUsername = instance.ClaimedByUser?.Username,",
                "ClaimedByUsername = instance.ClaimedByUser is null ? null : UserDisplay.Name(instance.ClaimedByUser),",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Fleets/FleetMapper.cs",
        [
            (
                "CreatedByUsername = rule.CreatedByUser.Username,",
                "CreatedByUsername = UserDisplay.Name(rule.CreatedByUser),",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Rules/RuleMapper.cs",
        [
            (
                "CreatedByUsername = rule.IsPublic || envelope is null ? rule.CreatedByUser.Username : string.Empty,",
                "CreatedByUsername = rule.IsPublic || envelope is null ? UserDisplay.Name(rule.CreatedByUser) : string.Empty,",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Gifts/GiftHistoryMapper.cs",
        [
            (
                "MiddlemanUsername = gift.MiddlemanUser?.Username,",
                "MiddlemanUsername = gift.MiddlemanUser is null ? null : UserDisplay.Name(gift.MiddlemanUser),",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Gifts/GiftEngagementMapper.cs",
        [
            (
                "AuthorUsername = envelope is null ? comment.AuthorUser.Username : string.Empty,",
                "AuthorUsername = envelope is null ? UserDisplay.Name(comment.AuthorUser) : string.Empty,",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Chats/Voice/VoicePresenceMapper.cs",
        [
            (
                "Username = session.User?.Username ?? string.Empty,",
                "Username = UserDisplay.Name(session.User),",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Friends/FriendAccessHelper.cs",
        [
            (
                "AuthorUsername = envelope is null ? message.AuthorUser?.Username ?? string.Empty : string.Empty,",
                "AuthorUsername = envelope is null ? UserDisplay.Name(message.AuthorUser) : string.Empty,",
            ),
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Friends/Queries/SearchUsers/SearchUsersQuery.cs",
        [("Username = user.Username,", "Username = UserDisplay.Name(user),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Friends/Queries/GetFriends/GetFriendsQuery.cs",
        [("Username = friend.Username,", "Username = UserDisplay.Name(friend),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Friends/Queries/GetFriendRequests/GetFriendRequestsQuery.cs",
        [("Username = otherUser.Username,", "Username = UserDisplay.Name(otherUser),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Friends/Queries/GetBlockedUsers/GetBlockedUsersQuery.cs",
        [("Username = b.Blocked!.Username,", "Username = UserDisplay.Name(b.Blocked),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Crews/Queries/GetMyCrewInvitations/GetMyCrewInvitationsQuery.cs",
        [("InviterUsername = i.InviterUser.Username,", "InviterUsername = UserDisplay.Name(i.InviterUser),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Crews/Queries/GetCrewInvitation/GetCrewInvitationQuery.cs",
        [("InviterUsername = invitation.InviterUser.Username,", "InviterUsername = UserDisplay.Name(invitation.InviterUser),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/EmergencyRequests/Queries/GetEmergencyRequests/GetEmergencyRequestsQuery.cs",
        [("RequesterUsername = r.RequesterUser.Username,", "RequesterUsername = UserDisplay.Name(r.RequesterUser),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Fleets/Queries/GetFleetEmergencies/GetFleetEmergenciesQuery.cs",
        [("RequesterUsername = r.RequesterUser.Username,", "RequesterUsername = UserDisplay.Name(r.RequesterUser),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Fleets/Queries/GetFleetCrewmateProfile/GetFleetCrewmateProfileQuery.cs",
        [("Username = target.Username,", "Username = UserDisplay.Name(target),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Crews/CrewJoinRequestProposalService.cs",
        [("ApplicantUsername = applicant.Username,", "ApplicantUsername = UserDisplay.Name(applicant),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Crews/CrewmateKickProposalService.cs",
        [("kick.RevealedUsername = targetUser.Username;", "kick.RevealedUsername = UserDisplay.Name(targetUser);")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Gifts/Queries/GetMyGiftHistoryForRecipient/GetMyGiftHistoryForRecipientQuery.cs",
        [("RecipientUsername = recipient.Username,", "RecipientUsername = UserDisplay.Name(recipient),")],
    ),
    (
        "LiberationFleet.Server/Application/Features/Library/Commands/RecordLibraryAcquisition/RecordLibraryAcquisitionCommand.cs",
        [
            (
                'var acquirerUsername = acquirer?.Username ?? "Crewmate";',
                'var acquirerUsername = string.IsNullOrWhiteSpace(UserDisplay.Name(acquirer)) ? "Crewmate" : UserDisplay.Name(acquirer);',
            )
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Library/Commands/CompleteLibraryRequest/CompleteLibraryRequestCommand.cs",
        [
            (
                'var completerUsername = libraryRequest.Unit.CurrentPossessorUser?.Username ?? "Crewmate";',
                'var completerUsername = string.IsNullOrWhiteSpace(UserDisplay.Name(libraryRequest.Unit.CurrentPossessorUser)) ? "Crewmate" : UserDisplay.Name(libraryRequest.Unit.CurrentPossessorUser);',
            )
        ],
    ),
    (
        "LiberationFleet.Server/Application/Features/Library/LibraryRequestPriorityService.cs",
        [
            (
                "dto.HigherPriorityRequesterUsername = highest.Request.RequesterUser?.Username ?? string.Empty;",
                "dto.HigherPriorityRequesterUsername = UserDisplay.Name(highest.Request.RequesterUser);",
            ),
            (
                "detail.HigherPriorityRequesterUsername = highest.RequesterUser?.Username ?? string.Empty;",
                "detail.HigherPriorityRequesterUsername = UserDisplay.Name(highest.RequesterUser);",
            ),
        ],
    ),
]


def ensure_using(text: str) -> str:
    if "using LiberationFleet.Server.Application.Common;" in text:
        return text
    if "UserDisplay" not in text:
        return text
    lines = text.splitlines(True)
    insert_at = 0
    for i, line in enumerate(lines):
        if line.startswith("using "):
            insert_at = i + 1
    lines.insert(insert_at, "using LiberationFleet.Server.Application.Common;\n")
    return "".join(lines)


def main() -> None:
    for rel, pairs in REPLACEMENTS:
        path = ROOT / rel
        text = path.read_text(encoding="utf-8")
        for old, new in pairs:
            count = text.count(old)
            if count == 0:
                print(f"MISS {rel}: {old[:70]}")
            else:
                text = text.replace(old, new)
                print(f"OK   {rel}: x{count}")
        text = ensure_using(text)
        path.write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
