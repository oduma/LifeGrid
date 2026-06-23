using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.HabitLogging;

public record LogHabitProgressCommand(
    Guid    HabitId,
    double  ActualValue,
    string  MeasurementUnit,
    string? ProofText,
    string? ProofImageUrl) : IRequest<Result>;
