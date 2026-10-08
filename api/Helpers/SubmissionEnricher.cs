using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Probate.Api.Helpers;

/// <summary>
/// Enriches raw CHEFS submission data with computed fields before PDF rendering.
/// Only fields that must be computed before template rendering are added here.
/// </summary>
public static class SubmissionEnricher
{
    /// <summary>
    /// Enriches PGT submission data with computed fields:
    /// - hasMinors / computedMinors: alive minors from spouseData + childData + siblingData + gchildData
    /// - hasIncapableAdults / computedIncapableAdults: alive incapable adults from
    ///   spouseData + childData + parentData + siblingData + gchildData
    /// Parent has no isAdult/minor concept (always adult) - only the
    /// incapable-adult branch applies to it. Grandchildren (nested under each
    /// child row) are only collected when their parent child was bypassed as a
    /// successor (child died BEFORE the 5-day survivorship cutoff) - a surviving
    /// child's own children are not in scope.
    /// </summary>
    public static object EnrichPGT(object submissionData)
    {
        var root = submissionData is JObject jo
            ? jo
            : JObject.Parse(JsonSerializer.Serialize(submissionData));

        var deceasedName = root.Value<string>("deceasedName") ?? "";

        var minors = new List<JObject>();
        var incapableAdults = new List<JObject>();

        CollectFromSpouseData(root, deceasedName, minors, incapableAdults);
        CollectFromChildData(root, deceasedName, minors, incapableAdults);
        CollectFromParentData(root, deceasedName, minors, incapableAdults);
        CollectFromSiblingData(root, deceasedName, minors, incapableAdults);

        root["hasMinors"] = minors.Count > 0;
        root["computedMinors"] = new JArray(minors);
        root["hasIncapableAdults"] = incapableAdults.Count > 0;
        root["computedIncapableAdults"] = new JArray(incapableAdults);
        root["applicantRelationship"] = DeduceApplicantRelationship(root);

        return root;
    }

    private static void CollectFromSpouseData(
        JObject root,
        string deceasedName,
        List<JObject> minors,
        List<JObject> incapableAdults
    )
    {
        var spouseData = root.SelectToken("spouse.spouseData") as JArray;
        if (spouseData == null)
            return;

        for (int i = 1; i < spouseData.Count; i++)
        {
            var s = spouseData[i] as JObject;
            if (s == null)
                continue;
            if (s.Value<string>("spouseIsAlive") != "yes")
                continue;

            if (s.Value<string>("spouseIsAdult") == "no")
            {
                minors.Add(BuildMinor(s, "spouse", deceasedName, "spouse"));
            }
            else if (
                s.Value<string>("spouseIsAdult") == "yes"
                && s.Value<string>("spouseIsCompetent") == "no"
            )
            {
                incapableAdults.Add(BuildIncapableAdult(s, "spouse", deceasedName, "spouse"));
            }
        }
    }

    private static void CollectFromChildData(
        JObject root,
        string deceasedName,
        List<JObject> minors,
        List<JObject> incapableAdults
    )
    {
        var childData = root.SelectToken("child.childData") as JArray;
        if (childData == null)
            return;

        for (int i = 1; i < childData.Count; i++)
        {
            var c = childData[i] as JObject;
            if (c == null)
                continue;

            if (c.Value<string>("childIsAlive") == "yes")
            {
                if (c.Value<string>("childIsAdult") == "no")
                {
                    minors.Add(BuildMinor(c, "child", deceasedName, "child"));
                }
                else if (
                    c.Value<string>("childIsAdult") == "yes"
                    && c.Value<string>("childIsCompetent") == "no"
                )
                {
                    incapableAdults.Add(BuildIncapableAdult(c, "child", deceasedName, "child"));
                }
            }

            // Grandchildren only matter when their parent child was bypassed
            // (died BEFORE the 5-day survivorship cutoff).
            var childBypassed =
                c.Value<string>("childIsAlive") == "no"
                && c.Value<string>("childDied5DaysAfter") == "no";
            if (childBypassed)
            {
                CollectFromGrandchildData(c, deceasedName, minors, incapableAdults);
            }
        }
    }

    private static void CollectFromGrandchildData(
        JObject childRow,
        string deceasedName,
        List<JObject> minors,
        List<JObject> incapableAdults
    )
    {
        var gchildData = childRow.SelectToken("gchildData") as JArray;
        if (gchildData == null)
            return;

        for (int i = 1; i < gchildData.Count; i++)
        {
            var g = gchildData[i] as JObject;
            if (g == null)
                continue;
            if (g.Value<string>("grandchildIsAlive") != "yes")
                continue;

            if (g.Value<string>("grandchildIsAdult") == "no")
            {
                minors.Add(BuildMinor(g, "grandchild", deceasedName, "grandchild"));
            }
            else if (
                g.Value<string>("grandchildIsAdult") == "yes"
                && g.Value<string>("grandchildIsCompetent") == "no"
            )
            {
                incapableAdults.Add(
                    BuildIncapableAdult(g, "grandchild", deceasedName, "grandchild")
                );
            }
        }
    }

