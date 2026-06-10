using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Probate.Api.Services;

public class TemplateService : ITemplateService
{
    // Allowlist maps logical key (case-insensitive) → filename under Templates/.
    // Add new templates here as they are authored.
    private static readonly Dictionary<string, string> TemplateFileMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["P1"]  = "P1.docx",
            ["P9"]  = "P9.docx",
            ["PGT"] = "PGT.docx",
        };

    private readonly string _templatesPath;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(IWebHostEnvironment env, ILogger<TemplateService> logger)
    {
        _logger = logger;
        _templatesPath = Path.Combine(env.ContentRootPath, "Templates");
    }

    public string GetTemplateBase64(string templateKey)
    {
        if (!TemplateFileMap.TryGetValue(templateKey, out var fileName))
            throw new InvalidOperationException(
                $"Template key '{templateKey}' is not recognized. "
                    + $"Valid keys: {string.Join(", ", TemplateFileMap.Keys)}"
            );

        var filePath = Path.Combine(_templatesPath, fileName);

        if (!File.Exists(filePath))
        {
            _logger.LogError("Template file not found at {FilePath}", filePath);
            throw new FileNotFoundException(
                $"Template file for key '{templateKey}' was not found on the server.",
                filePath
            );
        }

        return Convert.ToBase64String(File.ReadAllBytes(filePath));
    }
}
