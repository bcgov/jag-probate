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
    private const string SpouseExistsField = "spouseExists";
    private const string SpouseDataField = "spouseData";
    private const string SpouseDataPath = "spouse.spouseData";
    private const string SpouseNameField = "spouseName";
    private const string SpouseIsAliveField = "spouseIsAlive";
    private const string ChildExistsField = "childExists";
    private const string ChildDataField = "childData";
    private const string ChildDataPath = "child.childData";
    private const string ChildNameField = "childName";
    private const string ChildIsAliveField = "childIsAlive";
    private const string ParentExistsField = "parentExists";
    private const string ParentDataField = "parentData";
    private const string ParentDataPath = "parent.parentData";
    private const string SiblingExistsField = "siblingExists";
    private const string SiblingDataField = "siblingData";
    private const string SiblingDataPath = "sibling.siblingData";

    /// <summary>
    /// Enriches P2 schedule data with display-ready spouse, child, and intestate-successor rows.
    /// Intestate successor classes are emitted in priority order based on living predecessors.
    /// </summary>
    public static object EnrichP2(object submissionData)
    {
        var root = submissionData is JObject jo
            ? jo
            : JObject.Parse(JsonSerializer.Serialize(submissionData));

        var spouseRows = BuildP2PeopleRows(
            root,
            SpouseExistsField,
            SpouseDataField,
            SpouseDataPath,
            SpouseNameField,
            SpouseIsAliveField
        );
        var childRows = BuildP2PeopleRows(
            root,
            ChildExistsField,
            ChildDataField,
            ChildDataPath,
            ChildNameField,
            ChildIsAliveField
        );
        var successorRows = BuildP2SuccessorRows(root);

        root["p2SpouseRows"] = new JArray(spouseRows);
        root["p2ChildRows"] = new JArray(childRows);
        root["p2SuccessorRows"] = new JArray(successorRows);
        root["p2ListedNames"] =
            "|"
            + string.Join(
                "|",
                spouseRows
                    .Concat(childRows)
                    .Concat(successorRows)
                    .Select(row => row.Value<string>("name"))
            )
            + "|";

        return root;
    }

    private static List<JObject> BuildP2PeopleRows(
        JObject root,
        string existsField,
        string flatArrayField,
        string nestedArrayPath,
        string nameField,
        string aliveField
    )
    {
        var rows = new List<JObject>();
        var listedNames = new HashSet<string>(StringComparer.Ordinal);
        if (P2Value(root, existsField) != "yes")
            return rows;

        foreach (var person in P2Array(root, flatArrayField, nestedArrayPath).OfType<JObject>())
        {
            var name = person.Value<string>(nameField)?.Trim();
            if (string.IsNullOrEmpty(name))
                continue;
            if (!listedNames.Add(name))
                continue;

            rows.Add(
                new JObject
                {
                    ["name"] = name,
                    ["isDeceased"] = person.Value<string>(aliveField) == "no",
                }
            );
        }

        return rows;
    }

    private static List<JObject> BuildP2SuccessorRows(JObject root)
    {
        var rows = new List<JObject>();
        var spouseData = P2People(root, SpouseExistsField, SpouseDataField, SpouseDataPath);
        var childData = P2People(root, ChildExistsField, ChildDataField, ChildDataPath);
        var parentData = P2People(root, ParentExistsField, ParentDataField, ParentDataPath);
        var siblingData = P2People(root, SiblingExistsField, SiblingDataField, SiblingDataPath);
        var listedNames = new HashSet<string>(StringComparer.Ordinal);

        AddP2Grandchildren(rows, listedNames, childData);

        var canListParents =
            !HasLivingPerson(spouseData, SpouseNameField, SpouseIsAliveField)
            && !HasLivingPerson(childData, ChildNameField, ChildIsAliveField)
            && !HasLivingGrandchild(childData);
        var hasLivingParent = HasLivingPerson(parentData, "parentName", "parentIsAlive");
        if (canListParents)
            AddP2Successors(rows, listedNames, parentData, "parent", "Deceased's parent");

        var canListSiblings = canListParents && !hasLivingParent;
        var hasLivingSibling = HasLivingPerson(siblingData, "siblingName", "siblingIsAlive");
        if (canListSiblings)
            AddP2Successors(rows, listedNames, siblingData, "sibling", "Deceased's sibling");

        if (canListSiblings && !hasLivingSibling)
            AddP2OtherSuccessors(root, rows, listedNames);
        return rows;
    }

    private static void AddP2Grandchildren(
        List<JObject> rows,
        HashSet<string> listedNames,
        IEnumerable<JObject> childData
    )
    {
        foreach (var child in childData)
        {
            if (
                child.Value<string>(ChildIsAliveField) != "no"
                || child.Value<string>("childGchildExists") == "no"
            )
                continue;

            foreach (var grandchild in P2Grandchildren(child))
            {
                AddP2Successor(
                    rows,
                    listedNames,
                    grandchild.Value<string>("grandchildName"),
                    "Deceased's grandchild"
                );
            }
        }
    }

    private static bool HasLivingGrandchild(IEnumerable<JObject> childData) =>
        childData
            .Where(child =>
                child.Value<string>(ChildIsAliveField) == "no"
                && child.Value<string>("childGchildExists") != "no"
            )
            .SelectMany(P2Grandchildren)
            .Any(grandchild =>
                !string.IsNullOrWhiteSpace(grandchild.Value<string>("grandchildName"))
                && grandchild.Value<string>("grandchildIsAlive") == "yes"
            );

    private static void AddP2Successors(
        List<JObject> rows,
        HashSet<string> listedNames,
        IEnumerable<JObject> data,
        string fieldPrefix,
        string relationship
    )
    {
        foreach (
            var person in data.Where(person =>
                person.Value<string>($"{fieldPrefix}IsAlive") == "yes"
            )
        )
        {
            AddP2Successor(
                rows,
                listedNames,
                person.Value<string>($"{fieldPrefix}Name"),
                relationship
            );
        }
    }

    private static void AddP2OtherSuccessors(
        JObject root,
        List<JObject> rows,
        HashSet<string> listedNames
    )
    {
        if (root["applicant"] is not JObject applicant)
            return;

        if (
            applicant.Value<string>("applicantNameSelection") == "__other__"
            && applicant.Value<string>("applicantHasBeenNominated") == "no"
            && applicant.Value<string>("applicantIsOtherSuccessor") == "yes"
        )
        {
            var relationship = applicant.Value<string>("applicantOtherRelationship")?.Trim();
            AddP2Successor(
                rows,
                listedNames,
                applicant.Value<string>("applicantName"),
                string.IsNullOrEmpty(relationship) ? "Applicant" : relationship
            );
        }

        foreach (
            var successor in (applicant["applicantSuccessorData"] as JArray)?.OfType<JObject>()
                ?? []
        )
        {
            var name = successor.Value<string>("successorName")?.Trim();
            var relationship = successor.Value<string>("successorRelationship")?.Trim();
            if (string.IsNullOrEmpty(relationship))
            {
                relationship = (applicant["applicantSamePrioritySuccessorNames"] as JArray)
                    ?.OfType<JObject>()
                    .FirstOrDefault(entry =>
                        string.Equals(
                            entry.Value<string>("applicantSuccessorEntry")?.Trim(),
                            name,
                            StringComparison.Ordinal
                        )
                    )
                    ?.Value<string>("relationship")
                    ?.Trim();
            }

            if (
                !string.IsNullOrEmpty(relationship)
                && !relationship.StartsWith("deceased's ", StringComparison.OrdinalIgnoreCase)
            )
                relationship = $"deceased's {relationship}";

            AddP2Successor(rows, listedNames, name, relationship);
        }
    }

    private static IEnumerable<JObject> P2Array(JObject root, string flatField, string nestedPath)
    {
        var flatData = root[flatField] as JArray;
        var nestedData = root.SelectToken(nestedPath) as JArray;

        if (flatData == null)
            return nestedData?.OfType<JObject>() ?? [];
        if (nestedData == null || ReferenceEquals(flatData, nestedData))
            return flatData.OfType<JObject>();

        return nestedData.OfType<JObject>().Concat(flatData.OfType<JObject>());
    }

    private static IEnumerable<JObject> P2Grandchildren(JObject child) =>
        child["gchildData"] is JArray grandchildren ? grandchildren.OfType<JObject>() : [];

    private static string? P2Value(JObject root, string field)
    {
        var nestedPath = field switch
        {
            SpouseExistsField => "spouse.spouseExists",
            ChildExistsField => "child.childExists",
            ParentExistsField => "parent.parentExists",
            SiblingExistsField => "sibling.siblingExists",
            _ => field,
        };
        var nestedValue = root.SelectToken(nestedPath)?.Value<string>();
        return nestedValue ?? root.Value<string>(field);
    }

    private static List<JObject> P2People(
        JObject root,
        string existsField,
        string flatArrayField,
        string nestedArrayPath
    ) =>
        P2Value(root, existsField) == "yes"
            ? P2Array(root, flatArrayField, nestedArrayPath).ToList()
            : [];

    private static bool HasLivingPerson(
        IEnumerable<JObject> data,
        string nameField,
        string aliveField
    ) =>
        data.Any(person =>
            !string.IsNullOrWhiteSpace(person.Value<string>(nameField))
            && person.Value<string>(aliveField) == "yes"
        );

    private static void AddP2Successor(
        List<JObject> rows,
        HashSet<string> listedNames,
        string? name,
        string? relationship
    )
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || !listedNames.Add(name))
            return;

        rows.Add(new JObject { ["name"] = name, ["relationship"] = relationship ?? "" });
    }

    /// <summary>
    /// Enriches PGT submission data with computed fields:
    /// - hasMinors / computedMinors: alive minors from spouseData + childData + siblingData + gchildData + creditorPersonData
    /// - hasIncapableAdults / computedIncapableAdults: alive incapable adults from
    ///   spouseData + childData + parentData + siblingData + gchildData + creditorPersonData
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
        CollectFromCreditorPersonData(root, deceasedName, minors, incapableAdults);

        root["hasMinors"] = minors.Count > 0;
        root["computedMinors"] = new JArray(minors);
        root["hasIncapableAdults"] = incapableAdults.Count > 0;
        root["computedIncapableAdults"] = new JArray(incapableAdults);
        root["applicantRelationship"] = DeduceApplicantRelationship(root);

        return root;
    }

    /// <summary>
    /// Enriches P5 submission data with the WESA s.130 paragraph for the applicant.
    /// The CHEFS-calculated applicant.applicantSection130Pathway only covers the
    /// direct child/grandchild/parent/sibling/other-successor paths (b, d, e, f), so
    /// spouse, nominated and court-appointed paths are derived here:
    /// - court appointed (not nominated) -> g
    /// - nominated by spouse -> a
    /// - nominated by child with consent -> c
    /// - nominated by another intestate successor with consent -> e.1
    /// - applicant is the spouse -> a
    /// Otherwise the CHEFS value is kept. applicant.applicantSection130Nominated is
    /// true when the paragraph is a nomination paragraph.
    /// </summary>
    public static object EnrichP5(object submissionData)
    {
        var root = submissionData is JObject jo
            ? jo
            : JObject.Parse(JsonSerializer.Serialize(submissionData));

        if (root["applicant"] is not JObject applicant)
            return root;

        var (pathway, nominated) = DeduceSection130Pathway(root, applicant);
        if (pathway != null)
            applicant["applicantSection130Pathway"] = pathway;
        applicant["applicantSection130Nominated"] = nominated;

        return root;
    }

    private static (string? Pathway, bool Nominated) DeduceSection130Pathway(
        JObject root,
        JObject applicant
    )
    {
        if (applicant.Value<string>("applicantHasBeenNominated") == "yes")
        {
            var nominator = applicant.Value<string>("applicantNominator") ?? "";
            var role = nominator.Contains("::", StringComparison.Ordinal)
                ? nominator[..nominator.IndexOf("::", StringComparison.Ordinal)]
                : "";

            return role switch
            {
                "spouse" => ("s130a", true),
                "child" when applicant.Value<string>("applicantChildConsent") == "yes" => (
                    "s130c",
                    true
                ),
                "grandchild"
                or "parent"
                or "sibling" when applicant.Value<string>("applicantIntestateConsent") == "yes" => (
                    "s130e1",
                    true
                ),
                _ => (null, false),
            };
        }

        if (applicant.Value<string>("applicantCourtAppointed") == "yes")
            return ("s130g", false);

        var applicantName = applicant.Value<string>("applicantName")?.Trim();
        if (
            !string.IsNullOrEmpty(applicantName)
            && NameExistsInArray(root, "spouse.spouseData", "spouseName", applicantName)
        )
            return ("s130a", false);

        return (null, false);
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

    private static void CollectFromCreditorPersonData(
        JObject root,
        string deceasedName,
        List<JObject> minors,
        List<JObject> incapableAdults
    )
    {
        var creditorPersonData = root.SelectToken("creditor.creditorPersonData") as JArray;
        if (creditorPersonData == null)
            return;

        for (int i = 1; i < creditorPersonData.Count; i++)
        {
            var creditor = creditorPersonData[i] as JObject;
            if (
                creditor == null
                || string.IsNullOrWhiteSpace(creditor.Value<string>("creditorPersonName"))
            )
                continue;
            if (creditor.Value<string>("creditorPersonIsAlive") != "yes")
                continue;

            if (creditor.Value<string>("creditorPersonIsAdult") == "no")
            {
                minors.Add(BuildMinor(creditor, "creditorPerson", deceasedName, "creditor"));
            }
            else if (
                creditor.Value<string>("creditorPersonIsAdult") == "yes"
                && creditor.Value<string>("creditorPersonIsCompetent") == "no"
            )
            {
                incapableAdults.Add(
                    BuildIncapableAdult(
                        creditor,
                        "creditorPerson",
                        deceasedName,
                        "creditor",
                        "creditorPersonIncompetent"
                    )
                );
            }
        }
    }

    private static JObject BuildMinor(JObject src, string prefix, string deceasedName, string role)
    {
        var name = src.Value<string>($"{prefix}Name") ?? "";
        var p = $"{prefix}Minor";
        var g = $"{prefix}Guardian";

        var resAddress = ExtractFormatAddress(src, p);

        var postalAddress = HasDifferentMail(src, p)
            ? ExtractFormatAddress(src, p, "Mail")
            : resAddress;

        var guardianResAddress = ExtractFormatAddress(src, g);

        var guardianPostalAddress = HasDifferentMail(src, g)
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
        string role,
        string? incapablePrefix = null
    )
    {
        var name = src.Value<string>($"{prefix}Name") ?? "";
        var p = incapablePrefix ?? $"{prefix}Incomp";
        var n = $"{prefix}Nominee";

        var resAddress = ExtractFormatAddress(src, p);

        var postalAddress = HasDifferentMail(src, p)
            ? ExtractFormatAddress(src, p, "Mail")
            : resAddress;

        var nomineeResAddress = ExtractFormatAddress(src, n);

        var nomineePostalAddress = HasDifferentMail(src, n)
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
        var addressPrefix = $"{dataNamePrefix}{fieldPrefix}";
        return FormatAddress(
            AddressValue(src, addressPrefix, "Street"),
            AddressValue(src, addressPrefix, "City"),
            AddressValue(src, addressPrefix, "Province"),
            src.Value<string>($"{addressPrefix}State"),
            src.Value<string>($"{addressPrefix}ProvinceText"),
            AddressValue(src, addressPrefix, "Postal"),
            AddressValue(src, addressPrefix, "Country")
        );
    }

    private static string? AddressValue(JObject src, string prefix, string field)
    {
        return FirstNonEmpty(
            src.Value<string>($"{prefix}Res{field}"),
            src.Value<string>($"{prefix}{field}")
        );
    }

    private static bool HasDifferentMail(JObject src, string prefix)
    {
        return src.Value<string>($"{prefix}HasDiffMail") == "yes"
            || src.Value<string>($"{prefix}HasMailing") == "yes";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
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
