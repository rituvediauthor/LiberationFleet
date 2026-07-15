using LiberationFleet.Server.Application.Features.Crypto.Commands.DeleteCrewAttachment;
using LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertCrewKeyDistribution;
using LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertFleetKeyDistribution;
using LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertEncryptedContent;
using LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertPrivateKeyBackup;
using LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertPublicKey;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetCrewKeyState;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetCrewPublicKeys;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetEncryptedContents;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetFleetKeyState;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetFleetPublicKeys;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetMyPrivateKeyBackup;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetPublicKey;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/crypto")]
[Authorize]
public class CryptoController : ControllerBase
{
    private readonly IMediator _mediator;

    public CryptoController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPut("keys/public")]
    public async Task<IActionResult> UpsertPublicKey([FromBody] UpsertPublicKeyRequest body)
    {
        var result = await _mediator.Send(new UpsertPublicKeyCommand(body.IdentityPublicKey, body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("keys/public/{userId:int}")]
    public async Task<IActionResult> GetPublicKey(int userId)
    {
        var result = await _mediator.Send(new GetPublicKeyQuery(userId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("keys/public/crew/{crewId:int}")]
    public async Task<IActionResult> GetCrewPublicKeys(int crewId)
    {
        var result = await _mediator.Send(new GetCrewPublicKeysQuery(crewId));
        return Ok(result);
    }

    [HttpPut("keys/backup")]
    public async Task<IActionResult> UpsertPrivateKeyBackup([FromBody] UpsertPrivateKeyBackupRequest body)
    {
        var result = await _mediator.Send(new UpsertPrivateKeyBackupCommand(
            body.Salt,
            body.Iv,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("keys/backup")]
    public async Task<IActionResult> GetMyPrivateKeyBackup()
    {
        var result = await _mediator.Send(new GetMyPrivateKeyBackupQuery());
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("crew-keys/{crewId:int}")]
    public async Task<IActionResult> UpsertCrewKeyDistribution(int crewId, [FromBody] UpsertCrewKeyDistributionRequest body)
    {
        var result = await _mediator.Send(new UpsertCrewKeyDistributionCommand(
            crewId,
            body.UserId,
            body.KeyVersion,
            body.WrappedCrewKey,
            body.WrapNonce));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("crew-keys/{crewId:int}")]
    public async Task<IActionResult> GetCrewKeyState(int crewId)
    {
        var result = await _mediator.Send(new GetCrewKeyStateQuery(crewId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("fleet-keys/{fleetId:int}")]
    public async Task<IActionResult> UpsertFleetKeyDistribution(int fleetId, [FromBody] UpsertFleetKeyDistributionRequest body)
    {
        var result = await _mediator.Send(new UpsertFleetKeyDistributionCommand(
            fleetId,
            body.UserId,
            body.KeyVersion,
            body.WrappedFleetKey,
            body.WrapNonce));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("fleet-keys/{fleetId:int}")]
    public async Task<IActionResult> GetFleetKeyState(int fleetId)
    {
        var result = await _mediator.Send(new GetFleetKeyStateQuery(fleetId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("keys/public/fleet/{fleetId:int}")]
    public async Task<IActionResult> GetFleetPublicKeys(int fleetId)
    {
        var result = await _mediator.Send(new GetFleetPublicKeysQuery(fleetId));
        return Ok(result);
    }

    [HttpPut("content")]
    public async Task<IActionResult> UpsertEncryptedContent([FromBody] UpsertEncryptedContentRequest body)
    {
        var result = await _mediator.Send(new UpsertEncryptedContentCommand(
            body.ContentType,
            body.ResourceId,
            body.CrewId,
            body.FleetId,
            body.KeyVersion,
            body.Nonce,
            body.Ciphertext));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetEncryptedContents(
        [FromQuery] EncryptedContentTypeDto contentType,
        [FromQuery] string resourceIds,
        [FromQuery] int? crewId = null,
        [FromQuery] int? fleetId = null)
    {
        var ids = resourceIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var result = await _mediator.Send(new GetEncryptedContentsQuery(contentType, ids, crewId, fleetId));
        return Ok(result);
    }

    [HttpDelete("content")]
    public async Task<IActionResult> DeleteAttachment(
        [FromQuery] EncryptedContentTypeDto contentType,
        [FromQuery] string resourceId,
        [FromQuery] int crewId)
    {
        var result = await _mediator.Send(new DeleteCrewAttachmentCommand(contentType, resourceId, crewId));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
