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

    [Fact]
    public void EnrichPGT_IncludesMinorAndIncapableCreditorPeopleWhenCreditorOrganizationIsApplicant()
    {
        var payload = new JObject
        {
            ["deceasedName"] = "Test Deceased",
            ["applicant"] = new JObject { ["applicantName"] = "Creditor Organization" },
            ["creditor"] = new JObject
            {
                ["creditorOrgData"] = new JArray
                {
                    new JObject { ["creditorOrgName"] = "" },
                    new JObject { ["creditorOrgName"] = "Creditor Organization" },
                },
                ["creditorPersonData"] = new JArray
                {
                    new JObject { ["creditorPersonName"] = "" },
                    new JObject
                    {
                        ["creditorPersonName"] = "Minor Creditor",
                        ["creditorPersonIsAlive"] = "yes",
                        ["creditorPersonIsAdult"] = "no",
                        ["creditorPersonMinorDOB"] = "2015-03-12",
                        ["creditorPersonMinorResStreet"] = "1 Minor St",
                        ["creditorPersonMinorResCity"] = "Victoria",
                        ["creditorPersonMinorResProvince"] = "British Columbia",
                        ["creditorPersonMinorResPostal"] = "V8V 1A1",
                        ["creditorPersonMinorResCountry"] = "Canada",
                        ["creditorPersonMinorHasMailing"] = "yes",
                        ["creditorPersonMinorMailStreet"] = "2 Mail St",
                        ["creditorPersonMinorMailCity"] = "Victoria",
                        ["creditorPersonMinorMailProvince"] = "British Columbia",
                        ["creditorPersonMinorMailPostal"] = "V8V 2B2",
                        ["creditorPersonMinorMailCountry"] = "Canada",
                        ["creditorPersonMinorHasEmail"] = "yes",
                        ["creditorPersonMinorEmail"] = "minor@example.com",
                        ["creditorPersonHasGuardian"] = "yes",
                        ["creditorPersonGuardianName"] = "Minor Guardian",
                        ["creditorPersonGuardianResStreet"] = "3 Guardian St",
                        ["creditorPersonGuardianResCity"] = "Victoria",
                        ["creditorPersonGuardianResProvince"] = "British Columbia",
                        ["creditorPersonGuardianResPostal"] = "V8V 3C3",
                        ["creditorPersonGuardianResCountry"] = "Canada",
                    },
                    new JObject
                    {
                        ["creditorPersonName"] = "Incapable Creditor",
                        ["creditorPersonIsAlive"] = "yes",
                        ["creditorPersonIsAdult"] = "yes",
                        ["creditorPersonIsCompetent"] = "no",
                        ["creditorPersonIncompetentDOB"] = "1975-06-20",
                        ["creditorPersonIncompetentResStreet"] = "4 Incompetent St",
                        ["creditorPersonIncompetentResCity"] = "Victoria",
                        ["creditorPersonIncompetentResProvince"] = "British Columbia",
                        ["creditorPersonIncompetentResPostal"] = "V8V 4D4",
                        ["creditorPersonIncompetentResCountry"] = "Canada",
                        ["creditorPersonIncompetentHasEmail"] = "yes",
                        ["creditorPersonIncompetentEmail"] = "adult@example.com",
                        ["creditorPersonHasNominee"] = "yes",
                        ["creditorPersonNomineeName"] = "Adult Nominee",
                        ["creditorPersonNomineeResStreet"] = "5 Nominee St",
                        ["creditorPersonNomineeResCity"] = "Victoria",
                        ["creditorPersonNomineeResProvince"] = "British Columbia",
                        ["creditorPersonNomineeResPostal"] = "V8V 5E5",
                        ["creditorPersonNomineeResCountry"] = "Canada",
                    },
                },
            },
        };

        var result = (JObject)SubmissionEnricher.EnrichPGT(payload);
        var minor = (JObject)result["computedMinors"]![0]!;
        var adult = (JObject)result["computedIncapableAdults"]![0]!;

        Assert.True(result["hasMinors"]!.Value<bool>());
        Assert.Equal("Minor Creditor", minor["minorName"]?.Value<string>());
        Assert.Equal("creditor", minor["minorRole"]?.Value<string>());
        Assert.Equal("March 12, 2015", minor["minorDOB"]?.Value<string>());
        Assert.Equal(
            "2 Mail St, Victoria, British Columbia, V8V 2B2, Canada",
            minor["minorPostalAddress"]?.Value<string>()
        );
        Assert.Equal("minor@example.com", minor["minorEmail"]?.Value<string>());
        Assert.Equal("Minor Guardian", minor["minorGuardianName"]?.Value<string>());
        Assert.Equal(
            "3 Guardian St, Victoria, British Columbia, V8V 3C3, Canada",
            minor["minorGuardianResAddress"]?.Value<string>()
        );

        Assert.True(result["hasIncapableAdults"]!.Value<bool>());
        Assert.Equal("Incapable Creditor", adult["incapableAdultName"]?.Value<string>());
        Assert.Equal("creditor", adult["incapableAdultRole"]?.Value<string>());
        Assert.Equal("June 20, 1975", adult["incapableAdultDOB"]?.Value<string>());
        Assert.Equal(
            "4 Incompetent St, Victoria, British Columbia, V8V 4D4, Canada",
            adult["incapableAdultResAddress"]?.Value<string>()
        );
        Assert.Equal("adult@example.com", adult["incapableAdultEmail"]?.Value<string>());
        Assert.Equal("Adult Nominee", adult["incapableAdultNomineeName"]?.Value<string>());
        Assert.Equal(
            "5 Nominee St, Victoria, British Columbia, V8V 5E5, Canada",
            adult["incapableAdultNomineeResAddress"]?.Value<string>()
        );
    }

    [Theory]
    [InlineData("no", "", "yes", "", "", "Creditor Org", "s130g", false)]
    [InlineData("yes", "spouse::Sam Spouse", "", "", "", "Nina Nominee", "s130a", true)]
    [InlineData("yes", "child::Cal Child", "", "yes", "", "Nina Nominee", "s130c", true)]
    [InlineData("yes", "grandchild::Gail Grand", "", "", "yes", "Nina Nominee", "s130e1", true)]
    [InlineData("yes", "parent::Pat Parent", "", "", "yes", "Nina Nominee", "s130e1", true)]
    [InlineData("yes", "sibling::Sib Sibling", "", "", "yes", "Nina Nominee", "s130e1", true)]
    [InlineData("", "", "", "", "", "Sam Spouse", "s130a", false)]
    public void EnrichP5_DerivesSection130Pathway(
        string nominated,
        string nominator,
        string courtAppointed,
        string childConsent,
        string intestateConsent,
        string applicantName,
        string expectedPathway,
        bool expectedNominated
    )
    {
        var payload = P5Payload(
            new JObject
            {
                ["applicantName"] = applicantName,
                ["applicantHasBeenNominated"] = nominated,
                ["applicantNominator"] = nominator,
                ["applicantCourtAppointed"] = courtAppointed,
                ["applicantChildConsent"] = childConsent,
                ["applicantIntestateConsent"] = intestateConsent,
                ["applicantSection130Pathway"] = "",
            }
        );

        var applicant = (JObject)((JObject)SubmissionEnricher.EnrichP5(payload))["applicant"]!;

        Assert.Equal(expectedPathway, applicant.Value<string>("applicantSection130Pathway"));
        Assert.Equal(expectedNominated, applicant.Value<bool>("applicantSection130Nominated"));
    }

    [Fact]
    public void EnrichP5_NominatorPrefixOverridesCalculatedDirectSuccessorPathway()
    {
        var payload = P5Payload(
            new JObject
            {
                ["applicantName"] = "Nina Nominee",
                ["applicantNameSelection"] = "__other__",
                ["applicantHasBeenNominated"] = "yes",
                ["applicantNominator"] = "grandchild::Gail Grand",
                ["applicantIntestateConsent"] = "yes",
                ["applicantSection130Pathway"] = "s130e",
            }
        );

        var applicant = (JObject)((JObject)SubmissionEnricher.EnrichP5(payload))["applicant"]!;

        Assert.Equal("s130e1", applicant.Value<string>("applicantSection130Pathway"));
        Assert.True(applicant.Value<bool>("applicantSection130Nominated"));
    }

    [Theory]
    [InlineData("s130b")]
    [InlineData("s130d")]
    [InlineData("s130e")]
    [InlineData("s130f")]
    public void EnrichP5_KeepsCalculatedDirectSuccessorPathway(string pathway)
    {
        var payload = P5Payload(
            new JObject
            {
                ["applicantName"] = "Cal Child",
                ["applicantSection130Pathway"] = pathway,
            }
        );

        var applicant = (JObject)((JObject)SubmissionEnricher.EnrichP5(payload))["applicant"]!;

        Assert.Equal(pathway, applicant.Value<string>("applicantSection130Pathway"));
        Assert.False(applicant.Value<bool>("applicantSection130Nominated"));
    }

    [Fact]
    public void EnrichP5_LeavesPathwayUnsetWhenNominationConsentIsMissing()
    {
        var payload = P5Payload(
            new JObject
            {
                ["applicantName"] = "Nina Nominee",
                ["applicantHasBeenNominated"] = "yes",
                ["applicantNominator"] = "child::Cal Child",
                ["applicantChildConsent"] = "no",
                ["applicantSection130Pathway"] = "",
            }
        );

        var applicant = (JObject)((JObject)SubmissionEnricher.EnrichP5(payload))["applicant"]!;

        Assert.Equal("", applicant.Value<string>("applicantSection130Pathway"));
        Assert.False(applicant.Value<bool>("applicantSection130Nominated"));
    }

    private static JObject P5Payload(JObject applicant) =>
        new()
        {
            ["applicant"] = applicant,
            ["spouse"] = new JObject
            {
                ["spouseData"] = new JArray
                {
                    new JObject { ["spouseName"] = "" },
                    new JObject { ["spouseName"] = "Sam Spouse" },
                },
            },
        };
}
