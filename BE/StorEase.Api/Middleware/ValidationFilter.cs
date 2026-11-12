using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using StorEase.Application.Common;

namespace StorEase.Api.Middleware;

/// <summary>
/// Runs the FluentValidation validator for each action argument that has one, and answers 400 in the same
/// ValidationProblemDetails shape as ASP.NET Core's own model validation.
/// </summary>
public class ValidationFilter(IOptions<ApiBehaviorOptions> apiBehavior) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, ErrorText.Localize(error.ErrorMessage));
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = apiBehavior.Value.InvalidModelStateResponseFactory(context);
            return;
        }

        await next();
    }
}
