using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Probate.Api.Models;

/// <summary>
/// Response model for court locations API endpoint
/// </summary>
public class CourtLocationResult
{
    [JsonPropertyName("courts")]
    public List<CourtLocationModel> Courts { get; set; } = new();
}

/// <summary>
/// Represents a court location from eFiling Hub API
/// </summary>
public class CourtLocationModel
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
    public CourtAddress? Address { get; set; }
}

/// <summary>
/// Represents a court address
/// </summary>
public class CourtAddress
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
