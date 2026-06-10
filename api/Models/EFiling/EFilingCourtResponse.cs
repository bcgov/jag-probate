using System.Collections.Generic;
using Newtonsoft.Json;

namespace Probate.Api.Models.EFiling;

/// <summary>
/// eFiling Hub API response for courts endpoint
/// </summary>
public class EFilingCourtsResponse
{
    [JsonProperty("courts")]
    public List<EFilingCourt>? Courts { get; set; }
}

/// <summary>
/// Individual court information from eFiling Hub
/// </summary>
public class EFilingCourt
{
    [JsonProperty("id")]
    public double? Id { get; set; }

    [JsonProperty("identifierCode")]
    public string? IdentifierCode { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("code")]
    public string? Code { get; set; }

    [JsonProperty("isSupremeCourt")]
    public bool IsSupremeCourt { get; set; }

    [JsonProperty("address")]
    public EFilingAddress? Address { get; set; }
}

/// <summary>
/// Court address from eFiling Hub
/// </summary>
public class EFilingAddress
{
    [JsonProperty("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonProperty("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonProperty("addressLine3")]
    public string? AddressLine3 { get; set; }

    [JsonProperty("postalCode")]
    public string? PostalCode { get; set; }

    [JsonProperty("cityName")]
    public string? CityName { get; set; }

    [JsonProperty("provinceName")]
    public string? ProvinceName { get; set; }

    [JsonProperty("countryName")]
    public string? CountryName { get; set; }
}
