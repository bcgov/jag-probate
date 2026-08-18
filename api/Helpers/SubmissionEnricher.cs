using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Probate.Api.Helpers;

/// <summary>
/// Enriches raw CHEFS submission data with computed fields before passing to CDOGS.
/// Only fields that cannot be expressed in a Carbone template are computed here.
/// </summary>
public static class SubmissionEnricher
{
    /// <summary>
    /// Enriches PGT submission data with computed fields:
    /// - hasMinors / computedMinors: alive minors from spouseData + childData
    /// - hasIncapableAdults / computedIncapableAdults: alive incapable adults from spouseData + childData
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
            if (c.Value<string>("childIsAlive") != "yes")
                continue;

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

    /// <summary>
    /// Enriches P9 submission data with delivery groups:
    /// - hasInPerson / deliveredInPerson
    /// - hasByMail / deliveredByMail
    /// - hasElectronic / deliveredElectronic
    /// </summary>
    public static object EnrichP9(object submissionData)
    {
        var root = submissionData is JObject jo
            ? jo
            : JObject.Parse(JsonSerializer.Serialize(submissionData));

        var notifyData = root.SelectToken("notifyPeople.notifyPeopleData") as JArray;

        var inPerson = new List<JObject>();
        var byMail = new List<JObject>();
        var electronic = new List<JObject>();

        if (notifyData != null)
        {
            foreach (
                var item in notifyData
                    .OfType<JObject>()
                    .Where(item => item.Value<string>("p1Delivered") == "yes")
            )
            {
                var name = item.Value<string>("recipientName") ?? "";
                var role = item.Value<string>("recipientRole") ?? "";
                var displayName = !string.IsNullOrWhiteSpace(role) ? $"{name} ({role})" : name;

                var entry = new JObject
                {
                    ["recipientName"] = displayName,
                    ["deliveryDate"] = FormatDate(item.Value<string>("deliveryDate")),
                };

                switch (item.Value<string>("deliveryMethod"))
                {
                    case "inperson":
                        inPerson.Add(entry);
                        break;
                    case "mail":
                        byMail.Add(entry);
                        break;
                    case "electronic":
                        electronic.Add(entry);
                        break;
                }
            }
        }

        root["hasInPerson"] = inPerson.Count > 0;
        root["deliveredInPerson"] = new JArray(inPerson);
        root["hasByMail"] = byMail.Count > 0;
        root["deliveredByMail"] = new JArray(byMail);
        root["hasElectronic"] = electronic.Count > 0;
        root["deliveredElectronic"] = new JArray(electronic);

        return root;
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

        if (
            NameExistsInArray(
                root,
                "creditor.creditorPersonData",
                "creditorPersonName",
                applicantName
            )
        )
            return "Creditor";

        return "";
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
