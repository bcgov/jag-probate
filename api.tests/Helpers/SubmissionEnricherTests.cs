using Newtonsoft.Json.Linq;
using Probate.Api.Helpers;

namespace Probate.Api.Tests.Helpers;

public class SubmissionEnricherTests
{
    [Fact]
    public void EnrichPGT_UsesProvinceWhenItIsTheOnlyPopulatedRegionField()
    {
        var payload = new JObject
        {
            ["deceasedName"] = "Test Deceased",
            ["spouse"] = new JObject
            {
                ["spouseData"] = new JArray
                {
                    new JObject { ["spouseName"] = "ignored" },
                    new JObject
                    {
                        ["spouseName"] = "Alice",
                        ["spouseIsAlive"] = "yes",
                        ["spouseIsAdult"] = "no",
                        ["spouseMinorDOB"] = "2020-01-15",
                        ["spouseMinorStreet"] = "123 Main St",
                        ["spouseMinorCity"] = "Vancouver",
                        ["spouseMinorProvince"] = "British Columbia",
                        ["spouseMinorState"] = "",
                        ["spouseMinorProvinceText"] = "",
                        ["spouseMinorPostal"] = "V5K 1A1",
                        ["spouseMinorCountry"] = "Canada",
                        ["spouseMinorHasEmail"] = "yes",
                        ["spouseMinorEmail"] = "alice@example.com",
                        ["spouseMinorHasFax"] = "no",
                        ["spouseMinorHasGuardian"] = "no",
                    },
                },
            },
        };

        var result = (JObject)SubmissionEnricher.EnrichPGT(payload);
        var minor = (JObject)result["computedMinors"]![0]!;

        Assert.Equal(
            "123 Main St, Vancouver, British Columbia, V5K 1A1, Canada",
            minor["minorResAddress"]?.Value<string>()
        );
    }

    [Fact]
    public void EnrichPGT_UsesStateWhenItIsTheOnlyPopulatedRegionField()
    {
        var payload = new JObject
        {
            ["deceasedName"] = "Test Deceased",
            ["spouse"] = new JObject
            {
                ["spouseData"] = new JArray
                {
                    new JObject { ["spouseName"] = "ignored" },
                    new JObject
                    {
                        ["spouseName"] = "Alice",
                        ["spouseIsAlive"] = "yes",
                        ["spouseIsAdult"] = "no",
                        ["spouseMinorDOB"] = "2020-01-15",
                        ["spouseMinorStreet"] = "123 Main St",
                        ["spouseMinorCity"] = "Seattle",
                        ["spouseMinorProvince"] = "",
                        ["spouseMinorState"] = "WA",
                        ["spouseMinorProvinceText"] = "",
                        ["spouseMinorPostal"] = "98101",
                        ["spouseMinorCountry"] = "United States",
                        ["spouseMinorHasEmail"] = "yes",
                        ["spouseMinorEmail"] = "alice@example.com",
                        ["spouseMinorHasFax"] = "no",
                        ["spouseMinorHasGuardian"] = "no",
                    },
                },
            },
        };

        var result = (JObject)SubmissionEnricher.EnrichPGT(payload);
        var minor = (JObject)result["computedMinors"]![0]!;

        Assert.Equal(
            "123 Main St, Seattle, WA, 98101, United States",
            minor["minorResAddress"]?.Value<string>()
        );
    }
}
