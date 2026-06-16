using System;

namespace Probate.Api.Models;

public class FormSchemaDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BasedOnId { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Schema { get; set; } = "{}";
}

public class CreateFormSchemaRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BasedOnId { get; set; }
    public string Schema { get; set; } = "{}";
}

public class UpdateFormSchemaRequest
{
    public string Schema { get; set; } = "{}";
    public string? Description { get; set; }
}
