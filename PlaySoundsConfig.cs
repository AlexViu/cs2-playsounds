using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace PlaySounds;

public class PlaySoundsConfig : BasePluginConfig
{
    /// <summary>Permiso necesario para usar los comandos.</summary>
    [JsonPropertyName("AdminFlag")]
    public string AdminFlag { get; set; } = "@css/generic";

    /// <summary>Prefijo de los mensajes de chat.</summary>
    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = " {darkred}[Halloween]{default}";

    /// <summary>
    /// Archivos de soundevents que se precachean al cargar el mapa.
    /// Deben existir en un addon del workshop montado en el servidor (p. ej. con MultiAddonManager).
    /// </summary>
    [JsonPropertyName("SoundEventFiles")]
    public List<string> SoundEventFiles { get; set; } = ["soundevents/soundevents_addon.vsndevts"];

    /// <summary>Volumen por defecto (0.0 - 1.0) si no se indica en el comando.</summary>
    [JsonPropertyName("DefaultVolume")]
    public float DefaultVolume { get; set; } = 1.0f;

    /// <summary>Si es true, se avisa en el chat a los admins de quién reprodujo qué.</summary>
    [JsonPropertyName("NotifyAdmins")]
    public bool NotifyAdmins { get; set; } = true;

    /// <summary>Tipo de menú: "chat" (se elige con !1, !2...) o "center" (HTML en el centro de la pantalla).</summary>
    [JsonPropertyName("MenuType")]
    public string MenuType { get; set; } = "chat";

    /// <summary>Si es true, tras reproducir un sonido desde el menú se vuelve a abrir la lista de sonidos.</summary>
    [JsonPropertyName("ReopenMenuAfterPlay")]
    public bool ReopenMenuAfterPlay { get; set; } = true;

    /// <summary>
    /// Nombres de los comandos. Cada uno puede tener varios alias. Sin "css_" delante: se añade solo,
    /// y en el chat se usan con ! o / (p. ej. "psmenu" -> !psmenu).
    /// </summary>
    [JsonPropertyName("Commands")]
    public CommandNames Commands { get; set; } = new();

    /// <summary>Radio personal: cada jugador pone canciones que solo oye él.</summary>
    [JsonPropertyName("Radio")]
    public RadioConfig Radio { get; set; } = new();

    /// <summary>
    /// Alias cortos -> nombre del soundevent. También se puede usar el nombre del soundevent directamente.
    /// El menú muestra los sonidos en este mismo orden.
    /// </summary>
    [JsonPropertyName("Sounds")]
    public Dictionary<string, string> Sounds { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["grito"] = "halloween.scream",
        ["risa"] = "halloween.laugh",
        ["susurro"] = "halloween.whisper",
        ["puerta"] = "halloween.door",
        ["latido"] = "halloween.heartbeat",
    };
}

public class RadioConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Permiso para usar la radio. Vacío = cualquier jugador.</summary>
    [JsonPropertyName("Permission")]
    public string Permission { get; set; } = "";

    /// <summary>Volumen inicial de cada jugador (0.0 - 1.0). Cada uno lo puede cambiar desde el menú.</summary>
    [JsonPropertyName("DefaultVolume")]
    public float DefaultVolume { get; set; } = 0.5f;

    /// <summary>Comandos para abrir el menú de la radio (sin "css_").</summary>
    [JsonPropertyName("Commands")]
    public List<string> Commands { get; set; } = ["radio"];

    /// <summary>Comandos para parar la canción (sin "css_").</summary>
    [JsonPropertyName("StopCommands")]
    public List<string> StopCommands { get; set; } = ["radiostop"];

    /// <summary>
    /// Nombre que sale en el menú -> nombre del soundevent. Mejor que sean de tipo "csgo_mega"
    /// y estén en uno de los archivos de SoundEventFiles.
    /// </summary>
    [JsonPropertyName("Songs")]
    public Dictionary<string, string> Songs { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Canción 1"] = "radio.cancion1",
        ["Canción 2"] = "radio.cancion2",
    };
}

public class CommandNames
{
    [JsonPropertyName("Menu")]
    public List<string> Menu { get; set; } = ["psmenu"];

    [JsonPropertyName("PlayAll")]
    public List<string> PlayAll { get; set; } = ["psall"];

    [JsonPropertyName("PlayTo")]
    public List<string> PlayTo { get; set; } = ["psto"];

    [JsonPropertyName("PlayAt")]
    public List<string> PlayAt { get; set; } = ["psat"];

    [JsonPropertyName("List")]
    public List<string> List { get; set; } = ["pslist"];

    [JsonPropertyName("Reload")]
    public List<string> Reload { get; set; } = ["psreload"];
}
