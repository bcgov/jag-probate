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

        root["applicantRole"] = DetermineApplicantRole(root);

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

        var resAddress = FormatAddress(
            src.Value<string>($"{p}Street"),
            src.Value<string>($"{p}City"),
            src.Value<string>($"{p}Province"),
            src.Value<string>($"{p}Postal"),
            src.Value<string>($"{p}Country")
        );

        var postalAddress =
            src.Value<string>($"{p}HasDiffMail") == "yes"
                ? FormatAddress(
                    src.Value<string>($"{p}MailStreet"),
                    src.Value<string>($"{p}MailCity"),
                    src.Value<string>($"{p}MailProvince"),
                    src.Value<string>($"{p}MailPostal"),
                    src.Value<string>($"{p}MailCountry")
                )
                : resAddress;

        var guardianResAddress = FormatAddress(
            src.Value<string>($"{g}Street"),
            src.Value<string>($"{g}City"),
            src.Value<string>($"{g}Province"),
            src.Value<string>($"{g}Postal"),
            src.Value<string>($"{g}Country")
        );

        var guardianPostalAddress =
            src.Value<string>($"{g}HasDiffMail") == "yes"
                ? FormatAddress(
                    src.Value<string>($"{g}MailStreet"),
                    src.Value<string>($"{g}MailCity"),
                    src.Value<string>($"{g}MailProvince"),
                    src.Value<string>($"{g}MailPostal"),
                    src.Value<string>($"{g}MailCountry")
                )
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

        var resAddress = FormatAddress(
            src.Value<string>($"{p}Street"),
            src.Value<string>($"{p}City"),
            src.Value<string>($"{p}Province"),
            src.Value<string>($"{p}Postal"),
            src.Value<string>($"{p}Country")
        );

        var postalAddress =
            src.Value<string>($"{p}HasDiffMail") == "yes"
                ? FormatAddress(
                    src.Value<string>($"{p}MailStreet"),
                    src.Value<string>($"{p}MailCity"),
                    src.Value<string>($"{p}MailProvince"),
                    src.Value<string>($"{p}MailPostal"),
                    src.Value<string>($"{p}MailCountry")
                )
                : resAddress;

        var nomineeResAddress = FormatAddress(
            src.Value<string>($"{n}Street"),
            src.Value<string>($"{n}City"),
            src.Value<string>($"{n}Province"),
            src.Value<string>($"{n}Postal"),
            src.Value<string>($"{n}Country")
        );

        var nomineePostalAddress =
            src.Value<string>($"{n}HasDiffMail") == "yes"
                ? FormatAddress(
                    src.Value<string>($"{n}MailStreet"),
                    src.Value<string>($"{n}MailCity"),
                    src.Value<string>($"{n}MailProvince"),
                    src.Value<string>($"{n}MailPostal"),
                    src.Value<string>($"{n}MailCountry")
                )
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

    private static string DetermineApplicantRole(JObject root)
    {
        var applicantName =
            root.SelectToken("applicant.applicantName")?.Value<string>() ?? "";
        if (string.IsNullOrWhiteSpace(applicantName))
            return "";

        var compare = StringComparison.OrdinalIgnoreCase;

        // Check spouseData
        var spouseData = root.SelectToken("spouse.spouseData") as JArray;
        if (spouseData != null)
        {
            for (int i = 1; i < spouseData.Count; i++)
            {
                var s = spouseData[i] as JObject;
                if (s == null)
                    continue;

                if (string.Equals(s.Value<string>("spouseName"), applicantName, compare))
                    return "spouse";

                if (
                    s.Value<string>("spouseIsAdult") == "no"
                    && string.Equals(
                        s.Value<string>("spouseGuardianName"),
                        applicantName,
                        compare
                    )
                )
                    return $"guardian of {s.Value<string>("spouseName")}, a minor spouse";

                if (
                    s.Value<string>("spouseIsCompetent") == "no"
                    && string.Equals(
                        s.Value<string>("spouseNomineeName"),
                        applicantName,
                        compare
                    )
                )
                    return $"nominee of {s.Value<string>("spouseName")}, a mentally incapable spouse";
            }
        }

        // Check childData
        var childData = root.SelectToken("child.childData") as JArray;
        if (childData != null)
        {
            for (int i = 1; i < childData.Count; i++)
            {
                var c = childData[i] as JObject;
                if (c == null)
                    continue;

                if (string.Equals(c.Value<string>("childName"), applicantName, compare))
                    return "child";

                if (
                    c.Value<string>("childIsAdult") == "no"
                    && string.Equals(
                        c.Value<string>("childGuardianName"),
                        applicantName,
                        compare
                    )
                )
                    return $"guardian of {c.Value<string>("childName")}, a minor child";

                if (
                    c.Value<string>("childIsCompetent") == "no"
                    && string.Equals(
                        c.Value<string>("childNomineeName"),
                        applicantName,
                        compare
                    )
                )
                    return $"nominee of {c.Value<string>("childName")}, a mentally incapable child";
            }
        }

        // Check creditorPersonData
        var creditorData = root.SelectToken("creditor.creditorPersonData") as JArray;
        if (creditorData != null)
        {
            for (int i = 1; i < creditorData.Count; i++)
            {
                var cr = creditorData[i] as JObject;
                if (cr == null)
                    continue;

                if (
                    string.Equals(
                        cr.Value<string>("creditorPersonName"),
                        applicantName,
                        compare
                    )
                )
                    return "creditor";

                if (
                    cr.Value<string>("creditorPersonIsAdult") == "no"
                    && string.Equals(
                        cr.Value<string>("creditorPersonGuardianName"),
                        applicantName,
                        compare
                    )
                )
                    return $"guardian of {cr.Value<string>("creditorPersonName")}, a minor creditor";

                if (
                    cr.Value<string>("creditorPersonIsCompetent") == "no"
                    && string.Equals(
                        cr.Value<string>("creditorPersonNomineeName"),
                        applicantName,
                        compare
                    )
                )
                    return $"nominee of {cr.Value<string>("creditorPersonName")}, a mentally incapable creditor";
            }
        }

        return "";
    }

    private static string FormatAddress(
        string? street,
        string? city,
        string? province,
        string? postal,
        string? country
    )
    {
        var parts = new[] { street, city, province, postal, country }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        return parts.Length > 0 ? string.Join(", ", parts) : "None";
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
}
