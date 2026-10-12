using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Gifts.Commands.SaveReceptionOrderEditor;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using Moq;
using Xunit;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands;

public class SaveReceptionOrderEditorCommandTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsFailure()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns((int?)null);
        var mutualAid = new Mock<IMutualAidService>(MockBehavior.Strict);
        var handler = new SaveReceptionOrderEditorCommandHandler(currentUser.Object, mutualAid.Object);

        var result = await handler.Handle(
            new SaveReceptionOrderEditorCommand(new SaveReceptionOrderEditorRequest()),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Unauthorized.", result.Message);
    }

    [Fact]
    public async Task Handle_WhenAuthorized_DelegatesToMutualAidService()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(7);
        var mutualAid = new Mock<IMutualAidService>();
        var body = new SaveReceptionOrderEditorRequest();
        mutualAid
            .Setup(m => m.SaveReceptionOrderEditorAsync(7, body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveReceptionOrderEditorResponse
            {
                Success = true,
                Message = "Reception order saved."
            });

        var handler = new SaveReceptionOrderEditorCommandHandler(currentUser.Object, mutualAid.Object);
        var result = await handler.Handle(new SaveReceptionOrderEditorCommand(body), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Reception order saved.", result.Message);
        mutualAid.Verify(
            m => m.SaveReceptionOrderEditorAsync(7, body, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