    // Parent has no isAdult/guardian concept at all (always treated as an
    // adult) - only the incapable-adult (competent/nominee) branch applies.
    private static void CollectFromParentData(
        JObject root,
        string deceasedName,
        List<JObject> minors,
        List<JObject> incapableAdults
    )
    {
        var parentData = root.SelectToken("parent.parentData") as JArray;
        if (parentData == null)
            return;

        for (int i = 1; i < parentData.Count; i++)
        {
            var p = parentData[i] as JObject;
            if (p == null)
                continue;
            if (p.Value<string>("parentIsAlive") != "yes")
                continue;

            if (p.Value<string>("parentIsCompetent") == "no")
            {
                incapableAdults.Add(BuildIncapableAdult(p, "parent", deceasedName, "parent"));
            }
        }
    }

    private static void CollectFromSiblingData(
        JObject root,
        string deceasedName,
        List<JObject> minors,
        List<JObject> incapableAdults
    )
    {
        var siblingData = root.SelectToken("sibling.siblingData") as JArray;
        if (siblingData == null)
            return;

        for (int i = 1; i < siblingData.Count; i++)
        {
            var s = siblingData[i] as JObject;
            if (s == null)
                continue;
            if (s.Value<string>("siblingIsAlive") != "yes")
                continue;

            if (s.Value<string>("siblingIsAdult") == "no")
            {
                minors.Add(BuildMinor(s, "sibling", deceasedName, "sibling"));
            }
            else if (
                s.Value<string>("siblingIsAdult") == "yes"
                && s.Value<string>("siblingIsCompetent") == "no"
            )
            {
                incapableAdults.Add(BuildIncapableAdult(s, "sibling", deceasedName, "sibling"));
            }
        }
    }

    private static JObject BuildMinor(JObject src, string prefix, string deceasedName, string role)
    {
        var name = src.Value<string>($"{prefix}Name") ?? "";
        var p = $"{prefix}Minor";
        var g = $"{prefix}Guardian";

        var resAddress = ExtractFormatAddress(src, p);

        var postalAddress =
            src.Value<string>($"{p}HasDiffMail") == "yes"
                ? ExtractFormatAddress(src, p, "Mail")
                : resAddress;

        var guardianResAddress = ExtractFormatAddress(src, g);

        var guardianPostalAddress =
            src.Value<string>($"{g}HasDiffMail") == "yes"
                ? ExtractFormatAddress(src, g, "Mail")
                : guardianResAddress;

        return new JObject
        {
            ["minorName"] = name,
            ["minorRelationship"] = deceasedName,
            ["minorRole"] = role,
            ["minorDOB"] = FormatDate(src.Value<string>($"{p}DOB")),
            ["minorResAddress"] = resAddress,
            ["minorPostalAddress"] = postalAddress,
            ["minorEmail"] = NoneIfEmpty(
                src.Value<string>($"{p}Email"),
                src.Value<string>($"{p}HasEmail")
            ),
            ["minorFax"] = NoneIfEmpty(
                src.Value<string>($"{p}Fax"),
                src.Value<string>($"{p}HasFax")
            ),
            ["minorGuardianName"] =
                src.Value<string>($"{prefix}HasGuardian") == "yes"
                    ? (src.Value<string>($"{g}Name") ?? "None")
                    : "None",
            ["minorGuardianResAddress"] = guardianResAddress,
            ["minorGuardianPostalAddress"] = guardianPostalAddress,
            ["minorGuardianEmail"] = NoneIfEmpty(
                src.Value<string>($"{g}Email"),
                src.Value<string>($"{g}HasEmail")
            ),
            ["minorGuardianFax"] = NoneIfEmpty(
                src.Value<string>($"{g}Fax"),
                src.Value<string>($"{g}HasFax")
            ),
        };
    }

