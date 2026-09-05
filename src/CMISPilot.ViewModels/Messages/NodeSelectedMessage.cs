using CMISPilot.Cmis.Models;

namespace CMISPilot.ViewModels.Messages;

/// <summary>
/// Broadcast-Nachricht (CommunityToolkit-Messaging): im Server-Baum wurde ein
/// Knoten ausgewaehlt. Traegt das zugehoerige CMIS-Objekt, sofern der Knoten eines
/// repraesentiert (bei Server-/Repository-Knoten <c>null</c>). Empfaenger ist ab
/// R4 Etappe 3 das Eigenschaften-Werkzeugfenster; die Kopplung laeuft entkoppelt
/// ueber den <see cref="CommunityToolkit.Mvvm.Messaging.IMessenger"/> statt ueber
/// eine direkte Referenz.
/// </summary>
public sealed record NodeSelectedMessage(CmisObjectDto? CmisObject);
