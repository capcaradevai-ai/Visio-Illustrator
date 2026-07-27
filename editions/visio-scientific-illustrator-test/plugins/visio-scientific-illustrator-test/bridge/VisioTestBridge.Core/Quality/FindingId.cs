namespace VisioTestBridge.Core.Quality;

public static class FindingId
{
    public static string Create(
        string category,
        string regionId,
        IEnumerable<string> objectIds)
    {
        var normalizedCategory = NormalizePart(category, nameof(category));
        var normalizedRegion = NormalizePart(regionId, nameof(regionId));
        ArgumentNullException.ThrowIfNull(objectIds);

        var normalizedObjectIds = objectIds
            .Select(objectId => NormalizePart(objectId, nameof(objectIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (normalizedObjectIds.Length == 0)
            throw new ArgumentException("finding 必须至少关联一个对象。", nameof(objectIds));

        return $"{normalizedCategory}:{normalizedRegion}:{string.Join(",", normalizedObjectIds)}";
    }

    private static string NormalizePart(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("finding 标识字段不能为空。", parameterName);

        return value.Trim().ToLowerInvariant();
    }
}
