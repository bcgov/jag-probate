namespace Probate.Api.Services;

public interface ITemplateService
{
    /// <summary>
    /// Returns the base64-encoded content of the .docx template for the given key.
    /// Throws <see cref="InvalidOperationException"/> if the key is not in the allowlist.
    /// Throws <see cref="FileNotFoundException"/> if the file is missing from disk.
    /// </summary>
    string GetTemplateBase64(string templateKey);
}
