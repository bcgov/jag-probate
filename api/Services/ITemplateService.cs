using System.Collections.Generic;

namespace Probate.Api.Services;

public interface ITemplateService
{
    IReadOnlyList<string> RenderTemplates(string templateKey, object submissionData);
}
