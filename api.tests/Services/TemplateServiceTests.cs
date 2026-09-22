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
