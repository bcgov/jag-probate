using System;
using System.Collections.Generic;
using System.IO;
using Fluid;
using Fluid.Filters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Options;
using AppTemplateOptions = Probate.Api.Infrastructure.Options.TemplateOptions;

namespace Probate.Api.Services;

public class TemplateService : ITemplateService
{
    private readonly Dictionary<string, string[]> _templateGroups;
    private readonly string _templatesPath;
    private readonly ILogger<TemplateService> _logger;
    private readonly Fluid.TemplateOptions _fluidOptions = CreateOptions();

    private static Fluid.TemplateOptions CreateOptions()
    {
        var options = new Fluid.TemplateOptions();
        options.Filters.WithMoneyFilters();
        options.MoneyOptions.Currency = "CAD";
        options.MoneyOptions.MoneyFormat = "{{amount}}";
        return options;
    }

    public TemplateService(
        IWebHostEnvironment env,
        IOptions<AppTemplateOptions> options,
        ILogger<TemplateService> logger
    )
    {
        _logger = logger;
        _templatesPath = Path.Join(env.ContentRootPath, "Templates");
        _templateGroups = new Dictionary<string, string[]>(
            options.Value.Groups,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public IReadOnlyList<string> RenderTemplates(string templateKey, object submissionData) =>
        RenderTemplates(templateKey, submissionData, null);

    public IReadOnlyList<string> RenderTemplates(
        string templateKey,
        object submissionData,
        string? timeZoneId
    )
    {
        var timeZone = ResolveTimeZone(timeZoneId);

        if (!_templateGroups.TryGetValue(templateKey, out var fileNames) || fileNames.Length == 0)
            throw new InvalidOperationException(
                $"Template key '{templateKey}' is not recognized. "
                    + $"Valid keys: {string.Join(", ", _templateGroups.Keys)}"
            );

        var documents = new List<string>(fileNames.Length);
        foreach (var fileName in fileNames)
        {
            if (Path.IsPathRooted(fileName) || fileName != Path.GetFileName(fileName))
                throw new InvalidOperationException(
                    $"Template filename '{fileName}' for key '{templateKey}' is invalid."
                );

            var basePath = Path.GetFullPath(_templatesPath);
            var filePath = Path.GetFullPath(Path.Join(basePath, fileName));

            if (
                !filePath.StartsWith(
                    basePath + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new InvalidOperationException(
                    $"Template filename '{fileName}' for key '{templateKey}' resolves outside the templates directory."
                );

            if (!File.Exists(filePath))
            {
                _logger.LogError("Template file not found at {FilePath}", filePath);
                throw new FileNotFoundException(
                    $"Template file for key '{templateKey}' was not found on the server.",
                    filePath
                );
            }

            var parser = new FluidParser();
            if (!parser.TryParse(File.ReadAllText(filePath), out var template, out var error))
                throw new InvalidOperationException(
                    $"Template file '{fileName}' could not be parsed: {error}"
                );

            var options = _fluidOptions;
            if (timeZone != null)
            {
                options = CreateOptions();
                options.TimeZone = timeZone;
                options.Now = () => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
            }

            var context = new TemplateContext(options);
            context.SetValue("Model", submissionData);
            documents.Add(template.Render(context));
        }

        return documents;
    }

    private TimeZoneInfo? ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning("Unrecognized client time zone; using server time zone.");
            return null;
        }
    }
}
