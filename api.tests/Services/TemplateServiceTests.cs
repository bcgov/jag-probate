using System;
using System.Collections.Generic;
using System.IO;
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
}
