using LifeGrid.Application.Common;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.UserSetup.Commands;

public record FactoryResetCommand : IRequest<Result>;

public sealed class FactoryResetCommandHandler(IFactoryResetService factoryResetService)
    : IRequestHandler<FactoryResetCommand, Result>
{
    public async Task<Result> Handle(FactoryResetCommand request, CancellationToken cancellationToken)
    {
        await factoryResetService.ResetAsync(cancellationToken);
        return Result.Success();
    }
}
