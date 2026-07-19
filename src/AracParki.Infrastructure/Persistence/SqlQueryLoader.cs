using System.Collections.Concurrent;
using System.Reflection;
using AracParki.Application.Abstractions;

namespace AracParki.Infrastructure.Persistence;

public sealed class SqlQueryLoader : ISqlQueryLoader
{
    private readonly string _root = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory,
        "Persistence",
        "Sql");

    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public string Get(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return _cache.GetOrAdd(relativePath, static (path, root) =>
        {
            var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"SQL query not found: {path}", fullPath);
            }

            return File.ReadAllText(fullPath);
        }, _root);
    }
}
