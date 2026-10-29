using System.Globalization;

namespace FreeNotch;

public static class Strings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Table = new()
    {
        ["tr"] = new()
        {
            ["Nothing is playing"] = "Şu an bir şey çalmıyor",
            ["Play something in Spotify, a browser or any media app"] = "Spotify'da, tarayıcıda ya da herhangi bir medya uygulamasında bir şey çal",
            ["Unknown"] = "Bilinmiyor",
            ["Open"] = "Aç",
            ["Close"] = "Kapat",
            ["Theme"] = "Tema",
            ["Smoke Glass"] = "Duman Cam",
            ["OLED Black"] = "OLED Siyah",
            ["Replace the Windows volume pop-up"] = "Windows ses penceresinin yerine geç",
            ["Reset position"] = "Konumu sıfırla",
            ["Launch at login"] = "Oturum açılınca başlat",
            ["Quit FreeNotch"] = "FreeNotch'tan çık",
            ["Media"] = "Medya",
        },
        ["de"] = new()
        {
            ["Nothing is playing"] = "Es wird nichts abgespielt",
            ["Play something in Spotify, a browser or any media app"] = "Spiele etwas in Spotify, einem Browser oder einer Medien-App ab",
            ["Unknown"] = "Unbekannt",
            ["Open"] = "Öffnen",
            ["Close"] = "Schließen",
            ["Theme"] = "Design",
            ["Smoke Glass"] = "Rauchglas",
            ["OLED Black"] = "OLED-Schwarz",
            ["Replace the Windows volume pop-up"] = "Windows-Lautstärkeanzeige ersetzen",
            ["Reset position"] = "Position zurücksetzen",
            ["Launch at login"] = "Bei Anmeldung starten",
            ["Quit FreeNotch"] = "FreeNotch beenden",
            ["Media"] = "Medien",
        },
        ["es"] = new()
        {
            ["Nothing is playing"] = "No se está reproduciendo nada",
            ["Play something in Spotify, a browser or any media app"] = "Reproduce algo en Spotify, un navegador o cualquier app multimedia",
            ["Unknown"] = "Desconocido",
            ["Open"] = "Abrir",
            ["Close"] = "Cerrar",
            ["Theme"] = "Tema",
            ["Smoke Glass"] = "Cristal ahumado",
            ["OLED Black"] = "Negro OLED",
            ["Replace the Windows volume pop-up"] = "Reemplazar el aviso de volumen de Windows",
            ["Reset position"] = "Restablecer posición",
            ["Launch at login"] = "Abrir al iniciar sesión",
            ["Quit FreeNotch"] = "Salir de FreeNotch",
            ["Media"] = "Multimedia",
        },
        ["fr"] = new()
        {
            ["Nothing is playing"] = "Aucune lecture en cours",
            ["Play something in Spotify, a browser or any media app"] = "Lancez une lecture dans Spotify, un navigateur ou une app multimédia",
            ["Unknown"] = "Inconnu",
            ["Open"] = "Ouvrir",
            ["Close"] = "Fermer",
            ["Theme"] = "Thème",
            ["Smoke Glass"] = "Verre fumé",
            ["OLED Black"] = "Noir OLED",
            ["Replace the Windows volume pop-up"] = "Remplacer l’indicateur de volume de Windows",
            ["Reset position"] = "Réinitialiser la position",
            ["Launch at login"] = "Lancer à l’ouverture de session",
            ["Quit FreeNotch"] = "Quitter FreeNotch",
            ["Media"] = "Médias",
        },
    };

    private static readonly string Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public static string L(string key) =>
        Table.TryGetValue(Language, out var table) && table.TryGetValue(key, out var value) ? value : key;
}
