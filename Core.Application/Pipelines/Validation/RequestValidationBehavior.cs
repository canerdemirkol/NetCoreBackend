using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using ValidationException = NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types.ValidationException;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Validation;

public class RequestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public RequestValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        // Each validator gets its own ValidationContext: FluentValidation's context carries
        // mutable per-run state (PropertyChain, RuleSet, RootContextData) that would corrupt
        // if shared across concurrent ValidateAsync calls. Async execution still pays off
        // because individual validators can run I/O-bound rules without blocking a thread.
        ValidationResult[] results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken))
        );

        IEnumerable<ValidationExceptionModel> errors = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure != null)
            .GroupBy(
                keySelector: p => p.PropertyName,
                resultSelector: (propertyName, errors) =>
                    new ValidationExceptionModel { Property = propertyName, Errors = errors.Select(e => e.ErrorMessage) }
            )
            .ToList();

        if (errors.Any())
            throw new ValidationException(errors);
        TResponse response = await next(cancellationToken);
        return response;
    }
}
