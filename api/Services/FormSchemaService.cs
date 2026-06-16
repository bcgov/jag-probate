using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Probate.Api.Models;

namespace Probate.Api.Services;

public interface IFormSchemaService
{
    Task<IEnumerable<FormSchemaDto>> ListAsync();
    Task<FormSchemaDto?> GetAsync(string id);
    Task<FormSchemaDto> CreateAsync(CreateFormSchemaRequest request);
    Task<FormSchemaDto?> UpdateAsync(string id, UpdateFormSchemaRequest request);
    Task<bool> DeleteAsync(string id);
}

public class FormSchemaService : IFormSchemaService
{
    private readonly string _storePath;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public FormSchemaService(IConfiguration configuration)
    {
        _storePath =
            configuration["FormSchemas:StorePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "form-schemas");
        Directory.CreateDirectory(_storePath);
    }

    public Task<IEnumerable<FormSchemaDto>> ListAsync()
    {
        IEnumerable<FormSchemaDto> schemas = Directory
            .EnumerateFiles(_storePath, "*.json")
            .Select(LoadFile)
            .OfType<FormSchemaDto>()
            .OrderByDescending(s => s.CreatedAt);

        return Task.FromResult(schemas);
    }

    public Task<FormSchemaDto?> GetAsync(string id)
    {
        var file = SafePath(id);
        if (file is null || !File.Exists(file))
            return Task.FromResult<FormSchemaDto?>(null);

        return Task.FromResult(LoadFile(file));
    }

    public async Task<FormSchemaDto> CreateAsync(CreateFormSchemaRequest request)
    {
        var dto = new FormSchemaDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Description = request.Description,
            BasedOnId = request.BasedOnId,
            Version = await NextVersionAsync(request.Name),
            CreatedAt = DateTime.UtcNow,
            Schema = request.Schema,
        };

        await SaveFile(dto);
        return dto;
    }

    public async Task<FormSchemaDto?> UpdateAsync(string id, UpdateFormSchemaRequest request)
    {
        var existing = await GetAsync(id);
        if (existing is null)
            return null;

        existing.Schema = request.Schema;
        existing.Description = request.Description ?? existing.Description;

        await SaveFile(existing);
        return existing;
    }

    public Task<bool> DeleteAsync(string id)
    {
        var file = SafePath(id);
        if (file is null || !File.Exists(file))
            return Task.FromResult(false);

        File.Delete(file);
        return Task.FromResult(true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private FormSchemaDto? LoadFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FormSchemaDto>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveFile(FormSchemaDto dto)
    {
        var path = Path.Combine(_storePath, $"{dto.Id}.json");
        var json = JsonSerializer.Serialize(dto, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    /// Returns the next version number for a given form name (1-based).
    private async Task<int> NextVersionAsync(string name)
    {
        var all = await ListAsync();
        var max = all
            .Where(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Version)
            .DefaultIfEmpty(0)
            .Max();
        return max + 1;
    }

    /// Validates the id is safe (no path traversal) and returns the full path or null.
    private string? SafePath(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;

        var candidate = Path.GetFullPath(Path.Combine(_storePath, $"{id}.json"));
        if (!candidate.StartsWith(Path.GetFullPath(_storePath) + Path.DirectorySeparatorChar))
            return null;

        return candidate;
    }
}
