namespace LiberationFleet.Server.Application.Features.Engagement.Contracts;

public class ContentLikerDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarResourceId { get; set; }
}

public class ContentLikersResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ContentLikerDto> Items { get; set; } = Array.Empty<ContentLikerDto>();
}
