using System.Transactions;
using MediatR;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Transaction;

public class TransactionScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ITransactionalRequest
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        // The `using` declaration handles rollback automatically: if Complete() is not reached
        // (because next() or Complete() itself throws), Dispose rolls back. The previous explicit
        // Dispose() in a catch block duplicated the rollback and obscured the intent.
        using TransactionScope transactionScope = new(TransactionScopeAsyncFlowOption.Enabled);
        TResponse response = await next(cancellationToken);
        transactionScope.Complete();
        return response;
    }
}
