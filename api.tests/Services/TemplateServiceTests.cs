using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Services;

namespace Probate.Api.Tests.Services;

public class TemplateServiceTests
{
    [Theory]
    [InlineData("${{ Model.amount | money }}", "$1,134.65")]
    [InlineData("${{ Model.amount | money: 'CAD' }}", "$1,134.65")]
    [InlineData("${{ Model.amount | money_with_currency }}", "$1,134.65 CAD")]
    public void RenderTemplates_FormatsMoneyWithOneTemplateDollarSign(string source, string expected)
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var templatesPath = Path.Join(temporaryDirectory.FullName, "Templates");
            Directory.CreateDirectory(templatesPath);
            File.WriteAllText(Path.Join(templatesPath, "test.html"), source);

            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(value => value.ContentRootPath).Returns(temporaryDirectory.FullName);
            var options = Microsoft.Extensions.Options.Options.Create(
                new TemplateOptions
                {
                    Groups = new Dictionary<string, string[]> { ["test"] = ["test.html"] },
                }
            );
            var service = new TemplateService(
                environment.Object,
                options,
                NullLogger<TemplateService>.Instance
            );

            var documents = service.RenderTemplates(
                "test",
                new Dictionary<string, object> { ["amount"] = 1134.65m }
            );

            Assert.Equal(expected, Assert.Single(documents));
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("P10-exhibits.html")]
    [InlineData("P11-exhibits.html")]
    public void RenderTemplates_ExhibitsFormatMoneyWithOneDollarSign(string fileName)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.ContentRootPath).Returns(AppContext.BaseDirectory);
        var options = Microsoft.Extensions.Options.Options.Create(
            new TemplateOptions
            {
                Groups = new Dictionary<string, string[]> { ["exhibits"] = [fileName] },
            }
        );
        var service = new TemplateService(
            environment.Object,
            options,
            NullLogger<TemplateService>.Instance
        );

        var documents = service.RenderTemplates(
            "exhibits",
            new Dictionary<string, object>
            {
                ["realPropertyEntries"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["propertyDescription"] = "Test property",
                        ["marketValue"] = 1134.65m,
                        ["valueAtDeath"] = 1134.65m,
                    },
                },
            }
        );

        var document = Assert.Single(documents);
        Assert.Contains("<td>$1,134.65</td>", document);
        Assert.DoesNotContain("$$", document);
        Assert.DoesNotContain("$1134.65", document);
    }

    [Fact]
    public void RenderTemplates_BindsSubmissionDataToModel()
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory();
        var contentRootPath = temporaryDirectory.FullName;
        try
        {
            var templatesPath = Path.Join(contentRootPath, "Templates");
            Directory.CreateDirectory(templatesPath);
            File.WriteAllText(Path.Join(templatesPath, "test.html"), "{{ Model.applicant.name }}");

            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(value => value.ContentRootPath).Returns(contentRootPath);
            var options = Microsoft.Extensions.Options.Options.Create(
                new TemplateOptions
                {
                    Groups = new Dictionary<string, string[]> { ["test"] = ["test.html"] },
                }
            );
            var service = new TemplateService(
                environment.Object,
                options,
                NullLogger<TemplateService>.Instance
            );

            var documents = service.RenderTemplates(
                "test",
                new { applicant = new { name = "Test Name" } }
            );

            Assert.Equal("Test Name", Assert.Single(documents));
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("deceasedAddressProvince", "British Columbia")]
    [InlineData("deceasedAddressState", "Washington")]
    [InlineData("deceasedAddressProvinceText", "Bavaria")]
    public void RenderTemplates_P18IncludesAddressRegion(string field, string region)
    {
        var document = RenderTemplate("P18.html", new Dictionary<string, object>
        {
            ["deceasedAddressStreet"] = "123 Test Street",
            ["deceasedAddressCity"] = "Test City",
            [field] = region,
            ["deceasedAddressPostalCode"] = "12345",
            ["deceasedAddressCountry"] = "Test Country",
        });
        Assert.Contains(
            $"123 Test Street, Test City, {region}, 12345, Test Country",
            NormalizeText(ParseDocument(document).Root!.Value)
        );
    }

    [Fact]
    public void RenderTemplates_P10ConfiguredFilesMatchExactCaseAndRender()
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "appsettings.json")));
        var files = config.RootElement.GetProperty("Templates").GetProperty("Groups").GetProperty("P10");
        var actualNames = Directory.GetFiles(Path.Join(AppContext.BaseDirectory, "Templates")).Select(Path.GetFileName);
        foreach (var entry in files.EnumerateArray())
        {
            var fileName = entry.GetString()!;
            Assert.Contains(fileName, actualNames, StringComparer.Ordinal);
            Assert.NotEmpty(RenderTemplate(fileName, new Dictionary<string, object>()));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderTemplates_P11EmptyOrForeignOnly_RendersEmptyExhibitA(bool foreignOnly)
    {
        var data = new Dictionary<string, object>();
        if (foreignOnly)
        {
            data["hasOutsideBcProperty"] = true;
            data["realPropertyOutsideBCEntries"] = new[] { Asset("Foreign real property", 150m, 125m) };
            data["tangiblePersonalPropertyOutsideBCEntries"] = new[] { Asset("Foreign tangible property", 275m, 250m) };
        }
        var document = RenderTemplate("P11-exhibits.html", data);

        Assert.Contains("<b>EXHIBIT A</b>", document);
        Assert.Contains(
            "This is <b>Exhibit A</b> referred to in the affidavit of",
            document
        );
        Assert.Contains(
            "A commissioner for taking affidavits for British Columbia",
            document
        );
        Assert.DoesNotContain("EXHIBIT B", document);
        Assert.DoesNotContain("Foreign real property", document);
        Assert.DoesNotContain("Foreign tangible property", document);
        Assert.Equal(3, ParseDocument(document).Descendants("td").Count(cell => NormalizeText(cell.Value) == "None"));
        AssertRow(document, "TOTAL REAL PROPERTY WITHIN BRITISH COLUMBIA", "$0.00");
        AssertRow(
            document,
            "TOTAL TANGIBLE PERSONAL PROPERTY WITHIN BRITISH COLUMBIA",
            "$0.00"
        );
        AssertRow(document, "TOTAL INTANGIBLE PERSONAL PROPERTY", "$0.00");
        AssertRow(document, "GROSS VALUE OF ASSETS LESS SECURED DEBTS", "$0.00");
    }

    [Fact]
    public void RenderTemplates_P11RendersAllExhibitAAssetTypesAndTotals()
    {
        var document = RenderTemplate("P11-exhibits.html",
            new Dictionary<string, object>
            {
                ["applicant"] = new Dictionary<string, object>
                {
                    ["applicantName"] = "Applicant Name",
                },
                ["deceasedName"] = "Deceased Name",
                ["otherNameYN"] = "yes",
                ["deceasedOtherNames"] = new[]
                {
                    Name("Former", "Middle", "Name"),
                },
                ["realPropertyEntries"] = new[]
                {
                    Asset("BC real property", 110m, 100m),
                },
                ["tangiblePersonalPropertyEntries"] = new[]
                {
                    Asset("BC tangible property", 220m, 200m),
                },
                ["intangiblePersonalPropertyEntries"] = new[]
                {
                    Asset("BC intangible property", 330m, 300m),
                },
                ["intangibleOutOfBCPersonalPropertyEntries"] = new[]
                {
                    Asset("Foreign intangible property", 440m, 400m),
                },
            }
        );

        Assert.Contains("Applicant Name", document);
        Assert.Contains("Deceased Name", document);
        Assert.Contains("Former Middle Name", document);
        AssertAssetValues(document, "BC real property", "$110.00", "$100.00");
        AssertAssetValues(document, "BC tangible property", "$220.00", "$200.00");
        AssertAssetValues(document, "BC intangible property", "$330.00", "$300.00");
        AssertAssetValues(document, "Foreign intangible property", "$440.00", "$400.00");
        AssertRow(document, "TOTAL REAL PROPERTY WITHIN BRITISH COLUMBIA", "$100.00");
        AssertRow(
            document,
            "TOTAL TANGIBLE PERSONAL PROPERTY WITHIN BRITISH COLUMBIA",
            "$200.00"
        );
        AssertRow(document, "TOTAL INTANGIBLE PERSONAL PROPERTY", "$700.00");
        AssertRow(document, "GROSS VALUE OF ASSETS LESS SECURED DEBTS", "$1,000.00");
        Assert.DoesNotContain("EXHIBIT B", document);
    }

    [Fact]
    public void RenderTemplates_P11RendersDebtsAndOptionalLabels()
    {
        var document = RenderTemplate("P11-exhibits.html",
            new Dictionary<string, object>
            {
                ["realPropertyEntries"] = new[]
                {
                    Asset(
                        "Property with debt",
                        125m,
                        100m,
                        "BC Assessment",
                        Debt("Mortgage", 25m),
                        Debt("Line of credit", 10m)
                    ),
                },
            }
        );

        Assert.Contains("BC Assessment", document);
        AssertAssetValues(document, "Property with debt", "$125.00", "");
        AssertRow(document, "Less: Mortgage", "- $25.00", "");
        AssertRow(document, "Less: Line of credit", "- $10.00", "$100.00");
        AssertRow(document, "TOTAL REAL PROPERTY WITHIN BRITISH COLUMBIA", "$100.00");
        AssertRow(document, "GROSS VALUE OF ASSETS LESS SECURED DEBTS", "$100.00");
    }

    private static string RenderTemplate(string fileName, Dictionary<string, object> submissionData)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.ContentRootPath).Returns(AppContext.BaseDirectory);
        var options = Microsoft.Extensions.Options.Options.Create(
            new TemplateOptions
            {
                Groups = new Dictionary<string, string[]> { ["test"] = [fileName] },
            }
        );
        var service = new TemplateService(
            environment.Object,
            options,
            NullLogger<TemplateService>.Instance
        );

        return Assert.Single(service.RenderTemplates("test", submissionData));
    }

    private static Dictionary<string, object> Asset(
        string description,
        decimal marketValue,
        decimal valueAtDeath,
        string marketValueSource = "",
        params Dictionary<string, object>[] securedDebts
    ) => new()
    {
        ["propertyDescription"] = description,
        ["marketValue"] = marketValue,
        ["valueAtDeath"] = valueAtDeath,
        ["marketValueSource"] = marketValueSource,
        ["securedDebts"] = securedDebts,
    };

    private static Dictionary<string, object> Debt(string description, decimal amount) => new()
    {
        ["debtDescription"] = description,
        ["debtAmount"] = amount,
    };

    private static Dictionary<string, object> Name(
        string firstName,
        string middleName,
        string lastName
    ) => new()
    {
        ["firstNameGivenName1"] = firstName,
        ["middleNameS1"] = middleName,
        ["lastNameFamilyNameOrSurname1"] = lastName,
    };

    private static void AssertAssetValues(
        string document,
        string description,
        string expectedMarketValue,
        string expectedValueAtDeath
    )
    {
        var group = Assert.Single(
            ParseDocument(document).Descendants("tbody"),
            body => NormalizeText(body.Elements("tr").FirstOrDefault()?.Elements("td").FirstOrDefault()?.Value ?? "") == description);
        var values = group.Elements("tr").ElementAt(1).Elements("td").Skip(1)
            .Select(cell => NormalizeText(cell.Value));
        Assert.Equal(new[] { expectedMarketValue, expectedValueAtDeath }, values);
    }

    private static void AssertRow(string document, params string[] expected) => Assert.Contains(
        ParseDocument(document).Descendants("tr"),
        row => row.Elements("td").Select(cell => NormalizeText(cell.Value)).SequenceEqual(expected)
    );

    private static XDocument ParseDocument(string document) => XDocument.Parse(
        document
            .Replace("<!doctype html>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("&nbsp;", "&#160;", StringComparison.Ordinal)
    );

    private static string NormalizeText(string value) => string.Join(
        " ",
        value.Replace('\u00a0', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
    );
}
