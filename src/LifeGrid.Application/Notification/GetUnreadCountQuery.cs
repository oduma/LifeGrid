using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Notification;

public record GetUnreadCountQuery : IRequest<Result<int>>;

public sealed class GetUnreadCountQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetUnreadCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(
        GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var count = await repository.GetUnreadCountAsync(cancellationToken);
        return Result<int>.Success(count);
    }
}
