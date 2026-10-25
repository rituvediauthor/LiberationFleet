using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Tests.Application.Features.Chats;

public class ChatMapperTests
{
    [Fact]
    public void MapMessage_WhenRemoved_HidesContentUnlessModerator()
    {
        var message = new ChatRoomMessage
        {
            Id = 9,
            AuthorUserId = 3,
            Body = "hidden text",
            IsAnonymous = true,
            IsDeleted = true,
            RemovedByModerator = true,
            AuthorUser = new User { Id = 3, Username = "AnonAuthor" }
        };

        var regular = ChatMapper.MapMessage(message, envelope: null, viewerUserId: 1, allowKick: true);
        regular.IsRemovedByModerator.Should().BeTrue();
        regular.Body.Should().BeNull();
        regular.CanKick.Should().BeTrue();
        regular.CanViewRemoved.Should().BeFalse();
        regular.CanRestore.Should().BeFalse();

        var moderator = ChatMapper.MapMessage(
            message,
            envelope: null,
            viewerUserId: 1,
            allowKick: true,
            canModerate: true);
        moderator.Body.Should().Be("hidden text");
        moderator.CanViewRemoved.Should().BeTrue();
        moderator.CanRestore.Should().BeTrue();
        moderator.CanKick.Should().BeTrue();
    }
}
