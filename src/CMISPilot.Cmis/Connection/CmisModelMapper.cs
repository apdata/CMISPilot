using System;
using System.Collections.Generic;
using System.Linq;
using CMISPilot.Cmis.Models;
using PortCMIS;
using PortCMIS.Client;
using PortCMIS.Data;
using PortCMIS.Data.Extensions;
using PortCMIS.Enums;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Bildet PortCMIS-Typen auf die UI-freundlichen DTOs ab (T1.2). Zentral, damit
/// die Mapping-Logik nicht über die Dienste verstreut ist und PortCMIS-Typen die
/// Library nicht verlassen (NFA-03a).
/// </summary>
internal static class CmisModelMapper
{
    /// <param name="info">Die von PortCMIS gelieferte Repository-Information.</param>
    /// <param name="fallbackName">
    /// Anzeigename, der einspringt, wenn der Server keinen liefert. Manche Server geben
    /// bei <c>getRepositoryInfo</c> keinen <c>repositoryName</c> zurueck, obwohl
    /// <c>getRepositories</c> einen hat; ohne Ruckfall bliebe die Beschriftung im Baum
    /// und in der Statusleiste leer.
    /// </param>
    public static RepositoryInfoDto ToDto(IRepositoryInfo info, string? fallbackName = null) => new()
    {
        Id = info.Id,
        Name = string.IsNullOrWhiteSpace(info.Name) ? fallbackName : info.Name,
        Description = info.Description,
        VendorName = info.VendorName,
        ProductName = info.ProductName,
        ProductVersion = info.ProductVersion,
        CmisVersion = info.CmisVersionSupported,
        RootFolderId = info.RootFolderId,
        ThinClientUri = info.ThinClientUri,
        LatestChangeLogToken = info.LatestChangeLogToken,
        Capabilities = info.Capabilities is null ? null : ToDto(info.Capabilities),
        AclCapabilities = info.AclCapabilities is null ? null : ToDto(info.AclCapabilities),
        ChangesIncomplete = info.ChangesIncomplete,
        ChangesOnType = info.ChangesOnType?
            .Where(t => t.HasValue)
            .Select(t => ToBaseType(t!.Value).ToString())
            .ToList() ?? [],
        PrincipalIdAnonymous = info.PrincipalIdAnonymous,
        PrincipalIdAnyone = info.PrincipalIdAnyone,
        ExtensionFeatures = info.ExtensionFeatures?
            .Select(f => new ExtensionFeatureDto(
                f.Id, f.CommonName, f.VersionLabel, f.Url, f.Description, Pairs(f.FeatureData)))
            .ToList() ?? [],
        Extensions = ToDto(info.Extensions)
    };

    /// <summary>
    /// Bildet die herstellerspezifischen Erweiterungsdaten rekursiv ab. CMIS gibt fuer
    /// diesen Bereich keine Struktur vor, deshalb bleibt der Baum erhalten statt
    /// vorschnell geglaettet zu werden - was davon nuetzlich ist, entscheidet die Anzeige.
    /// </summary>
    private static IReadOnlyList<CmisExtensionElementDto> ToDto(IList<ICmisExtensionElement>? elements) =>
        elements?
            .Select(e => new CmisExtensionElementDto(
                e.Name, e.Namespace, e.Value, Pairs(e.Attributes), ToDto(e.Children)))
            .ToList() ?? [];

    /// <summary>Kopiert ein Dictionary in eine stabil sortierte Paarliste (fuer die Anzeige).</summary>
    private static IReadOnlyList<KeyValuePair<string, string>> Pairs(IDictionary<string, string>? source) =>
        source?.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList() ?? [];

    /// <summary>
    /// Bildet die ACL-Capabilities ab. Die Berechtigungszuordnung kommt bei PortCMIS als
    /// Dictionary und wird hier zu einer Liste geglaettet - die Ansicht zeigt sie als
    /// Tabelle, und eine Reihenfolge ist dafuer nuetzlicher als ein Schluesselzugriff.
    /// </summary>
    public static AclCapabilitiesDto ToDto(IAclCapabilities acl) => new()
    {
        SupportedPermissions = acl.SupportedPermissions?.ToString(),
        AclPropagation = acl.AclPropagation?.ToString(),
        Permissions = acl.Permissions?
            .Select(p => new PermissionDefinitionDto(p.Id, p.Description))
            .ToList() ?? [],
        PermissionMapping = acl.PermissionMapping?
            .Select(kv => new PermissionMappingDto(
                kv.Key,
                (IReadOnlyList<string>)(kv.Value?.Permissions?.ToList() ?? [])))
            .OrderBy(m => m.Key, StringComparer.Ordinal)
            .ToList() ?? []
    };

