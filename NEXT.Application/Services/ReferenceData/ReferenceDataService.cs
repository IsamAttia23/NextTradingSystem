using NEXT.Application.DTOs.Common;
using System.Linq.Expressions;

using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;

namespace NEXT.Application.Services.ReferenceData;

public sealed class ReferenceDataService : IReferenceDataService
{
    private readonly Dictionary<string, IReferenceSource> _sources = new(StringComparer.Ordinal);
    public ReferenceDataService(IEnumerable<IReferenceSource> sources)
    {
        foreach (var source in sources)
            if (!_sources.TryAdd(source.Key, source))
                throw new InvalidOperationException($"Reference source '{source.Key}' is registered more than once.");
    }
    public Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(string key, CancellationToken ct = default) =>
        _sources.TryGetValue(key, out var source) ? source.GetAsync(ct)
            : throw new InvalidOperationException($"Reference source '{key}' is not registered.");
}
