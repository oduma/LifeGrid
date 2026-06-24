// VaultViewModel lives in LifeGrid.Presentation (net10.0-android) and cannot be
// directly referenced from this net10.0 test project.
//
// LoadAsync behaviour:
//   - No badges → IsEmptyStateVisible = true, Badges collection is empty
//   - With badges → IsEmptyStateVisible = false, Badges contains one VaultBadgeItem per dto
//   - GridSpan set to 4 when device width ≥ 400dp; 3 otherwise
//   - TierColor mapped: Gold=#FFC300, Silver=#9CA3AF, Bronze=#D47A43 (Phase 26)
//
// Data-pipeline coverage is provided by GetUserBadgesQueryTests and
// ConsistencyBadgeEvaluatorTests.
// Full ViewModel branch coverage is deferred to a future LifeGrid.Presentation.Tests project.