    public static RepositoryCapabilitiesDto ToDto(IRepositoryCapabilities cap) => new()
    {
        ContentStreamUpdates = cap.ContentStreamUpdatesCapability?.ToString(),
        Changes = cap.ChangesCapability?.ToString(),
        Renditions = cap.RenditionsCapability?.ToString(),
        OrderBy = cap.OrderByCapability?.ToString(),
        Query = cap.QueryCapability?.ToString(),
        Join = cap.JoinCapability?.ToString(),
        Acl = cap.AclCapability?.ToString(),
        GetDescendantsSupported = cap.IsGetDescendantsSupported,
        GetFolderTreeSupported = cap.IsGetFolderTreeSupported,
        MultifilingSupported = cap.IsMultifilingSupported,
        UnfilingSupported = cap.IsUnfilingSupported,
        VersionSpecificFilingSupported = cap.IsVersionSpecificFilingSupported,
        PwcSearchableSupported = cap.IsPwcSearchableSupported,
        PwcUpdatableSupported = cap.IsPwcUpdatableSupported,
        AllVersionsSearchableSupported = cap.IsAllVersionsSearchableSupported,
        CreatablePropertyTypes = cap.CreatablePropertyTypes?.CanCreate?
            .Select(t => t.ToString())
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList() ?? [],
        NewTypeSettableAttributes = cap.NewTypeSettableAttributes is null
            ? null
            : ToDto(cap.NewTypeSettableAttributes)
    };

    /// <summary>Bildet die beim Anlegen neuer Typen setzbaren Attribute ab (CMIS 1.1).</summary>
    public static NewTypeSettableAttributesDto ToDto(INewTypeSettableAttributes attributes) => new()
    {
        Id = attributes.CanSetId,
        LocalName = attributes.CanSetLocalName,
        LocalNamespace = attributes.CanSetLocalNamespace,
        DisplayName = attributes.CanSetDisplayName,
        QueryName = attributes.CanSetQueryName,
        Description = attributes.CanSetDescription,
        Creatable = attributes.CanSetCreatable,
        Fileable = attributes.CanSetFileable,
        Queryable = attributes.CanSetQueryable,
        FulltextIndexed = attributes.CanSetFulltextIndexed,
        IncludedInSupertypeQuery = attributes.CanSetIncludedInSupertypeQuery,
        ControllablePolicy = attributes.CanSetControllablePolicy,
        ControllableAcl = attributes.CanSetControllableAcl
    };

    public static CmisObjectDto ToDto(ICmisObject obj)
    {
        var doc = obj as IDocument;
        return new CmisObjectDto
        {
            Id = obj.Id,
            Name = obj.Name,
            BaseType = ToBaseType(obj.BaseTypeId),
            TypeId = obj.ObjectType?.Id,
            CreatedBy = obj.CreatedBy,
            CreationDate = ToOffset(obj.CreationDate),
            LastModifiedBy = obj.LastModifiedBy,
            LastModificationDate = ToOffset(obj.LastModificationDate),
            ContentStreamLength = doc?.ContentStreamLength,
            ContentStreamMimeType = doc?.ContentStreamMimeType,
            ContentStreamFileName = doc?.ContentStreamFileName,
            Properties = obj.Properties?.Select(ToDto).ToList() ?? new List<PropertyDto>(),
            SecondaryTypeIds = obj.SecondaryTypes?.Select(t => t.Id).ToList() ?? new List<string>(),
            AllowableActions = obj.AllowableActions?.Actions?
                .Select(a => a.ToString()!).ToList()
        };
    }

    public static PropertyDto ToDto(IProperty p) => new()
    {
        Id = p.Id,
        DisplayName = p.DisplayName,
        QueryName = p.QueryName,
        PropertyType = ToPropertyType(p.PropertyType),
        IsMultiValued = p.IsMultiValued,
        Value = p.Value,
        Values = p.Values?.ToList() ?? new List<object?>(),
        ValueAsString = p.IsMultiValued ? p.ValuesAsString : p.ValueAsString
    };