    private static JObject BuildIncapableAdult(
        JObject src,
        string prefix,
        string deceasedName,
        string role
    )
    {
        var name = src.Value<string>($"{prefix}Name") ?? "";
        var p = $"{prefix}Incomp";
        var n = $"{prefix}Nominee";

        var resAddress = ExtractFormatAddress(src, p);

        var postalAddress =
            src.Value<string>($"{p}HasDiffMail") == "yes"
                ? ExtractFormatAddress(src, p, "Mail")
                : resAddress;

        var nomineeResAddress = ExtractFormatAddress(src, n);

        var nomineePostalAddress =
            src.Value<string>($"{n}HasDiffMail") == "yes"
                ? ExtractFormatAddress(src, n, "Mail")
                : nomineeResAddress;

        return new JObject
        {
            ["incapableAdultName"] = name,
            ["incapableAdultRelationship"] = deceasedName,
            ["incapableAdultRole"] = role,
            ["incapableAdultDOB"] = FormatDate(src.Value<string>($"{p}DOB")),
            ["incapableAdultResAddress"] = resAddress,
            ["incapableAdultPostalAddress"] = postalAddress,
            ["incapableAdultEmail"] = NoneIfEmpty(
                src.Value<string>($"{p}Email"),
                src.Value<string>($"{p}HasEmail")
            ),
            ["incapableAdultFax"] = NoneIfEmpty(
                src.Value<string>($"{p}Fax"),
                src.Value<string>($"{p}HasFax")
            ),
            ["incapableAdultNomineeName"] =
                src.Value<string>($"{prefix}HasNominee") == "yes"
                    ? (src.Value<string>($"{n}Name") ?? "None")
                    : "None",
            ["incapableAdultNomineeResAddress"] = nomineeResAddress,
            ["incapableAdultNomineePostalAddress"] = nomineePostalAddress,
            ["incapableAdultNomineeEmail"] = NoneIfEmpty(
                src.Value<string>($"{n}Email"),
                src.Value<string>($"{n}HasEmail")
            ),
            ["incapableAdultNomineeFax"] = NoneIfEmpty(
                src.Value<string>($"{n}Fax"),
                src.Value<string>($"{n}HasFax")
            ),
        };
    }

    private static string ExtractFormatAddress(
        JObject src,
        string dataNamePrefix,
        string fieldPrefix = ""
    )
    {
        return FormatAddress(
            src.Value<string>($"{dataNamePrefix}{fieldPrefix}Street"),
            src.Value<string>($"{dataNamePrefix}{fieldPrefix}City"),
            src.Value<string>($"{dataNamePrefix}{fieldPrefix}Province"),
            src.Value<string>($"{dataNamePrefix}{fieldPrefix}State"),
            src.Value<string>($"{dataNamePrefix}{fieldPrefix}ProvinceText"),
            src.Value<string>($"{dataNamePrefix}{fieldPrefix}Postal"),
            src.Value<string>($"{dataNamePrefix}{fieldPrefix}Country")
        );
    }

    private static string FormatAddress(
        string? street,
        string? city,
        string? province,
        string? state,
        string? provinceText,
        string? postal,
        string? country
    )
    {
        var region = BuildRegion(province, state, provinceText);
        var parts = new[] { street, city, region, postal, country }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        return parts.Length > 0 ? string.Join(", ", parts) : "None";
    }

    private static string BuildRegion(string? province, string? state, string? provinceText)
    {
        if (!string.IsNullOrWhiteSpace(province))
            return province.Trim();
        if (!string.IsNullOrWhiteSpace(state))
            return state.Trim();
        if (!string.IsNullOrWhiteSpace(provinceText))
            return provinceText.Trim();
        return string.Empty;
    }

    private static string FormatDate(string? isoDate)
    {
        if (string.IsNullOrWhiteSpace(isoDate))
            return "None";
        if (
            DateTimeOffset.TryParse(
                isoDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt
            )
        )
            return dt.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        return isoDate;
    }

    private static string NoneIfEmpty(string? value, string? hasFlag)
    {
        if (hasFlag == "yes" && !string.IsNullOrWhiteSpace(value))
            return value;
        return "None";
    }

    private static string DeduceApplicantRelationship(JObject root)
    {
        var applicantName = root.SelectToken("applicant.applicantName")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(applicantName))
            return "";

        if (NameExistsInArray(root, "spouse.spouseData", "spouseName", applicantName))
            return "Spouse";

        if (NameExistsInArray(root, "child.childData", "childName", applicantName))
            return "Child";

        if (NameExistsInArray(root, "parent.parentData", "parentName", applicantName))
            return "Parent";

        if (NameExistsInArray(root, "sibling.siblingData", "siblingName", applicantName))
            return "Sibling";

        if (
            NameExistsInArray(
                root,
                "creditor.creditorPersonData",
                "creditorPersonName",
                applicantName
            )
        )
            return "Creditor";

        if (NameExistsInGrandchildData(root, applicantName))
            return "Grandchild";

        return "";
    }

    private static bool NameExistsInGrandchildData(JObject root, string applicantName)
    {
        var childData = root.SelectToken("child.childData") as JArray;
        if (childData == null)
            return false;

        foreach (var child in childData.OfType<JObject>())
        {
            var gchildData = child.SelectToken("gchildData") as JArray;
            if (gchildData == null)
                continue;

            if (
                gchildData
                    .OfType<JObject>()
                    .Any(g =>
                        string.Equals(
                            g.Value<string>("grandchildName")?.Trim(),
                            applicantName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
            )
                return true;
        }

        return false;
    }

    private static bool NameExistsInArray(
        JObject root,
        string arrayPath,
        string nameField,
        string applicantName
    )
    {
        var array = root.SelectToken(arrayPath) as JArray;
        if (array == null)
            return false;

        return array
            .OfType<JObject>()
            .Any(item =>
                string.Equals(
                    item.Value<string>(nameField)?.Trim(),
                    applicantName,
                    StringComparison.OrdinalIgnoreCase
                )
            );
    }
}
