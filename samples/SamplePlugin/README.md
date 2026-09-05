# SamplePlugin

Vorlage für eigene CMISPilot-Plugins, siehe
[`Docs/Plan-Plugin-Schnittstelle.md`](../../Docs/Plan-Plugin-Schnittstelle.md).
Zeigt an einem minimalen, aber vollständigen Beispiel alle Beitragspunkte des
Vertrags `CMISPilot.Plugins.Abstractions`: eine Start-Schaltfläche, einen
kontextbezogenen Ribbon-Tab, einen Dokument-Tab mit Command-Bindung, sowie
Icons aus dem Host und aus dem Plugin selbst.

## Warum dieses Projekt in `CMISPilot.sln` steht, aber CMISPilot.Desktop es nicht referenziert

Ein Plugin ist zur Compile-Zeit unabhängig vom Host — genau das ist der Punkt
der Schnittstelle. Die Mitgliedschaft in der Solution ist trotzdem sinnvoll,
damit dieses Projekt beim regulären `dotnet build src/CMISPilot.sln` mitgebaut
wird und nicht unbemerkt verrottet. `CMISPilot.Desktop` hat bewusst **keine**
`ProjectReference` darauf.

## Bauen und ausprobieren

```powershell
dotnet build samples/SamplePlugin/SamplePlugin.csproj -c Debug

# In das Plugins/-Verzeichnis neben der gebauten Anwendung kopieren:
Copy-Item samples/SamplePlugin/bin/Debug/net10.0-windows/SamplePlugin.dll `
  src/CMISPilot.Desktop/bin/Debug/net10.0-windows/Plugins/ -Force
```

`Plugins/` existiert nicht standardmäßig — anlegen, falls es fehlt. CMISPilot
danach starten: Auf dem Start-Tab erscheint eine Schaltfläche „Beispiel" in der
Gruppe „Erweiterungen". Ein Klick öffnet den Dokument-Tab und schaltet den
kontextbezogenen Ribbon-Tab „Beispiel" frei.

Ohne dieses Verzeichnis (oder ganz ohne `Plugins/`) läuft CMISPilot unverändert
— das ist der Auslieferungsfall.

## Herkunft

Ursprünglich der Spike aus P0, der bewiesen hat, dass WPF ein erst zur Laufzeit
geladenes Plugin trägt (Details: [`Docs/status/P0-spike.md`](../../Docs/status/P0-spike.md)).
Seit P3 dauerhafte Vorlage statt Wegwerfcode.
