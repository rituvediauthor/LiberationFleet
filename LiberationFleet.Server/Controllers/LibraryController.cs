using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Commands.CancelLibraryRequest;
using LiberationFleet.Server.Application.Features.Library.Commands.CompleteLibraryRequest;
using LiberationFleet.Server.Application.Features.Library.Commands.CreateLibraryOffering;
using LiberationFleet.Server.Application.Features.Library.Commands.CreateLibraryRequest;
using LiberationFleet.Server.Application.Features.Library.Commands.DenyLibraryRequest;
using LiberationFleet.Server.Application.Features.Library.Commands.ConfirmLibraryUnitBroken;
using LiberationFleet.Server.Application.Features.Library.Commands.RecordLibraryMaintenance;
using LiberationFleet.Server.Application.Features.Library.Commands.ReportLibraryUnitBroken;
using LiberationFleet.Server.Application.Features.Library.Commands.RecordLibraryAcquisition;
using LiberationFleet.Server.Application.Features.Library.Commands.ReportLibraryUnitFixed;
using LiberationFleet.Server.Application.Features.Library.Commands.SendLibraryRequestMessage;
using LiberationFleet.Server.Application.Features.Library.Commands.UndenyLibraryRequest;
using LiberationFleet.Server.Application.Features.Library.Commands.DeleteLibraryOffering;
using LiberationFleet.Server.Application.Features.Library.Commands.ReportLibraryUnitLost;
using LiberationFleet.Server.Application.Features.Library.Commands.UpdateLibraryOffering;
using LiberationFleet.Server.Application.Features.Library.Commands.UpdateLibraryRequest;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Library.Queries.GetDurableLibraryUnits;
using LiberationFleet.Server.Application.Features.Library.Queries.GetIncomingLibraryRequests;
using LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryCategories;
using LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryRequestDetail;
using LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryRequestMessages;
using LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryUnitDetail;
using LiberationFleet.Server.Application.Features.Library.Queries.GetMyLibraryOfferings;
using LiberationFleet.Server.Application.Features.Library.Queries.GetMyLibraryRequests;
using LiberationFleet.Server.Application.Features.Library.Queries.GetStockLibraryOfferings;
using LiberationFleet.Server.Application.Features.Library.Queries.GetUnitActiveLibraryRequests;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Filters;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/library")]
[Authorize]
[ServiceFilter(typeof(LibraryAccessFilter))]
public class LibraryController(IMediator mediator) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] bool inUseOnly = false,
        [FromQuery] string? kind = null)
    {
        var result = await mediator.Send(new GetLibraryCategoriesQuery(inUseOnly, kind));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("durable-units")]
    public async Task<IActionResult> GetDurableUnits(
        [FromQuery] string? search,
        [FromQuery] int[]? categoryIds,
        [FromQuery] int limit = 30,
        [FromQuery] int offset = 0)
    {
        var result = await mediator.Send(new GetDurableLibraryUnitsQuery(
            search,
            categoryIds ?? Array.Empty<int>(),
            limit,
            offset));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("consumable-units")]
    public async Task<IActionResult> GetConsumableUnits(
        [FromQuery] string? search,
        [FromQuery] int[]? categoryIds,
        [FromQuery] int limit = 30,
        [FromQuery] int offset = 0)
    {
        var result = await mediator.Send(new GetStockLibraryOfferingsQuery(
            LibraryOfferingKind.Consumable,
            search,
            categoryIds ?? Array.Empty<int>(),
            limit,
            offset));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("services-units")]
    public async Task<IActionResult> GetServicesUnits(
        [FromQuery] string? search,
        [FromQuery] int[]? categoryIds,
        [FromQuery] int limit = 30,
        [FromQuery] int offset = 0)
    {
        var result = await mediator.Send(new GetStockLibraryOfferingsQuery(
            LibraryOfferingKind.Service,
            search,
            categoryIds ?? Array.Empty<int>(),
            limit,
            offset));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("offerings/mine")]
    public async Task<IActionResult> GetMyOfferings(
        [FromQuery] string? search,
        [FromQuery] int[]? categoryIds,
        [FromQuery] int limit = 30,
        [FromQuery] int offset = 0)
    {
        var result = await mediator.Send(new GetMyLibraryOfferingsQuery(search, categoryIds, limit, offset));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("offerings/{id:int}")]
    public async Task<IActionResult> UpdateOffering(int id, [FromBody] UpdateLibraryOfferingRequest body)
    {
        var result = await mediator.Send(new UpdateLibraryOfferingCommand(id, body.IsOutOfStock));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("offerings/{id:int}")]
    public async Task<IActionResult> DeleteOffering(int id)
    {
        var result = await mediator.Send(new DeleteLibraryOfferingCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("units/{id:int}")]
    public async Task<IActionResult> GetUnitDetail(int id)
    {
        var result = await mediator.Send(new GetLibraryUnitDetailQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("units/{unitId:int}/requests")]
    public async Task<IActionResult> GetUnitActiveRequests(int unitId)
    {
        var result = await mediator.Send(new GetUnitActiveLibraryRequestsQuery(unitId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("units/{id:int}/requests")]
    public async Task<IActionResult> CreateRequest(int id, [FromBody] CreateLibraryRequestRequest body)
    {
        var result = await mediator.Send(new CreateLibraryRequestCommand(
            id,
            body.Quantity,
            body.PurposePreview,
            body.NeededByStart,
            body.NeededByEnd,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("units/{id:int}/acquisitions")]
    public async Task<IActionResult> RecordAcquisition(int id, [FromBody] RecordLibraryAcquisitionRequest body)
    {
        var result = await mediator.Send(new RecordLibraryAcquisitionCommand(
            id,
            body.Quantity,
            body.PurposePreview,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("units/{id:int}/report-broken")]
    public async Task<IActionResult> ReportBroken(int id, [FromBody] ReportLibraryUnitBrokenRequest body)
    {
        var result = await mediator.Send(new ReportLibraryUnitBrokenCommand(
            id,
            body.ExplanationPreview,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("units/{id:int}/confirm-broken")]
    public async Task<IActionResult> ConfirmBroken(int id)
    {
        var result = await mediator.Send(new ConfirmLibraryUnitBrokenCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("units/{id:int}/report-fixed")]
    public async Task<IActionResult> ReportFixed(int id)
    {
        var result = await mediator.Send(new ReportLibraryUnitFixedCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("units/{id:int}/report-lost")]
    public async Task<IActionResult> ReportLost(int id)
    {
        var result = await mediator.Send(new ReportLibraryUnitLostCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("units/{id:int}/maintenance")]
    public async Task<IActionResult> RecordMaintenance(int id, [FromBody] RecordLibraryMaintenanceRequest body)
    {
        var result = await mediator.Send(new RecordLibraryMaintenanceCommand(
            id,
            body.Cost,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests/incoming")]
    public async Task<IActionResult> GetIncomingRequests()
    {
        var result = await mediator.Send(new GetIncomingLibraryRequestsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests/mine")]
    public async Task<IActionResult> GetMyRequests()
    {
        var result = await mediator.Send(new GetMyLibraryRequestsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> GetRequestDetail(int id)
    {
        var result = await mediator.Send(new GetLibraryRequestDetailQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests/{id:int}/messages")]
    public async Task<IActionResult> GetRequestMessages(
        int id,
        [FromQuery] int limit = 50,
        [FromQuery] int? beforeMessageId = null)
    {
        var result = await mediator.Send(new GetLibraryRequestMessagesQuery(id, limit, beforeMessageId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("requests/{id:int}/messages")]
    public async Task<IActionResult> SendRequestMessage(int id, [FromBody] SendLibraryRequestMessageRequest body)
    {
        var result = await mediator.Send(new SendLibraryRequestMessageCommand(
            id,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.MentionedUserIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("requests/{id:int}")]
    public async Task<IActionResult> UpdateRequest(int id, [FromBody] UpdateLibraryRequestRequest body)
    {
        var result = await mediator.Send(new UpdateLibraryRequestCommand(
            id,
            body.PurposePreview,
            body.NeededByStart,
            body.NeededByEnd,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("requests/{id:int}/cancel")]
    public async Task<IActionResult> CancelRequest(int id)
    {
        var result = await mediator.Send(new CancelLibraryRequestCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("requests/{id:int}/complete")]
    public async Task<IActionResult> CompleteRequest(int id)
    {
        var result = await mediator.Send(new CompleteLibraryRequestCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("requests/{id:int}/deny")]
    public async Task<IActionResult> DenyRequest(int id)
    {
        var result = await mediator.Send(new DenyLibraryRequestCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("requests/{id:int}/undeny")]
    public async Task<IActionResult> UndenyRequest(int id)
    {
        var result = await mediator.Send(new UndenyLibraryRequestCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("offerings")]
    public async Task<IActionResult> CreateOffering([FromBody] CreateLibraryOfferingRequest body)
    {
        if (!LibraryEnumParser.TryParseOfferingKind(body.Kind, out var kind))
        {
            return BadRequest(new LibraryOfferingOperationResponse
            {
                Success = false,
                Message = "Invalid offering kind."
            });
        }

        if (!LibraryEnumParser.TryParseFulfillmentMode(body.FulfillmentMode, out var fulfillmentMode))
        {
            return BadRequest(new LibraryOfferingOperationResponse
            {
                Success = false,
                Message = "Invalid fulfillment mode."
            });
        }

        var result = await mediator.Send(new CreateLibraryOfferingCommand(
            body.Title,
            body.DescriptionPreview,
            body.CategoryIds,
            body.ValuePerUnit,
            body.UnitLabel,
            body.Quantity,
            body.QuantityNotApplicable,
            body.ThumbnailResourceId,
            kind,
            fulfillmentMode,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
