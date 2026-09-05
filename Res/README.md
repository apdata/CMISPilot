# Res – Original-Grafiken der verwendeten Icons

Dieser Ordner ist ein **Herkunfts-Archiv**: Er enthält jede im Programm verwendete
Grafik in ihrem Originalformat, so wie sie bezogen wurde, plus diese
Dokumentation, woher sie stammt und wie sie angepasst wurde.

## Wie die Icons zur Laufzeit eingebunden sind

- **Vektor-Icons** (Großteil): Der Pfadinhalt (`GeometryDrawing`) der jeweiligen
  Quell-`.xaml` ist fest als `DrawingImage` nach
  `src/CMISPilot.Desktop/Resources/Icons/Icons.xaml` übernommen. Dabei wurde der
  transparente Canvas-Hintergrund entfernt und die `DynamicResource`-Farbschlüssel
  der Quelle zu festen `SolidColorBrush` aufgelöst – die Icons tragen ihre eigene,
  feste Farbgebung, unabhängig vom App-Theme. Zur Laufzeit wird **nie** auf einen
  Pfad außerhalb des Repositories verwiesen.
- **Raster-Icons**: Als Datei unter `src/CMISPilot.Desktop/Resources/Icons/`
  (Build-Aktion `Resource`), per `BitmapImage`/`UriSource` referenziert. Betrifft
  aktuell nur „Abfrage laden" (`FolderOpen16.png` / `FolderOpen32.png`).
- **Anwendungssymbol**: `src/CMISPilot.Desktop/Resources/CMISPilot.ico`, aus
  `Compass.xaml` erzeugt und auf den App-Akzent `#0F6CBD` umgefärbt.

Die Dateien in diesem Ordner werden **nicht** kompiliert oder eingebunden. Sie
dienen nur der Nachvollziehbarkeit und als Ausgangspunkt für spätere Anpassungen.

## Herkunft

