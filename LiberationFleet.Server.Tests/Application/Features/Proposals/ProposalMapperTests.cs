using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Tests.Application.Features.Proposals;

public class ProposalMapperTests
{
    [Fact]
    public void RefreshTargetDisplayNames_ClipsJoinFieldsThatWouldOverflowAnonymousAliasSave()
    {
        var longName = new string('A', 256);
        var join = new ProposalCrewJoinRequest
        {
            ApplicantUserId = 9,
            ApplicantUsername = "short",
            Title = "Allow short to join",
            Description = "short wants to join."
        };

        ProposalMapper.RefreshTargetDisplayNames(
            new Dictionary<int, string> { [9] = longName },
            joinRequest: join);

        join.ApplicantUsername.Should().HaveLength(ProposalMapper.FrozenUsernameMaxLength);
        join.Title.Should().HaveLength(ProposalMapper.TitleMaxLength);
        join.Title.Should().StartWith("Allow A");
        join.Description.Should().StartWith(longName);
    }

    [Fact]
    public void RefreshTargetDisplayNames_DoesNotThrowWhenKickTitleIsNull()
    {
        var kick = new ProposalCrewmateKick
        {
            TargetUserId = 4,
            AnonymousNickname = "  ",
            Title = null!,
            Description = null!
        };

        var act = () => ProposalMapper.RefreshTargetDisplayNames(
            new Dictionary<int, string> { [4] = "Ada Lovelace" },
            kick: kick);

        act.Should().NotThrow();
    }

    [Fact]
    public void MapListItem_MapsCrewRuleChangeWhenTitleAndDescriptionAreNull()
    {
        var proposal = new Proposal { Id = 3, Kind = ProposalKind.CrewRuleChange, Status = ProposalStatus.Pending };
        var change = new ProposalCrewRuleChange
        {
            Action = CrewRuleProposalAction.Create,
            Title = null!,
            Description = null!
        };

        var dto = ProposalMapper.MapListItem(proposal, envelope: null, crewRuleChange: change);

        dto.HasPlaintextContent.Should().BeTrue();
        dto.Title.Should().Be(CrewRuleChangeDescriber.CreateTitle);
        dto.DescriptionPreview.Should().Contain("encrypted");
    }

    [Fact]
    public void IndexByResourceId_KeepsFirstEnvelopeWhenResourceIdsCollide()
    {
        var first = new EncryptedContentEnvelope { ResourceId = "12", Nonce = "n1", Ciphertext = "c1" };
        var duplicate = new EncryptedContentEnvelope { ResourceId = "12", Nonce = "n2", Ciphertext = "c2" };

        var indexed = ProposalMapper.IndexByResourceId([first, duplicate]);

        indexed.Should().ContainKey("12");
        indexed["12"].Nonce.Should().Be("n1");
    }
}
