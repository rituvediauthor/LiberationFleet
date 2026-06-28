using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Projects.Commands.CreateProjectPost;

public record CreateProjectPostCommand(string Nonce, string Ciphertext, int KeyVersion) : IRequest<ProjectOperationResponse>;

public class CreateProjectPostCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProjectRepository projectRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProjectPostCommand, ProjectOperationResponse>
{
    public async Task<ProjectOperationResponse> Handle(CreateProjectPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProjectOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProjectOperationResponse { Success = false, Message = "Encrypted post content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProjectOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var post = new ProjectPost
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        await projectRepository.AddPostAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ProjectForumPost,
            ResourceId = post.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewIfNotMutedAsync(
            membership.CrewId,
            NotificationKind.NewProjectPost,
            MutedContentType.Project,
            post.Id,
            "New project post",
            "A new project post was published.",
            $"/app/crew/projects/{post.Id}",
            relatedEntityId: post.Id,
            excludeUserId: userId,
            cancellationToken: cancellationToken);

        return new ProjectOperationResponse
        {
            Success = true,
            Message = "Project post created.",
            PostId = post.Id
        };
    }
}
