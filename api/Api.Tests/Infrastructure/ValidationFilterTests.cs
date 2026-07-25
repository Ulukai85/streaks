using Api.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

public class ValidationFilterTests
{
    private sealed record FakeRequest(string Name);

    private sealed class FakeRequestValidator : AbstractValidator<FakeRequest>
    {
        public FakeRequestValidator()
        {
            RuleFor(r => r.Name).NotEmpty();
        }
    }

    [Fact]
    public async Task InvokeAsync_Returns_ValidationProblem_When_Request_Invalid()
    {
        var filter = new ValidationFilter<FakeRequest>();
        var context = CreateContext(new FakeRequest(""));
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        nextCalled.Should().BeFalse();
        var problem = result.Should().BeOfType<ValidationProblem>().Which;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_Calls_Next_When_Request_Valid()
    {
        var filter = new ValidationFilter<FakeRequest>();
        var context = CreateContext(new FakeRequest("valid"));

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok()));

        result.Should().BeOfType<Ok>();
    }

    private static EndpointFilterInvocationContext CreateContext(FakeRequest request)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FakeRequest>, FakeRequestValidator>();

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        return EndpointFilterInvocationContext.Create(httpContext, request);
    }
}
