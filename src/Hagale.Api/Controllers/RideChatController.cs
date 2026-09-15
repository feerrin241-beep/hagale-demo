using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Rides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ride-requests/{rideRequestId:guid}/messages")]
public sealed class RideChatController(IRideChatService rideChatService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<RideChatMessageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RideChatMessageDto>>> List(
        Guid rideRequestId,
        CancellationToken cancellationToken)
    {
        var result = await rideChatService.ListAsync(
            User.GetRequiredUserId(),
            rideRequestId,
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("rideChat", result.Error!);
    }

    [HttpPost]
    [ProducesResponseType<RideChatMessageDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RideChatMessageDto>> Send(
        Guid rideRequestId,
        SendRideChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rideChatService.SendAsync(
            User.GetRequiredUserId(),
            rideRequestId,
            new SendRideChatMessageCommand(request.Message),
            cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("rideChat", result.Error!);
    }
}

public sealed record SendRideChatMessageRequest(
    [Required, StringLength(500, MinimumLength = 1)] string Message);
