using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Planara.Files.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class UseFluentValidationAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var parameter in context.ActionDescriptor.Parameters.OfType<ControllerParameterDescriptor>())
        {
            var parameterName = parameter.Name;
            var parameterType = parameter.ParameterInfo.ParameterType;

            var validatorType = typeof(IValidator<>).MakeGenericType(parameterType);
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator is null)
                continue;

            context.ActionArguments.TryGetValue(parameterName, out var value);

            if (value is null)
            {
                errors[parameterName] = ["Параметр является обязательным."];
                continue;
            }

            var result = await ValidateAsync(
                validator,
                parameterType,
                value,
                context.HttpContext.RequestAborted);

            if (result.IsValid)
                continue;

            foreach (var group in result.Errors.GroupBy(x => GetErrorKey(parameterName, x)))
            {
                errors[group.Key] = group
                    .Select(x => x.ErrorMessage)
                    .ToArray();
            }
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            });

            return;
        }

        await next();
    }

    private static async Task<ValidationResult> ValidateAsync(
        IValidator validator,
        Type parameterType,
        object value,
        CancellationToken cancellationToken)
    {
        var contextType = typeof(ValidationContext<>).MakeGenericType(parameterType);

        var validationContext = (IValidationContext)Activator.CreateInstance(
            contextType,
            value)!;

        return await validator.ValidateAsync(validationContext, cancellationToken);
    }

    private static string GetErrorKey(string parameterName, ValidationFailure failure)
    {
        if (string.IsNullOrWhiteSpace(failure.PropertyName))
            return parameterName;

        return $"{parameterName}.{failure.PropertyName}";
    }
}