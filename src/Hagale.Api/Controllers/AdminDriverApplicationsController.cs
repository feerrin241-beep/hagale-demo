using System.ComponentModel.DataAnnotations;
using Hagale.Application.Contracts;
using Hagale.Application.Authentication;
using Hagale.Application.Drivers;
using Hagale.Domain.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Administrator)]
[Route("api/v1/admin/driver-applications")]
public sealed class AdminDriverApplicationsController(
    IDriverApplicationService driverApplicationService,
    IPrivateDocumentStorage privateDocumentStorage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DriverApplicationPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DriverApplicationPageDto>> List(
        [FromQuery] DriverStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await driverApplicationService.ListForReviewAsync(status, page, pageSize, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("pagination", result.Error!);
    }

    [HttpPut("{driverProfileId:guid}/start-review")]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> StartReview(Guid driverProfileId, CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.StartReviewAsync(
            User.GetRequiredUserId(),
            driverProfileId,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("review", result.Error!);
    }

    [HttpGet("{driverProfileId:guid}/documents/{documentId:guid}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OpenDocument(
        Guid driverProfileId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.GetDocumentForReviewAsync(
            User.GetRequiredUserId(),
            driverProfileId,
            documentId,
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return this.BusinessRuleViolation("document", result.Error!);
        }

        var document = result.Value;
        var storedDocument = await privateDocumentStorage.OpenReadAsync(document.StorageObjectKey, cancellationToken);
        return storedDocument is null
            ? NotFound(new ProblemDetails { Title = "Documento no encontrado.", Detail = "El archivo privado no existe en el almacenamiento local." })
            : File(storedDocument.Content, storedDocument.ContentType, document.DownloadFileName, enableRangeProcessing: true);
    }

    [HttpPut("{driverProfileId:guid}/documents/{documentId:guid}/review")]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> ReviewDocument(
        Guid driverProfileId,
        Guid documentId,
        ReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.ReviewDocumentAsync(
            User.GetRequiredUserId(),
            driverProfileId,
            documentId,
            new ReviewDecision(request.Approve, request.Notes),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("review", result.Error!);
    }

    [HttpPut("{driverProfileId:guid}/review")]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> ReviewApplication(
        Guid driverProfileId,
        ReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.ReviewApplicationAsync(
            User.GetRequiredUserId(),
            driverProfileId,
            new ReviewDecision(request.Approve, request.Notes),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("review", result.Error!);
    }
}

public sealed record ReviewDecisionRequest(bool Approve, [StringLength(1_000)] string? Notes);
