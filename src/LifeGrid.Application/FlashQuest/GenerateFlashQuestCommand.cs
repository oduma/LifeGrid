using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.FlashQuest;

public record GenerateFlashQuestCommand(Guid WeekId) : IRequest<Result<GenerateFlashQuestResult>>;

public record GenerateFlashQuestResult(int QuestsInjected);
