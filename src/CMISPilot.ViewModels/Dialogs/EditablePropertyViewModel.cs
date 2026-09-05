using CMISPilot.Cmis.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CMISPilot.ViewModels.Dialogs;

/// <summary>
/// Ein einzelnes editierbares Feld im Bearbeiten-/Anlegen-Dialog (M7, T7.3).
/// Bewusst WPF-frei (NFA-03): reine <see cref="ObservableObject"/>-Bindungsquelle.
/// </summary>
public sealed partial class EditablePropertyViewModel : ObservableObject
{
    public EditablePropertyViewModel(
        string id, string displayName, CmisPropertyType propertyType, bool isRequired, string value)
    {
        Id = id;
        DisplayName = displayName;
        PropertyType = propertyType;
        IsRequired = isRequired;
        _value = value;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public CmisPropertyType PropertyType { get; }
    public bool IsRequired { get; }

    [ObservableProperty]
    private string _value;

    /// <summary>Validierungsfehler des Felds (null, wenn gültig). Von <see cref="EditPropertiesViewModel.Validate"/> gesetzt.</summary>
    [ObservableProperty]
    private string? _errorMessage;
}
