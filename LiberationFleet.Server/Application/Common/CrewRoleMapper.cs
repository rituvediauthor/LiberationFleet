using System.Text.Json;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common;

public static class CrewRoleMapper
{
    public static IReadOnlyList<string> MapRoles(CrewMembership membership)
    {
        var roles = new List<string>();
        if (membership.IsOrganizer)
        {
            roles.Add("Organizer");
        }

        if (membership.IsAdvocate)
        {
            roles.Add("Advocate");
        }

        if (membership.IsDecentralizer)
        {
            roles.Add("Decentralizer");
        }

        if (membership.IsCeremonialOrganizer)
        {
            roles.Add("Ceremonial organizer");
        }

        if (membership.IsModerator)
        {
            roles.Add("Moderator");
        }

        if (membership.IsIntermediary)
        {
            roles.Add("Intermediary");
        }

        if (membership.IsHonoraryMember)
        {
            roles.Add("Honorary member");
        }

        return roles;
    }

    public static string GetRoleKey(CrewRole role) => role.ToString();

    public static IReadOnlyList<CrewRoleDefinitionDto> GetAllRoleDefinitions() =>
        Enum.GetValues<CrewRole>()
            .Select(role => new CrewRoleDefinitionDto
            {
                Role = GetRoleKey(role),
                DisplayName = GetDisplayName(role),
                Description = GetDescription(role)
            })
            .ToList();

    public static IReadOnlyList<CrewmateElectedRoleDto> MapElectedRoleDtos(CrewMembership membership) =>
        MapElectedRoles(membership)
            .Select(role => new CrewmateElectedRoleDto
            {
                Role = GetRoleKey(role),
                DisplayName = GetDisplayName(role)
            })
            .ToList();

    public static IReadOnlyList<CrewRole> MapElectedRoles(CrewMembership membership)
    {
        var roles = new List<CrewRole>();
        if (membership.IsOrganizer)
        {
            roles.Add(CrewRole.Organizer);
        }

        if (membership.IsAdvocate)
        {
            roles.Add(CrewRole.Advocate);
        }

        if (membership.IsDecentralizer)
        {
            roles.Add(CrewRole.Decentralizer);
        }

        if (membership.IsCeremonialOrganizer)
        {
            roles.Add(CrewRole.CeremonialOrganizer);
        }

        if (membership.IsModerator)
        {
            roles.Add(CrewRole.Moderator);
        }

        if (membership.IsIntermediary)
        {
            roles.Add(CrewRole.Intermediary);
        }

        return roles;
    }

    public static bool HasAnyRole(CrewMembership membership) =>
        membership.IsOrganizer
        || membership.IsAdvocate
        || membership.IsDecentralizer
        || membership.IsCeremonialOrganizer
        || membership.IsModerator
        || membership.IsIntermediary
        || membership.IsHonoraryMember;

    public static bool HasRole(CrewMembership membership, CrewRole role) =>
        role switch
        {
            CrewRole.Organizer => membership.IsOrganizer,
            CrewRole.Advocate => membership.IsAdvocate,
            CrewRole.Decentralizer => membership.IsDecentralizer,
            CrewRole.CeremonialOrganizer => membership.IsCeremonialOrganizer,
            CrewRole.Moderator => membership.IsModerator,
            CrewRole.Intermediary => membership.IsIntermediary,
            _ => false
        };

    public static string GetDisplayName(CrewRole role) =>
        role switch
        {
            CrewRole.Organizer => "Organizer",
            CrewRole.Advocate => "Advocate",
            CrewRole.Decentralizer => "Decentralizer",
            CrewRole.CeremonialOrganizer => "Ceremonial organizer",
            CrewRole.Moderator => "Moderator",
            CrewRole.Intermediary => "Intermediary",
            _ => role.ToString()
        };

    public static string GetDescription(CrewRole role) =>
        role switch
        {
            CrewRole.Organizer =>
                "Full crew access including settings. Can leave the role without a vote; nominating a new organizer requires crew approval.",
            CrewRole.Advocate =>
                "Resolve conflict and serve as a mouthpiece for anonymous crew opinions. Can toggle anonymous mode in crew chat channels.",
            CrewRole.Decentralizer =>
                "Save backups of crew records and identify concentrations of power. Can export the gift log and crewmate states.",
            CrewRole.CeremonialOrganizer =>
                "Organize events, celebrations, and ceremonies for the crew. No special app powers.",
            CrewRole.Moderator =>
                "Delete inappropriate file attachments and restrict a crewmate's ability to attach files.",
            CrewRole.Intermediary =>
                "Bridge gifts when giver and recipient do not share a payment platform. Automatically loses the role after failing to complete two gifts.",
            _ => string.Empty
        };

    public static IReadOnlyList<CrewRole> ParseRoles(IEnumerable<string> roleNames)
    {
        var roles = new List<CrewRole>();
        foreach (var name in roleNames)
        {
            if (TryParseRole(name, out var role))
            {
                roles.Add(role);
            }
        }

        return roles.Distinct().ToList();
    }

    public static bool TryParseRole(string value, out CrewRole role)
    {
        var normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "organizer":
                role = CrewRole.Organizer;
                return true;
            case "advocate":
                role = CrewRole.Advocate;
                return true;
            case "decentralizer":
                role = CrewRole.Decentralizer;
                return true;
            case "ceremonialorganizer":
            case "ceremonial organizer":
                role = CrewRole.CeremonialOrganizer;
                return true;
            case "moderator":
                role = CrewRole.Moderator;
                return true;
            case "intermediary":
                role = CrewRole.Intermediary;
                return true;
            default:
                role = default;
                return Enum.TryParse(value, ignoreCase: true, out role);
        }
    }

    public static string SerializeRoles(IEnumerable<CrewRole> roles) =>
        JsonSerializer.Serialize(roles.Distinct().Select(r => r.ToString()).ToList());

    public static IReadOnlyList<CrewRole> DeserializeRoles(string rolesJson)
    {
        if (string.IsNullOrWhiteSpace(rolesJson))
        {
            return [];
        }

        try
        {
            var names = JsonSerializer.Deserialize<List<string>>(rolesJson) ?? [];
            return ParseRoles(names);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void ApplyRoles(CrewMembership membership, IEnumerable<CrewRole> roles, bool assign)
    {
        foreach (var role in roles.Distinct())
        {
            switch (role)
            {
                case CrewRole.Organizer:
                    membership.IsOrganizer = assign;
                    break;
                case CrewRole.Advocate:
                    membership.IsAdvocate = assign;
                    break;
                case CrewRole.Decentralizer:
                    membership.IsDecentralizer = assign;
                    break;
                case CrewRole.CeremonialOrganizer:
                    membership.IsCeremonialOrganizer = assign;
                    break;
                case CrewRole.Moderator:
                    membership.IsModerator = assign;
                    break;
                case CrewRole.Intermediary:
                    membership.IsIntermediary = assign;
                    if (assign)
                    {
                        membership.IntermediaryFailedCompletions = 0;
                    }
                    break;
            }
        }
    }
}
