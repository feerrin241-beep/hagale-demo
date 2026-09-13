using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

internal static class ControllerValidationExtensions
{
    public static ActionResult BusinessRuleViolation(this ControllerBase controller, string key, string error)
    {
        controller.ModelState.AddModelError(key, error);
        return controller.ValidationProblem(controller.ModelState);
    }
}
