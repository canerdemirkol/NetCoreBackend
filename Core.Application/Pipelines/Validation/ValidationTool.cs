using FluentValidation;
using FluentValidation.Results;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using ValidationException = NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types.ValidationException;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Validation;

public static class ValidationTool
{
    public static void Validate(IValidator validator, object entity)
    {
        ValidationContext<object> context = new(entity);
        ValidationResult result = validator.Validate(context);
        if (result.IsValid)
            return;

        // Convert FluentValidation failures into the framework's ValidationExceptionModel so that
        // HttpExceptionHandler.HandleException(ValidationException) catches it and emits a 400 with
        // ValidationProblemDetails — instead of FluentValidation's ValidationException falling
        // through to the generic 500 handler.
        IEnumerable<ValidationExceptionModel> errors = result.Errors
            .GroupBy(f => f.PropertyName)
            .Select(g => new ValidationExceptionModel
            {
                Property = g.Key,
                Errors = g.Select(f => f.ErrorMessage)
            });

        throw new ValidationException(errors);
    }
}