    public static PropertyDto ToDto(IPropertyData p) => new()
    {
        Id = p.Id,
        DisplayName = p.DisplayName,
        QueryName = p.QueryName,
        IsMultiValued = p.Values is { Count: > 1 },
        Value = p.FirstValue,
        Values = p.Values?.ToList() ?? new List<object?>(),
        ValueAsString = p.FirstValue?.ToString()
    };

    public static TypeDefinitionDto ToDto(
        IObjectType type, IReadOnlyList<TypeDefinitionDto>? children = null) => new()
    {
        Id = type.Id,
        LocalName = type.LocalName,
        LocalNamespace = type.LocalNamespace,
        DisplayName = type.DisplayName,
        QueryName = type.QueryName,
        Description = type.Description,
        BaseType = ToBaseType(type.BaseTypeId),
        ParentTypeId = type.ParentTypeId,
        IsCreatable = type.IsCreatable,
        IsFileable = type.IsFileable,
        IsQueryable = type.IsQueryable,
        IsFulltextIndexed = type.IsFulltextIndexed,
        IsIncludedInSupertypeQuery = type.IsIncludedInSupertypeQuery,
        IsControllablePolicy = type.IsControllablePolicy,
        IsControllableAcl = type.IsControllableAcl,
        PropertyDefinitions = type.PropertyDefinitions?
            .Select(ToDto).ToList() ?? new List<PropertyDefinitionDto>(),
        Children = children ?? Array.Empty<TypeDefinitionDto>()
    };

    /// <summary>
    /// Bildet einen PortCMIS-Typbaum-Knoten (<c>ITree&lt;IObjectType&gt;</c>) rekursiv
    /// auf einen verschachtelten <see cref="TypeDefinitionDto"/> ab (FA-60). Genutzt
    /// vom <c>TypeService</c> für <c>GetTypeDescendants</c>.
    /// </summary>
    public static TypeDefinitionDto ToDtoTree(ITree<IObjectType> node) =>
        ToDto(node.Item, node.Children?.Select(ToDtoTree).ToList()
            ?? (IReadOnlyList<TypeDefinitionDto>)Array.Empty<TypeDefinitionDto>());

    public static PropertyDefinitionDto ToDto(IPropertyDefinition d)
    {
        // Laenge/Wertebereich stecken in den konkreten Subtypen (String/Integer/Decimal).
        long? maxLength = null;
        string? minValue = null;
        string? maxValue = null;
        string? precision = null;

        switch (d)
        {
            case IPropertyStringDefinition s:
                maxLength = ToInt64OrNull(s.MaxLength);
                break;
            case IPropertyIntegerDefinition i:
                minValue = i.MinValue?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                maxValue = i.MaxValue?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                break;
            case IPropertyDecimalDefinition dec:
                minValue = dec.MinValue?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                maxValue = dec.MaxValue?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                precision = dec.Precision switch
                {
                    DecimalPrecision.Bits32 => "32",
                    DecimalPrecision.Bits64 => "64",
                    _ => null
                };
                break;
        }

        return new PropertyDefinitionDto
        {
            Id = d.Id,
            LocalName = d.LocalName,
            DisplayName = d.DisplayName,
            QueryName = d.QueryName,
            Description = d.Description,
            PropertyType = ToPropertyType(d.PropertyType) ?? CmisPropertyType.String,
            Cardinality = ToCardinality(d.Cardinality),
            Updatability = ToUpdatability(d.Updatability),
            IsInherited = d.IsInherited,
            IsRequired = d.IsRequired,
            IsQueryable = d.IsQueryable,
            IsOrderable = d.IsOrderable,
            IsOpenChoice = d.IsOpenChoice,
            MaxLength = maxLength,
            MinValue = minValue,
            MaxValue = maxValue,
            Precision = precision
        };
    }

    /// <summary>Wandelt eine (moeglicherweise sehr grosse) BigInteger-Laenge sicher in long?; bei Ueberlauf null.</summary>
    private static long? ToInt64OrNull(System.Numerics.BigInteger? value) =>
        value is { } v && v >= long.MinValue && v <= long.MaxValue ? (long)v : null;

