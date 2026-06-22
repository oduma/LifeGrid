// TimelineViewModel and TimelineWeekItem live in LifeGrid.Presentation (net10.0-android)
// and cannot be directly referenced from this net10.0 test project.
//
// CardBorderState ternary logic:
//   IsSelected=true  → "Selected"  (Primary border)
//   IsSelected=false, IsCurrentWeek=true  → "Current"   (Secondary border)
//   IsSelected=false, IsCurrentWeek=false → "Default"   (dim SurfaceBrush border)
//
// SelectWeekCommand transfers IsSelected from the previous item to the tapped item;
// IsCurrentWeek is never mutated by the command.
//
// Data-pipeline coverage is provided by GetTimelineQueryHandlerTests.
// Full ViewModel branch coverage is deferred to a future LifeGrid.Presentation.Tests project.
