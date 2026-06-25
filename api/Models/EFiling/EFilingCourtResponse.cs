using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Probate.Api.Models.EFiling;

/// <summary>
/// eFiling Hub API response for courts endpoint
/// </summary>
public class EFilingCourtsResponse
{
    [JsonPropertyName("courts")]
    public List<EFilingCourt>? Courts { get; set; }
}

/// <summary>
/// Individual court information from eFiling Hub
/// </summary>
public class EFilingCourt
{
    [JsonPropertyName("id")]
    public double? Id { get; set; }

    [JsonPropertyName("identifierCode")]
    public string? IdentifierCode { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("isSupremeCourt")]
    public bool IsSupremeCourt { get; set; }

    [JsonPropertyName("address")]
    public EFilingAddress? Address { get; set; }
}

/// <summary>
/// Court address from eFiling Hub
/// </summary>
public class EFilingAddress
{
    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("addressLine3")]
    public string? AddressLine3 { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("cityName")]
    public string? CityName { get; set; }

    [JsonPropertyName("provinceName")]
    public string? ProvinceName { get; set; }

    [JsonPropertyName("countryName")]
    public string? CountryName { get; set; }
}
