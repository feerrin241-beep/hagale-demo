using System.ComponentModel.DataAnnotations;
using Hagale.Application.Contracts;
using Hagale.Application.Authentication;
using Hagale.Application.Drivers;
using Hagale.Domain.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Route("api/v1/driver-application")]
public sealed class DriverApplicationController(
    IDriverApplicationService driverApplicationService,
    IPrivateDocumentStorage privateDocumentStorage) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DriverProfileDto>> Apply(CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.ApplyAsync(User.GetRequiredUserId(), new CreateDriverApplicationCommand(), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("driverApplication", result.Error!);
    }

    [HttpGet("me")]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverProfileDto>> GetMine(CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.GetByUserAsync(User.GetRequiredUserId(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ProblemDetails { Title = "Solicitud no encontrada.", Detail = result.Error });
    }

    [HttpPost("vehicles")]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> RegisterVehicle(RegisterVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.RegisterVehicleAsync(
            User.GetRequiredUserId(),
            new RegisterVehicleCommand(request.Brand, request.Model, request.Year, request.Color, request.Plate, request.Type, request.OperatingCityCode),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("vehicle", result.Error!);
    }

    [HttpPut("vehicles/{vehicleId:guid}")]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> UpdateVehicle(Guid vehicleId, RegisterVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.UpdateVehicleAsync(
            User.GetRequiredUserId(),
            vehicleId,
            new UpdateVehicleCommand(request.Brand, request.Model, request.Year, request.Color, request.Plate, request.Type, request.OperatingCityCode),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("vehicle", result.Error!);
    }

    [HttpPatch("availability")]
    [Authorize(Roles = HagaleRoles.Driver)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> ChangeAvailability(ChangeAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.ChangeAvailabilityAsync(
            User.GetRequiredUserId(),
            new ChangeAvailabilityCommand(request.AvailabilityStatus),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("availability", result.Error!);
    }

    [HttpPatch("location")]
    [Authorize(Roles = HagaleRoles.Driver)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> UpdateLocation(UpdateDriverLocationRequest request, CancellationToken cancellationToken)
    {
        var result = await driverApplicationService.UpdateLocationAsync(
            User.GetRequiredUserId(),
            new UpdateDriverLocationCommand(request.Latitude, request.Longitude),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("dispatchLocation", result.Error!);
    }

    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_500_000)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverProfileDto>> RegisterDocument([FromForm] RegisterDriverDocumentRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            ModelState.AddModelError(nameof(request.File), "Selecciona un documento.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var userId = User.GetRequiredUserId();
            await using var stream = request.File.OpenReadStream();
            var storedDocument = await privateDocumentStorage.StoreAsync(
                userId,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                stream,
                cancellationToken);

            var result = await driverApplicationService.RegisterDocumentAsync(
                userId,
                new RegisterDriverDocumentCommand(request.Type, storedDocument.StorageObjectKey, request.ExpiresOn),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            await privateDocumentStorage.DeleteAsync(storedDocument.StorageObjectKey, cancellationToken);
            return this.BusinessRuleViolation("document", result.Error!);
        }
        catch (InvalidOperationException exception)
        {
            return this.BusinessRuleViolation("document", exception.Message);
        }
    }
}

public sealed record RegisterVehicleRequest(
    [Required, StringLength(100)] string Brand,
    [Required, StringLength(100)] string Model,
    [Range(1900, 2100)] int Year,
    [Required, StringLength(50)] string Color,
    [Required, StringLength(10)] string Plate,
    [EnumDataType(typeof(VehicleType))] VehicleType Type,
    [Required, StringLength(20)] string OperatingCityCode);

public sealed record RegisterDriverDocumentRequest(
    [EnumDataType(typeof(DriverDocumentType))] DriverDocumentType Type,
    DateOnly? ExpiresOn,
    IFormFile? File);

public sealed record ChangeAvailabilityRequest([EnumDataType(typeof(DriverAvailabilityStatus))] DriverAvailabilityStatus AvailabilityStatus);

public sealed record UpdateDriverLocationRequest(
    [Range(typeof(decimal), "-90", "90")] decimal Latitude,
    [Range(typeof(decimal), "-180", "180")] decimal Longitude);
