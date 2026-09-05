using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using PortCMIS.Data;
using PortCMIS.Data.Extensions;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Schreibt eine PortCMIS-<see cref="IRepositoryInfo"/> in der Darstellung des
/// CMIS Browser Binding.
///
/// <para>Warum eigener Code statt des PortCMIS-Konverters: dessen
/// <c>JsonConverter</c>, der Typ <c>JsonObject</c> und die Enum-Hilfsklasse
/// <c>CmisValue</c> sind allesamt <c>internal</c> und von hier aus nicht aufrufbar.
/// Die Feldnamen unten sind daher wörtlich aus PortCMIS' <c>BrowserConstants</c>
/// übernommen und entsprechen der CMIS-1.1-Spezifikation.</para>
///
/// <para>Der Aufbau folgt der Reihenfolge des PortCMIS-Konverters. <c>repositoryUrl</c>
/// und <c>rootFolderUrl</c> fehlen bewusst: die kennt nur die Bindungsschicht des
/// Servers, sie stehen in der eingelesenen Repository-Information nicht zur Verfügung
/// und wären hier nur geraten.</para>
/// </summary>
internal static class RepositoryInfoJsonWriter
{
    public static string Write(IRepositoryInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var options = new JsonWriterOptions { Indented = true };
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();

            writer.WriteString("repositoryId", info.Id);
            writer.WriteString("repositoryName", info.Name);
            writer.WriteString("repositoryDescription", info.Description);
            writer.WriteString("vendorName", info.VendorName);
            writer.WriteString("productName", info.ProductName);
            writer.WriteString("productVersion", info.ProductVersion);
            writer.WriteString("rootFolderId", info.RootFolderId);

            WriteCapabilities(writer, info.Capabilities);
            WriteAclCapabilities(writer, info.AclCapabilities);

            writer.WriteString("latestChangeLogToken", info.LatestChangeLogToken);
            writer.WriteString("cmisVersionSupported", info.CmisVersionSupported);
            WriteIfNotNull(writer, "thinClientURI", info.ThinClientUri);

            if (info.ChangesIncomplete is bool changesIncomplete)
            {
                writer.WriteBoolean("changesIncomplete", changesIncomplete);
            }

            writer.WriteStartArray("changesOnType");
            foreach (var type in info.ChangesOnType ?? [])
            {
                if (type.HasValue)
                {
                    writer.WriteStringValue(CmisValueOf(type.Value));
                }
            }

            writer.WriteEndArray();

            WriteIfNotNull(writer, "principalIdAnonymous", info.PrincipalIdAnonymous);
            WriteIfNotNull(writer, "principalIdAnyone", info.PrincipalIdAnyone);
            WriteExtensionFeatures(writer, info.ExtensionFeatures);
            WriteExtensions(writer, info.Extensions);

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCapabilities(Utf8JsonWriter writer, IRepositoryCapabilities? cap)
    {
        if (cap is null)
        {
            writer.WriteNull("capabilities");
            return;
        }

        writer.WriteStartObject("capabilities");
        WriteEnum(writer, "capabilityContentStreamUpdatability", cap.ContentStreamUpdatesCapability);
        WriteEnum(writer, "capabilityChanges", cap.ChangesCapability);
        WriteEnum(writer, "capabilityRenditions", cap.RenditionsCapability);
        WriteBool(writer, "capabilityGetDescendants", cap.IsGetDescendantsSupported);
        WriteBool(writer, "capabilityGetFolderTree", cap.IsGetFolderTreeSupported);
        WriteBool(writer, "capabilityMultifiling", cap.IsMultifilingSupported);
        WriteBool(writer, "capabilityUnfiling", cap.IsUnfilingSupported);
        WriteBool(writer, "capabilityVersionSpecificFiling", cap.IsVersionSpecificFilingSupported);
        WriteBool(writer, "capabilityPWCSearchable", cap.IsPwcSearchableSupported);
        WriteBool(writer, "capabilityPWCUpdatable", cap.IsPwcUpdatableSupported);
        WriteBool(writer, "capabilityAllVersionsSearchable", cap.IsAllVersionsSearchableSupported);
        WriteEnum(writer, "capabilityOrderBy", cap.OrderByCapability);
        WriteEnum(writer, "capabilityQuery", cap.QueryCapability);
        WriteEnum(writer, "capabilityJoin", cap.JoinCapability);
        WriteEnum(writer, "capabilityACL", cap.AclCapability);

        if (cap.CreatablePropertyTypes is { } creatable)
        {
            writer.WriteStartObject("capabilityCreatablePropertyTypes");
            writer.WriteStartArray("canCreate");
            foreach (var type in creatable.CanCreate ?? new HashSet<PortCMIS.Enums.PropertyType>())
            {
                writer.WriteStringValue(CmisValueOf(type));
            }

            writer.WriteEndArray();
            WriteExtensions(writer, creatable.Extensions);
            writer.WriteEndObject();
        }

        if (cap.NewTypeSettableAttributes is { } settable)
        {
            writer.WriteStartObject("capabilityNewTypeSettableAttributes");
            WriteBool(writer, "id", settable.CanSetId);
            WriteBool(writer, "localName", settable.CanSetLocalName);
            WriteBool(writer, "localNamespace", settable.CanSetLocalNamespace);
            WriteBool(writer, "displayName", settable.CanSetDisplayName);
            WriteBool(writer, "queryName", settable.CanSetQueryName);
            WriteBool(writer, "description", settable.CanSetDescription);
            WriteBool(writer, "creatable", settable.CanSetCreatable);
            WriteBool(writer, "fileable", settable.CanSetFileable);
            WriteBool(writer, "queryable", settable.CanSetQueryable);
            WriteBool(writer, "fulltextIndexed", settable.CanSetFulltextIndexed);
            WriteBool(writer, "includedInSupertypeQuery", settable.CanSetIncludedInSupertypeQuery);
            WriteBool(writer, "controllablePolicy", settable.CanSetControllablePolicy);
            WriteBool(writer, "controllableACL", settable.CanSetControllableAcl);
            WriteExtensions(writer, settable.Extensions);
            writer.WriteEndObject();
        }

        WriteExtensions(writer, cap.Extensions);
        writer.WriteEndObject();
    }

    private static void WriteAclCapabilities(Utf8JsonWriter writer, IAclCapabilities? acl)
    {
        if (acl is null)
        {
            return;
        }

        writer.WriteStartObject("aclCapabilities");
        WriteEnum(writer, "supportedPermissions", acl.SupportedPermissions);
        WriteEnum(writer, "propagation", acl.AclPropagation);

        writer.WriteStartArray("permissions");
        foreach (var permission in acl.Permissions ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("permission", permission.Id);
            writer.WriteString("description", permission.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("permissionMapping");
        foreach (var mapping in (acl.PermissionMapping ?? new Dictionary<string, IPermissionMapping>())
                     .OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("key", mapping.Key);
            writer.WriteStartArray("permission");
            foreach (var permission in mapping.Value?.Permissions ?? [])
            {
                writer.WriteStringValue(permission);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteExtensions(writer, acl.Extensions);
        writer.WriteEndObject();
    }

    private static void WriteExtensionFeatures(Utf8JsonWriter writer, IList<IExtensionFeature>? features)
    {
        if (features is null || features.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("extendedFeatures");
        foreach (var feature in features)
        {
            writer.WriteStartObject();
            WriteIfNotNull(writer, "id", feature.Id);
            WriteIfNotNull(writer, "url", feature.Url);
            WriteIfNotNull(writer, "commonName", feature.CommonName);
            WriteIfNotNull(writer, "versionLabel", feature.VersionLabel);
            WriteIfNotNull(writer, "description", feature.Description);

            if (feature.FeatureData is { Count: > 0 } data)
            {
                writer.WriteStartObject("featureData");
                foreach (var entry in data)
                {
                    writer.WriteString(entry.Key, entry.Value);
                }

                writer.WriteEndObject();
            }

            WriteExtensions(writer, feature.Extensions);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Schreibt die herstellerspezifischen Erweiterungsdaten als zusaetzliche
    /// Eigenschaften des umgebenden Objekts - genau so macht es auch der
    /// PortCMIS-Konverter. Ein Element mit Kindern wird zum Unterobjekt, eines ohne
    /// zum einfachen Wert.
    /// </summary>
    private static void WriteExtensions(Utf8JsonWriter writer, IList<ICmisExtensionElement>? extensions)
    {
        foreach (var extension in extensions ?? [])
        {
            if (extension.Children is { Count: > 0 })
            {
                writer.WriteStartObject(extension.Name);
                WriteExtensions(writer, extension.Children);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteString(extension.Name, extension.Value);
            }
        }
    }

    private static void WriteIfNotNull(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteBool(Utf8JsonWriter writer, string name, bool? value)
    {
        if (value is bool b)
        {
            writer.WriteBoolean(name, b);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static void WriteEnum(Utf8JsonWriter writer, string name, Enum? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, CmisValueOf(value));
        }
    }

    /// <summary>
    /// Liefert den CMIS-Wert eines PortCMIS-Enums (z. B. <c>anytime</c> statt
    /// <c>Anytime</c>).
    ///
    /// <para>PortCMIS hinterlegt ihn als <c>CmisValueAttribute</c> am Enum-Feld. Sowohl
    /// das Attribut als auch die zugehörige Hilfsklasse sind <c>internal</c>, der Wert
    /// wird deshalb über die Attributdaten gelesen — die sind unabhängig von der
    /// Sichtbarkeit des Attributtyps zugänglich. Findet sich kein Attribut, bleibt der
    /// .NET-Name als Rückfall; das ist ungenau, aber besser als eine Lücke im JSON.</para>
    /// </summary>
    private static string CmisValueOf(Enum value)
    {
        var field = value.GetType().GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static);
        var attribute = field?
            .GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == "CmisValueAttribute");

        return attribute?.ConstructorArguments.Count > 0
            && attribute.ConstructorArguments[0].Value is string cmisValue
                ? cmisValue
                : value.ToString();
    }
}
