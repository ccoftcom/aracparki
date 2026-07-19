using System.Reflection;
using AracParki.Application.Abstractions;

namespace AracParki.Infrastructure.Persistence;

public sealed class SqlQueryLoader : ISqlQueryLoader
{
    private readonly string _root = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory,
        "Persistence",
        "Sql");

    public string Get(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"SQL query not found: {relativePath}", fullPath);
        }

        return File.ReadAllText(fullPath);
    }
}
