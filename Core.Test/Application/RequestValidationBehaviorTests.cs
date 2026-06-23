using FluentValidation;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Validation;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using ValidationException = NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types.ValidationException;

namespace NetCoreBackend.NArchitecture.Core.Test.Application;

// Regression coverage for the R2 fix that gave each validator its own ValidationContext.
//
// FluentValidation's ValidationContext carries mutable per-run state (PropertyChain, RuleSet,
// RootContextData). The R1 fix originally shared one context across concurrent ValidateAsync
// calls — under load this manifested as missing rules, wrong rule sets, and intermittent
// NullRef. The test below tightens the loop with several validators that each pause briefly
// inside the async path, which surfaced the corruption reliably before the R2 fix.
public sealed class RequestValidationBehaviorTests
{
    public sealed record ProbeRequest(string Value) : IRequest<string>;

    private sealed class SlowValidator : AbstractValidator<ProbeRequest>
    {
        private readonly string _failToken;

        public SlowValidator(string failToken)
        {
            _failToken = failToken;
            RuleFor(x => x.Value).CustomAsync(async (value, ctx, ct) =>
            {
                // Yield + small delay forces the WhenAll continuations to interleave on the
                // thread pool, which is exactly the scenario that exposed the shared-context
                // bug. The work itself is otherwise a no-op.
                await Task.Yield();
                await Task.Delay(5, ct);

                if (value?.Contains(_failToken, StringComparison.Ordinal) == true)
                    ctx.AddFailure(nameof(ProbeRequest.Value), $"value contains '{_failToken}'");
            });
        }
    }

    private static Task<string> Next(CancellationToken ct) => Task.FromResult("handled");

    [Fact]
    public async Task ConcurrentValidators_DoNotCorruptContext()
    {
        IValidator<ProbeRequest>[] validators =
        [
            new SlowValidator("a"),
            new SlowValidator("b"),
            new SlowValidator("c"),
            new SlowValidator("d"),
        ];

        RequestValidationBehavior<ProbeRequest, string> behavior = new(validators);

        // Drive 20 parallel handles to stress the shared-state hazard. With per-validator
        // contexts each one should observe its own input cleanly and report only its own
        // failure (none for "z..." values).
        Task<string>[] runs = Enumerable.Range(0, 20)
            .Select(i => behavior.Handle(new ProbeRequest($"z-{i}"), Next, CancellationToken.None))
            .ToArray();

        string[] results = await Task.WhenAll(runs);

        Assert.All(results, r => Assert.Equal("handled", r));
    }

    [Fact]
    public async Task FailureFromOneValidator_DoesNotMaskOthers()
    {
        IValidator<ProbeRequest>[] validators = [new SlowValidator("a"), new SlowValidator("b")];
        RequestValidationBehavior<ProbeRequest, string> behavior = new(validators);

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new ProbeRequest("contains-a-and-b"), Next, CancellationToken.None));

        // Both tokens appear in the value → both validators should have contributed errors,
        // grouped under the single failing property "Value".
        var groups = ex.Errors.ToList();
        ValidationExceptionModel valueGroup = Assert.Single(groups);
        Assert.NotNull(valueGroup.Errors);
        Assert.Equal(2, valueGroup.Errors!.Count());
    }
}
