namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public sealed class TestHistoryService : ITestHistoryService
{
    private readonly ILogger<TestHistoryService> _logger;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public TestHistoryService(ILogger<TestHistoryService> logger)
    {
        _logger = logger;
        _filePath = FilePaths.TestHistoryPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureDirectoryExists();
    }

    public async Task<IReadOnlyList<TestHistoryEntry>> GetHistoryAsync(int maxEntries = 50)
    {
        if (!File.Exists(_filePath))
            return Array.Empty<TestHistoryEntry>();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            var entries = JsonSerializer.Deserialize<List<TestHistoryEntry>>(json, _jsonOptions);
            return entries?.OrderByDescending(e => e.Timestamp).Take(maxEntries).ToList() ?? new List<TestHistoryEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load test history");
            return new List<TestHistoryEntry>();
        }
    }

    public async Task SaveTestAsync(TestHistoryEntry entry)
    {
        var entries = (await GetHistoryAsync(1000)).ToList();
        entries.Insert(0, entry);

        if (entries.Count > 1000)
            entries.RemoveRange(1000, entries.Count - 1000);

        var json = JsonSerializer.Serialize(entries, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);

        _logger.LogInformation("Saved test history entry {Id}", entry.Id);
    }

    public async Task ClearHistoryAsync()
    {
        if (File.Exists(_filePath))
        {
            await File.WriteAllTextAsync(_filePath, "[]");
            _logger.LogInformation("Cleared test history");
        }
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}