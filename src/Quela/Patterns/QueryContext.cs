namespace Quela;

/// <summary>
/// Context for multi-tenant queries with automatic filtering.
/// </summary>
/// <example>
/// <code>
/// var ctx = QueryContext.ForFacility(orgId: 1, facilityId: 42, userId: "nurse1");
///
/// // Use with TypedView for automatic tenant filtering
/// var residents = new ActiveResidents();
/// residents.From(ctx)
///     .Where(residents.UnitId == 5)
///     .Select(residents.Id, residents.FullName);
/// </code>
/// </example>
public class QueryContext
{
    public int OrganizationId { get; init; }
    public int FacilityId { get; init; }
    public int? UnitId { get; init; }
    public DateTime EffectiveDate { get; init; } = DateTime.Today;
    public string UserId { get; init; } = "";

    /// <summary>
    /// Creates a context for facility-level queries.
    /// </summary>
    public static QueryContext ForFacility(int orgId, int facilityId, string userId) =>
        new()
        {
            OrganizationId = orgId,
            FacilityId = facilityId,
            UserId = userId
        };

    /// <summary>
    /// Creates a context for organization-level queries.
    /// </summary>
    public static QueryContext ForOrganization(int orgId, string userId) =>
        new()
        {
            OrganizationId = orgId,
            UserId = userId
        };

    /// <summary>
    /// Creates a point-in-time query context.
    /// </summary>
    public QueryContext AsOf(DateTime effectiveDate) =>
        new()
        {
            OrganizationId = OrganizationId,
            FacilityId = FacilityId,
            UnitId = UnitId,
            EffectiveDate = effectiveDate,
            UserId = UserId
        };

    /// <summary>
    /// Scopes the context to a specific unit.
    /// </summary>
    public QueryContext ForUnit(int unitId) =>
        new()
        {
            OrganizationId = OrganizationId,
            FacilityId = FacilityId,
            UnitId = unitId,
            EffectiveDate = EffectiveDate,
            UserId = UserId
        };
}