Sofern nicht anders vermerkt: **Microsoft Visual Studio Image Library**, lokal
unter `E:\src\_Visual Studio Image Library\<Version>\images\`. Nutzungsbedingungen
je Version in der jeweiligen `Visual Studio <Version> Image Library EULA.rtf` im
Wurzelverzeichnis der Bibliothek.

| Icon-Schlüssel (`Icons.xaml`) | Verwendung | Originaldatei in diesem Ordner | Quelle | Anpassung |
|---|---|---|---|---|
| `Icon.Server` | Server-Knoten im Baum | `Server.xaml` (+`.png`) | VS Image Library 2022 | Vektor → DrawingImage, Farben fixiert |
| `Icon.Database` | Repository-Knoten im Baum | `Database.xaml` | VS 2022 | wie oben |
| `Icon.FolderClosed` | Ordner-Knoten (zu) | `FolderClosed.xaml` | VS 2022 | wie oben |
| `Icon.Document` | Dokument-Knoten | `Document.xaml` | VS 2022 | wie oben |
| `Icon.NewFolder` | Ribbon „Neuer Ordner" | `NewFolder.xaml` | VS 2022 | wie oben |
| `Icon.NewDocument` | Ribbon „Neues Dokument" | `NewDocument.xaml` | VS 2022 | wie oben |
| `Icon.Edit` | Ribbon / Kontextmenü „Bearbeiten" | `pencil_32.png` | VS Image Library 2012, Ordner `Objects VS2012 / png_format / Office and VS` | unverändert übernommen als `Resources/Icons/Edit32.png` (nur 32×32, für 16 px skaliert WPF herunter) |
| `Icon.Delete` | Ribbon „Löschen" | `Delete.xaml` | VS 2022 | wie oben |
| `Icon.Download` | Ribbon „Herunterladen" | `Download.xaml` | VS 2022 | wie oben |
| `Icon.Open` | Ribbon „Öffnen" | `Open.xaml` | VS 2022 | wie oben |
| `Icon.Upload` | Ribbon „Inhalt setzen" | `Upload.xaml` | VS 2022 | wie oben |
| `Icon.Execute` | Ribbon „Ausführen" (Abfrage) | `Execute.xaml` | VS 2022 | wie oben |
| `Icon.QueryView` | Start-Ribbon „Abfrage" | `QueryView.xaml` | VS 2022 | wie oben |
| `Icon.Refresh` | „Aktualisieren" / „Zurücksetzen" | `Refresh.xaml` | VS 2022 | wie oben |
| `Icon.Tag` | „Typen" / Typ-Knoten | `Tag.xaml` | VS 2022 | wie oben |
| `Icon.Log` | Diagnose-Werkzeugfenster | `Log.xaml` | VS 2022 | wie oben |
| `Icon.ClearLog` | „Protokoll leeren" | `ClearWindowContent.xaml` | VS 2022 | wie oben |
| `Icon.PropertiesDetail` | „Erweiterte Eigenschaften" | `ShowDetailsPane.xaml` | VS Image Library 2026 | Vektor → DrawingImage, feste Fill-Farbe der Quelle |
| `Icon.About` | Backstage „Über CMISPilot" | `AboutBox.xaml` | VS 2026 | wie oben |
| `Icon.Profile` | Backstage „Profile" | `User.xaml` | VS 2026 | wie oben |
| `Icon.Connect` | Ribbon „Verbinden" | `Connect.xaml` | VS 2022 | wie oben |
| `Icon.Disconnect` | Ribbon „Trennen" | `Disconnect.xaml` | VS 2022 | wie oben |
| `Icon.TreeView` | Explorer-Werkzeugfenster | `TreeView.xaml` | VS 2022 | wie oben |
| `Icon.Property` | Eigenschaften-Werkzeugfenster | `Property.xaml` | VS 2022 | wie oben |
| `Icon.Output` | Ausgabe-Werkzeugfenster / „Alle einblenden" | `Output.xaml` | VS 2022 | wie oben |
| `Icon.DarkTheme` | (aktuell ungenutzt) | `DarkTheme.xaml` | VS 2022 | wie oben |
| `Icon.Exit` | Backstage „Beenden" | `Exit.xaml` | VS 2022 | wie oben |
| `Icon.ExcelExport` | „Nach Excel" | `GetExcelFormat.xaml` | VS 2022 | wie oben |
| `Icon.QueryLoad`, `Icon.QueryLoadLarge` | Ribbon „Laden" (Abfrage) | `FolderOpen_16x16_72.png`, `FolderOpen_32x32_72.png` | VS Image Library 2012, Ordner `Objects VS2012 / png_format / WinVista` | unverändert übernommen als `Resources/Icons/FolderOpen16.png` / `FolderOpen32.png` |
| `Icon.QuerySave` | Ribbon „Speichern" (Abfrage) | `base_floppydisk_32.png` | VS Image Library 2012, Ordner `Objects VS2012 / png_format / Office and VS` | unverändert übernommen als `Resources/Icons/QuerySave32.png` (nur 32×32) |
| `Icon.CopyRow` | Kontextmenü „Zeile kopieren" | `Copy.xaml` | VS 2022 | wie oben |
| `Icon.CopyAllRows` | Kontextmenü „Alle Zeilen kopieren" | `SelectAll.xaml` | VS 2022 | wie oben |
| `Icon.LevelInformation` | Ausgabe/Fehlerliste, Ebene „Information" | `StatusInformation.xaml` | VS 2022 | wie oben |
| `Icon.LevelWarning` | Ebene „Warnung" | `StatusWarning.xaml` | VS 2022 | wie oben |
| `Icon.LevelError` | Ebene „Fehler" | `StatusError.xaml` | VS 2022 | wie oben |
| `Icon.LevelDebug` | Ebene „Debug" | `Debug.xaml` | VS 2022 | wie oben |
| `Icon.RepositoryInfo`, `Icon.RepositoryInfoLarge` | Start-Ribbon „Repository-Info" | `023_Tip_16x16_72.png`, `023_Tip_32x32_72.png` (zusätzlich `023_Tip_48x48_72.png` abgelegt) | VS Image Library 2012, Ordner `Objects VS2012 / png_format / WinVista` | unverändert übernommen als `Resources/Icons/RepositoryInfo16.png` / `RepositoryInfo32.png` |
| _(Anwendungssymbol)_ | Taskleiste, Alt-Tab, `.exe`, Fenster-Titelleiste | `Compass.xaml` | VS 2022 | auf `#0F6CBD` umgefärbt, als `Resources/CMISPilot.ico` exportiert |

Zu jedem `.xaml` aus der VS Image Library liegt hier auch die zugehörige `.png`
als schnelle Sichtvorlage.

## Neue Grafiken hinzufügen

1. Originaldatei hierher in `Res/` kopieren.
2. In der Tabelle oben eine Zeile ergänzen: Schlüssel, Verwendung, Dateiname,
   Quelle, Anpassung. Bei anderer Herkunft als der VS Image Library die genaue
   Quelle und die Lizenz-/Nutzungslage nennen.
3. Icon im Programm einbinden (Vektor: `DrawingImage` in
   `src/CMISPilot.Desktop/Resources/Icons/Icons.xaml`; Raster: Datei unter
   `src/CMISPilot.Desktop/Resources/Icons/` mit Build-Aktion `Resource`).
