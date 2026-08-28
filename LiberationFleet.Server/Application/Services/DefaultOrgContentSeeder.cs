using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Services;

/// <summary>
/// Seeds default chat rooms and public rules when a crew or fleet is created.
/// Defaults are editable/deletable afterward.
/// </summary>
public class DefaultOrgContentSeeder(
    IChatRepository chatRepository,
    IRuleRepository ruleRepository,
    IFleetRepository fleetRepository)
{
    public async Task SeedCrewDefaultsAsync(Crew crew, int createdByUserId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await AddGeneralRoomsAsync(
            crewId: crew.Id,
            fleetId: null,
            createdByUserId,
            now,
            cancellationToken);

        foreach (var (title, body) in DefaultPublicRules)
        {
            await ruleRepository.AddAsync(new CrewRule
            {
                CrewId = crew.Id,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsPublic = true,
                IsDeleted = false,
                Title = title,
                Description = body
            }, cancellationToken);
        }
    }

    public async Task SeedFleetDefaultsAsync(Fleet fleet, int createdByUserId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await AddGeneralRoomsAsync(
            crewId: null,
            fleetId: fleet.Id,
            createdByUserId,
            now,
            cancellationToken);

        foreach (var (title, body) in DefaultPublicRules)
        {
            await fleetRepository.AddRuleAsync(new FleetRule
            {
                FleetId = fleet.Id,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsPublic = true,
                IsDeleted = false,
                Title = title,
                Description = body
            }, cancellationToken);
        }
    }

    private async Task AddGeneralRoomsAsync(
        int? crewId,
        int? fleetId,
        int createdByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await chatRepository.AddRoomAsync(new ChatRoom
        {
            CrewId = crewId,
            FleetId = fleetId,
            Name = "General",
            Purpose = "General discussion",
            RoomType = ChatRoomType.Text,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            LastActivityAt = now,
            SortOrder = 0
        }, cancellationToken);

        await chatRepository.AddRoomAsync(new ChatRoom
        {
            CrewId = crewId,
            FleetId = fleetId,
            Name = "General",
            Purpose = "General voice channel",
            RoomType = ChatRoomType.Voice,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            LastActivityAt = now,
            SortOrder = 1
        }, cancellationToken);
    }

    public static IReadOnlyList<(string Title, string Body)> DefaultPublicRules { get; } =
    [
        (
            "The social contract of tolerance",
            "All who tolerate others who are not engaged in or perpetuating physical based, psychological based, financial based, resource based, reputation based, intimidation based, or other provable nonconsensual destructive harm should themselves be tolerated. Otherwise violators may face repercussions including but not limited to being educated, being kicked from the crew/fleet, having certain app-based privileges revoked, loss of a role, legal action, or whatever else the community deems appropriate."
        ),
        (
            "The social contract of non-violence",
            "All who refrain from engaging in nonconsensual violent behavior should themselves not be subject to violent behavior. Otherwise violators may be subject to however much restriction or violent retaliation is sufficient to stop the violent violator of this contract. Any degree of violence or detention which exceeds sufficiency may too be treated as a violation of the social contract of non-violence. Violators will also be subject to the repercussions for violating the social contract of tolerance."
        ),
        (
            "Consent policy",
            """
            Consent is the accessibility of autonomy and can manifest when one entity grants permission to another entity to do something pertaining to them such as, but not limited to, act upon them, act on behalf of them, acquire their personal information, disclose their personal information, leverage their identity for promotion, condemnation, or commentary, cross their boundaries

            Consent must be provided for any of the above sort of actions to be taken and can be withheld for any reason at any time.

            Consent cannot be given implicitly through the way a person is dressed, behaving, dancing, or whatever else. However, should there be explicit mutual understanding between parties that certain dress, behavior, dancing, or whatever else, is an expression of consent, then these things can be used.

            Consent can, however, be implicitly withheld through a person’s visible discomfort, tone of voice, hesitancy, and so on. These things may not always be an indication of consent being withheld as it can vary based upon a person’s neurotype, sexuality, personality, or past communication. It is important that you know your partner well enough to know what to expect consent from them to look like.

            Consent can be indirectly withheld through displays of discomfort or a lack of enthusiasm.

            As a rule of thumb, if we are unsure, stop, ask, and respect.

            People who are incapable of consent include minors, anyone whose consequential reasoning portions of their brain have not fully matured, anyone who is not fully aware of what they are consenting to, anyone whose reasoning is inhibited due to substances, exhaustion, fear, discomfort, hunger, or whatever else might inhibit judgment making, anyone who fears that withholding consent will come at any amount of cost to them. That cost could be but is not limited to finances, safety, physical wellness, mental health, relationships, or anything else, anyone who is unaware that they are being asked for consent.

            In most instances where both parties are capable of consent, consent must be given before action can be taken.

            In all instances where consent cannot be given by either party, the inability to consent should be treated the same as consent being withheld. No actions which require consent should be taken.

            Consent is only optional under the following circumstances:

            Exposing abusive behaviors through the disclosure of private information.

            The guardian of a minor permitting their child to receive needed healthcare.

            The trusted caretakers of an adult with chronically impaired judgment, permitting that adult to receive needed healthcare.

            Criticism of a public figure’s public behavior, public comments, or public information (especially individuals in positions of power/influence)

            Finally, when a person is in a situation where disclosing information about some non-destructively harmful truth, which would cause another to withhold consent, might put them or another at risk of non-consensual destructive harm. We should take great measures to avoid finding ourselves in this sort of situation if at all possible.

            Violators of consent are in violation of the social contract of tolerance.
            """.Trim()
        ),
        (
            "Gift policy",
            "All financial contributions recorded to the gift log are nontransactional.  This means that no matter how much you have given, no one will ever owe you anything as a result. Your reward is a higher priority score which may entitle you to receive aid, goods, and services sooner than crewmates with lower priority scores (exceptions do exist). This policy also means no matter how much you have received via mutual aid or the library of things, you will never owe anyone anything in return. Consequences to not contributing may include a decrease in the amount of periodic concentrated aid and potential removal from the giving season."
        ),
        (
            "LoT Policy",
            "The Library of Things is not a market place where transactions are made for items of equal value. The Library of Things (LoT) functions in the same manner as the giving season. Each acquisition of a good or service provided is a gift that reflects in the provider’s priority score. There is not to be any transactionary behavior within the bounds of the LoT system."
        ),
        (
            "Spending Policy",
            "Once you have received a gift, that money is yours to do with as you see fit. Whether you spend the money to better your circumstances or use it to enjoy life a bit more, that is your choice. We have to live in order to survive and we have to survive in order to live. It is up to each person to decide what that balance looks like for themselves so long as it doesn’t violate any of the social contracts."
        ),
        (
            "Wealth policy",
            "Any ultra wealthy members who are caught requesting aid will be subject to removal from the crew/fleet."
        )
    ];
}
