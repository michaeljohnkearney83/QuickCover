using Mediator;
using Microsoft.AspNetCore.Mvc;
using QuickCover.Application.Policies;
using QuickCover.Application.Quotes;
using QuickCover.Domain.Quotes;

namespace QuickCover.QuoteApi;

// API contracts are separate from domain types, so the domain can change without breaking clients.
public sealed record CreateQuoteRequest(
    int DriverAge,
    int YearsLicensed,
    int ClaimsLast5Years,
    decimal VehicleValue,
    VehicleUse Use,
    string PostcodeArea);

public sealed record PurchaseQuoteRequest(DateOnly StartDate);

public static class QuoteEndpoints
{
    // Step 1 identifies the customer with a header. Step 5 replaces this with an Entra ID token
    // and reads the customer id from its claims.
    private const string CustomerHeader = "X-Customer-Id";

    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/quotes").WithTags("Quotes");

        api.MapPost("/", async (
                CreateQuoteRequest body,
                [FromHeader(Name = CustomerHeader)] string customerId,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var request = new QuoteRequest(
                    body.DriverAge, body.YearsLicensed, body.ClaimsLast5Years,
                    body.VehicleValue, body.Use, body.PostcodeArea);

                var quote = await mediator.Send(new CreateQuoteCommand(customerId, request), ct);
                return Results.Created($"/api/quotes/{quote.QuoteId}", quote);
            })
            .WithName("CreateQuote");

        api.MapGet("/{quoteId:guid}", async (
                Guid quoteId,
                [FromHeader(Name = CustomerHeader)] string customerId,
                IMediator mediator,
                CancellationToken ct) =>
                Results.Ok(await mediator.Send(new GetQuoteQuery(quoteId, customerId), ct)))
            .WithName("GetQuote");

        api.MapPost("/{quoteId:guid}/purchase", async (
                Guid quoteId,
                PurchaseQuoteRequest body,
                [FromHeader(Name = CustomerHeader)] string customerId,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator.Send(new PurchaseQuoteCommand(quoteId, customerId, body.StartDate), ct);
                // 202: payment is taken, the policy is issued asynchronously. Poll the Location URL.
                return Results.Accepted($"/api/quotes/{quoteId}/policy", result);
            })
            .WithName("PurchaseQuote");

        api.MapGet("/{quoteId:guid}/policy", async (
                Guid quoteId,
                [FromHeader(Name = CustomerHeader)] string customerId,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var policy = await mediator.Send(new GetPolicyForQuoteQuery(quoteId, customerId), ct);
                return policy is null
                    ? Results.NotFound(new { message = "Policy not issued yet. Try again shortly." })
                    : Results.Ok(policy);
            })
            .WithName("GetPolicyForQuote");

        app.MapGet("/api/policies", async (
                [FromHeader(Name = CustomerHeader)] string customerId,
                IMediator mediator,
                CancellationToken ct) =>
                Results.Ok(await mediator.Send(new ListPoliciesQuery(customerId), ct)))
            .WithTags("Policies")
            .WithName("ListPolicies");

        return app;
    }
}
