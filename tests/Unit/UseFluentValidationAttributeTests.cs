using System.Reflection;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Planara.Files.Filters;
using Planara.Files.Validators;

namespace Planara.Files.Tests.Unit;

public class UseFluentValidationAttributeTests
{
    [Fact]
    public async Task OnActionExecutionAsync_InvalidFile_ReturnsBadRequest()
    {
        var serviceProvider = CreateServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        var file = CreateFile("file.exe", "application/octet-stream", "content");
        var executingContext = CreateExecutingContext(httpContext, file);

        var executed = false;

        var filter = new UseFluentValidationAttribute();

        await filter.OnActionExecutionAsync(
            executingContext,
            () =>
            {
                executed = true;

                return Task.FromResult(new ActionExecutedContext(
                    executingContext,
                    filters: new List<IFilterMetadata>(),
                    controller: new object()));
            });

        executed.Should().BeFalse();
        executingContext.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ValidFile_CallsNext()
    {
        var serviceProvider = CreateServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        var file = CreateFile("image.png", "image/png", "content");
        var executingContext = CreateExecutingContext(httpContext, file);

        var executed = false;

        var filter = new UseFluentValidationAttribute();

        await filter.OnActionExecutionAsync(
            executingContext,
            () =>
            {
                executed = true;

                return Task.FromResult(new ActionExecutedContext(
                    executingContext,
                    filters: new List<IFilterMetadata>(),
                    controller: new object()));
            });

        executed.Should().BeTrue();
        executingContext.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutValidator_CallsNext()
    {
        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        var file = CreateFile("file.exe", "application/octet-stream", "content");
        var executingContext = CreateExecutingContext(httpContext, file);

        var executed = false;

        var filter = new UseFluentValidationAttribute();

        await filter.OnActionExecutionAsync(
            executingContext,
            () =>
            {
                executed = true;

                return Task.FromResult(new ActionExecutedContext(
                    executingContext,
                    filters: new List<IFilterMetadata>(),
                    controller: new object()));
            });

        executed.Should().BeTrue();
        executingContext.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_NullArgument_ReturnsBadRequest()
    {
        var serviceProvider = CreateServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        var executingContext = CreateExecutingContext(httpContext, argument: null);

        var executed = false;

        var filter = new UseFluentValidationAttribute();

        await filter.OnActionExecutionAsync(
            executingContext,
            () =>
            {
                executed = true;

                return Task.FromResult(new ActionExecutedContext(
                    executingContext,
                    filters: new List<IFilterMetadata>(),
                    controller: new object()));
            });

        executed.Should().BeFalse();
        executingContext.Result.Should().BeOfType<BadRequestObjectResult>();

        var result = (BadRequestObjectResult)executingContext.Result!;
        result.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped<IValidator<IFormFile>, FormFileValidator>();

        return services.BuildServiceProvider();
    }

    private static void FakeAction(IFormFile file)
    {
    }

    private static IFormFile CreateFile(
        string fileName,
        string contentType,
        string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static ActionExecutingContext CreateExecutingContext(
        HttpContext httpContext,
        object? argument,
        string parameterName = "file")
    {
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            CreateActionDescriptor(nameof(FakeAction), parameterName),
            new ModelStateDictionary());

        return new ActionExecutingContext(
            actionContext,
            filters: new List<IFilterMetadata>(),
            actionArguments: new Dictionary<string, object?>
            {
                [parameterName] = argument
            },
            controller: new object());
    }

    private static ControllerActionDescriptor CreateActionDescriptor(
        string methodName,
        params string[] parameterNames)
    {
        return new ControllerActionDescriptor
        {
            Parameters = parameterNames
                .Select(parameterName => CreateParameterDescriptor(methodName, parameterName))
                .Cast<ParameterDescriptor>()
                .ToList()
        };
    }

    private static ControllerParameterDescriptor CreateParameterDescriptor(
        string methodName,
        string parameterName)
    {
        var method = typeof(UseFluentValidationAttributeTests)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;

        var parameter = method
            .GetParameters()
            .Single(x => x.Name == parameterName);

        return new ControllerParameterDescriptor
        {
            Name = parameterName,
            ParameterInfo = parameter
        };
    }
}