using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CMISPilot.Desktop.Services;

/// <summary>
/// Fragt das von Windows der Dateiendung zugeordnete Symbol ab (<c>SHGetFileInfo</c>,
/// <c>SHGFI_USEFILEATTRIBUTES</c> – funktioniert ohne dass die Datei existiert, genau
/// wie es der Windows-Explorer selbst für die Dateiliste tut). Ergebnis je Endung
/// gecacht, da <c>SHGetFileInfo</c> ein teurer Shell-Aufruf ist und dieselbe Endung in
/// einem Ordner typischerweise mehrfach vorkommt.
///
/// Rein maschinenabhängig: welches Symbol (falls überhaupt eins) zurückkommt, hängt
/// davon ab, welche Anwendung die Endung auf diesem Rechner registriert hat. Liefert
/// Windows nichts, gibt <see cref="GetIconForFileName"/> <c>null</c> zurück – der
/// Aufrufer fällt dann auf das bisherige, feste Dokument-Icon zurück.
/// </summary>
internal static class ShellIconCache
{
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // Je Endung (inkl. Punkt, ordinal ohne Gross-/Kleinschreibung) das per Shell
    // ermittelte Symbol – oder null, wenn Windows keins liefert.
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Liefert das Betriebssystem-Symbol für die Endung von <paramref name="fileName"/>
    /// (z. B. <c>.pdf</c> → Adobe-Logo, <c>.xlsm</c> → Excel-Logo, falls die jeweilige
    /// Anwendung auf diesem Rechner installiert ist), oder <c>null</c> ohne Endung oder
    /// wenn Windows dafür kein Symbol liefert.
    /// </summary>
    public static ImageSource? GetIconForFileName(string? fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        return Cache.GetOrAdd(extension, static ext =>
        {
            var info = new SHFILEINFO();
            var result = SHGetFileInfo(
                "x" + ext, FILE_ATTRIBUTE_NORMAL, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        });
    }
}