    public static QueryResultDto ToQueryResult(IEnumerable<IQueryResult> results)
    {
        var rows = new List<QueryRowDto>();
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in results)
        {
            var props = new List<PropertyDto>();
            var byColumn = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pd in r.Properties)
            {
                var dto = ToDto(pd);
                props.Add(dto);
                var col = pd.QueryName ?? pd.Id;
                if (col is not null)
                {
                    byColumn[col] = pd.FirstValue;
                    if (seen.Add(col)) columns.Add(col);
                }
            }

            rows.Add(new QueryRowDto
            {
                ObjectId = r.GetPropertyValueById(PropertyIds.ObjectId) as string,
                Properties = props,
                ValuesByColumn = byColumn
            });
        }

        return new QueryResultDto { ColumnNames = columns, Rows = rows };
    }

    /// <summary>Bildet einen PortCMIS-ACE (R6.1) auf <see cref="AclEntryDto"/> ab.</summary>
    public static AclEntryDto ToDto(IAce ace) => new()
    {
        PrincipalId = ace.PrincipalId,
        Permissions = ace.Permissions?.ToList() ?? new List<string>(),
        IsDirect = ace.IsDirect
    };

    /// <summary>
    /// Bildet eine einzelne Version eines Dokuments (aus <c>IDocument.GetAllVersions</c>,
    /// R6.1) auf <see cref="ObjectVersionDto"/> ab.
    /// </summary>
    public static ObjectVersionDto ToVersionDto(IDocument version) => new()
    {
        Id = version.Id,
        VersionLabel = version.VersionLabel,
        IsLatestVersion = version.IsLatestVersion,
        IsMajorVersion = version.IsMajorVersion,
        IsLatestMajorVersion = version.IsLatestMajorVersion,
        CreatedBy = version.CreatedBy,
        CreationDate = ToOffset(version.CreationDate),
        LastModifiedBy = version.LastModifiedBy,
        LastModificationDate = ToOffset(version.LastModificationDate),
        CheckinComment = version.CheckinComment,
        ContentStreamLength = version.ContentStreamLength,
        ContentStreamFileName = version.ContentStreamFileName
    };

    private static DateTimeOffset? ToOffset(DateTime? dt)
        => dt.HasValue ? new DateTimeOffset(dt.Value.ToUniversalTime(), TimeSpan.Zero) : null;

    public static CmisBaseType ToBaseType(BaseTypeId id) => id switch
    {
        BaseTypeId.CmisDocument => CmisBaseType.Document,
        BaseTypeId.CmisFolder => CmisBaseType.Folder,
        BaseTypeId.CmisRelationship => CmisBaseType.Relationship,
        BaseTypeId.CmisPolicy => CmisBaseType.Policy,
        BaseTypeId.CmisItem => CmisBaseType.Item,
        BaseTypeId.CmisSecondary => CmisBaseType.Secondary,
        _ => CmisBaseType.Unknown
    };

    private static CmisPropertyType? ToPropertyType(PropertyType? t) => t switch
    {
        PropertyType.Boolean => CmisPropertyType.Boolean,
        PropertyType.Id => CmisPropertyType.Id,
        PropertyType.Integer => CmisPropertyType.Integer,
        PropertyType.DateTime => CmisPropertyType.DateTime,
        PropertyType.Decimal => CmisPropertyType.Decimal,
        PropertyType.Html => CmisPropertyType.Html,
        PropertyType.String => CmisPropertyType.String,
        PropertyType.Uri => CmisPropertyType.Uri,
        _ => null
    };

    private static CmisCardinality? ToCardinality(Cardinality? c) => c switch
    {
        Cardinality.Single => CmisCardinality.Single,
        Cardinality.Multi => CmisCardinality.Multi,
        _ => null
    };

    private static CmisUpdatability? ToUpdatability(Updatability? u) => u switch
    {
        Updatability.ReadOnly => CmisUpdatability.ReadOnly,
        Updatability.ReadWrite => CmisUpdatability.ReadWrite,
        Updatability.WhenCheckedOut => CmisUpdatability.WhenCheckedOut,
        Updatability.OnCreate => CmisUpdatability.OnCreate,
        _ => null
    };
}
