using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace PlaySounds;

public partial class PlaySounds : BasePlugin, IPluginConfig<PlaySoundsConfig>
{
    public override string ModuleName => "PlaySounds";
    public override string ModuleVersion => "1.4.0";
    public override string ModuleAuthor => "Lonza";
    public override string ModuleDescription => "Permite a los admins reproducir sonidos a los jugadores.";

    public PlaySoundsConfig Config { get; set; } = new();

    private enum PlayMode
    {
        /// <summary>Solo lo oyen los objetivos, emitido desde su propio jugador.</summary>
        Private,
        /// <summary>Emitido desde la posición de los objetivos, lo oyen todos los que estén cerca.</summary>
        AtPosition,
    }

    public void OnConfigParsed(PlaySoundsConfig config)
    {
        // System.Text.Json crea el diccionario sin comparador: lo rehacemos para que los alias no distingan mayúsculas.
        config.Sounds = new Dictionary<string, string>(config.Sounds, StringComparer.OrdinalIgnoreCase);
        config.DefaultVolume = Math.Clamp(config.DefaultVolume, 0f, 1f);
        config.Radio.Songs = new Dictionary<string, string>(config.Radio.Songs, StringComparer.OrdinalIgnoreCase);
        config.Radio.DefaultVolume = Math.Clamp(config.Radio.DefaultVolume, 0f, 1f);
        Config = config;

        if (!Messages.Languages.TryGetValue(config.Language, out var texts))
        {
            Console.WriteLine($"[PlaySounds] Idioma \"{config.Language}\" no disponible, se usa \"{Messages.FallbackLanguage}\". " +
                              $"Disponibles: {string.Join(", ", Messages.Languages.Keys)}");
            texts = Messages.Languages[Messages.FallbackLanguage];
        }
        _texts = texts;
    }

    private Dictionary<string, string> _texts = Messages.Languages[Messages.FallbackLanguage];

    // Texto traducido. {0}, {1}... se sustituyen a mano (no con string.Format) para no chocar con {green}, {red}...
    private string T(string key, params object[] args)
    {
        if (!_texts.TryGetValue(key, out var text) &&
            !Messages.Languages[Messages.FallbackLanguage].TryGetValue(key, out text))
            return key;

        for (var i = 0; i < args.Length; i++)
            text = text.Replace($"{{{i}}}", args[i].ToString());
        return text;
    }

    // Comandos registrados, para poder quitarlos y volver a registrarlos al recargar el config.
    private readonly List<(string Name, CommandInfo.CommandCallback Handler)> _registeredCommands = [];

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnServerPrecacheResources>(manifest =>
        {
            foreach (var file in Config.SoundEventFiles)
                manifest.AddResource(file);
        });

        LoadRadio();
        RegisterCommands();
    }

    #region Comandos

    private void RegisterCommands()
    {
        foreach (var (name, handler) in _registeredCommands)
            RemoveCommand(name, handler);
        _registeredCommands.Clear();

        var names = Config.Commands;
        Register(names.Menu, T("Desc.Menu"), OnMenuCommand);
        Register(names.PlayAll, T("Desc.PlayAll"), OnPlayAllCommand);
        Register(names.PlayTo, T("Desc.PlayTo"), OnPlayToCommand);
        Register(names.PlayAt, T("Desc.PlayAt"), OnPlayAtCommand);
        Register(names.List, T("Desc.List"), OnListCommand);
        Register(names.Reload, T("Desc.Reload"), OnReloadCommand);

        if (Config.Radio.Enabled)
        {
            Register(Config.Radio.Commands, T("Desc.Radio"), OnRadioCommand);
            Register(Config.Radio.StopCommands, T("Desc.RadioStop"), OnRadioStopCommand);
        }
    }

    private void Register(List<string> aliases, string description, CommandInfo.CommandCallback handler)
    {
        foreach (var alias in aliases)
        {
            var name = alias.Trim().TrimStart('!', '/').ToLowerInvariant();
            if (name.Length == 0 || name.Contains(' ')) continue;
            if (!name.StartsWith("css_")) name = "css_" + name;
            if (_registeredCommands.Any(c => c.Name == name)) continue;

            AddCommand(name, description, handler);
            _registeredCommands.Add((name, handler));
        }
    }

    // Nombre del primer alias de un comando, tal como se escribe en el chat (para los mensajes de uso).
    private static string ChatName(List<string> aliases) =>
        "!" + (aliases.FirstOrDefault()?.Trim().TrimStart('!', '/') ?? "?");

    // <menu>
    private void OnMenuCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller is null)
        {
            command.ReplyToCommand(T("OnlyInGame"));
            return;
        }
        if (!HasAccess(caller, command)) return;

        OpenSoundMenu(caller);
    }

    // <playall> <sonido> [volumen] -> todos lo oyen "dentro de su cabeza"
    private void OnPlayAllCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;
        if (!RequireArgs(caller, command, 1, $"{ChatName(Config.Commands.PlayAll)} {T("Args.Sound")}")) return;
        if (!TryResolveSound(caller, command, command.GetArg(1), out var sound)) return;

        PlayToAll(caller, sound, ParseVolume(command, 2), command);
    }

    // <playto> <objetivo> <sonido> [volumen] -> solo el objetivo lo oye
    private void OnPlayToCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;
        if (!RequireArgs(caller, command, 2, $"{ChatName(Config.Commands.PlayTo)} {T("Args.TargetSound")}")) return;
        if (!TryGetTargets(caller, command, out var targets)) return;
        if (!TryResolveSound(caller, command, command.GetArg(2), out var sound)) return;

        Play(caller, PlayMode.Private, targets, sound, ParseVolume(command, 3), command);
    }

    // <playat> <objetivo> <sonido> [volumen] -> suena en la posición del objetivo, lo oyen todos los cercanos en 3D
    private void OnPlayAtCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;
        if (!RequireArgs(caller, command, 2, $"{ChatName(Config.Commands.PlayAt)} {T("Args.TargetSound")}")) return;
        if (!TryGetTargets(caller, command, out var targets)) return;
        if (!TryResolveSound(caller, command, command.GetArg(2), out var sound)) return;

        Play(caller, PlayMode.AtPosition, targets, sound, ParseVolume(command, 3), command);
    }

    private void OnListCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;

        if (Config.Sounds.Count == 0)
        {
            Reply(caller, command, T("NoSoundsConfigured"));
            return;
        }

        Reply(caller, command, T("SoundList"));
        foreach (var (alias, soundEvent) in Config.Sounds)
            Reply(caller, command, $"  {{green}}{alias}{{default}} -> {soundEvent}");
    }

    private void OnReloadCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;

        try
        {
            OnConfigParsed(ConfigManager.Load<PlaySoundsConfig>(ModuleName));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error recargando el config de PlaySounds");
            Reply(caller, command, T("ReloadError"));
            return;
        }

        // Responder antes de re-registrar: el propio comando de recarga puede cambiar de nombre.
        Reply(caller, command, T("Reloaded", Config.Sounds.Count, ChatName(Config.Commands.Menu)));
        Server.NextFrame(RegisterCommands);
    }

    private bool RequireArgs(CCSPlayerController? caller, CommandInfo command, int count, string usage)
    {
        if (command.ArgCount > count) return true;

        Reply(caller, command, T("Usage", usage));
        return false;
    }

    #endregion

    #region Menú

    // Paso 1: elegir sonido
    private void OpenSoundMenu(CCSPlayerController admin)
    {
        var menu = CreateMenu(T("Menu.Sounds"));

        if (Config.Sounds.Count == 0)
            menu.AddMenuOption(T("Menu.NoSounds"), (_, _) => { }, disabled: true);

        foreach (var (alias, soundEvent) in Config.Sounds)
            menu.AddMenuOption(alias, (p, _) => OpenModeMenu(p, alias, soundEvent));

        menu.Open(admin);
    }

    // Paso 2: elegir a quién
    private void OpenModeMenu(CCSPlayerController admin, string alias, string sound)
    {
        var menu = CreateMenu(T("Menu.Who", alias));

        menu.AddMenuOption(T("Menu.Everyone"), (p, _) =>
        {
            PlayToAll(p, sound, Config.DefaultVolume);
            AfterPlay(p);
        });
        menu.AddMenuOption(T("Menu.OnlyPlayer"), (p, _) => OpenPlayerMenu(p, alias, sound, PlayMode.Private));
        menu.AddMenuOption(T("Menu.NextToPlayer"), (p, _) => OpenPlayerMenu(p, alias, sound, PlayMode.AtPosition));
        menu.AddMenuOption(T("Menu.Back"), (p, _) => OpenSoundMenu(p));

        menu.Open(admin);
    }

    // Paso 3: elegir jugador
    private void OpenPlayerMenu(CCSPlayerController admin, string alias, string sound, PlayMode mode)
    {
        var title = mode == PlayMode.Private ? T("Menu.OnlyTo", alias) : T("Menu.NextTo", alias);
        var menu = CreateMenu(title);

        menu.AddMenuOption(T("Menu.RandomAlive"), (p, _) =>
        {
            var alive = Utilities.GetPlayers().Where(pl => IsValidHuman(pl) && pl.PawnIsAlive).ToList();
            if (alive.Count == 0)
            {
                Reply(p, null, T("NoAlivePlayers"));
                return;
            }

            Play(p, mode, [alive[Random.Shared.Next(alive.Count)]], sound, Config.DefaultVolume);
            AfterPlay(p);
        });

        var players = Utilities.GetPlayers()
            .Where(IsValidHuman)
            .OrderByDescending(pl => pl.PawnIsAlive)
            .ThenBy(pl => pl.PlayerName);

        foreach (var target in players)
        {
            // Guardamos el userid y no el objeto: el jugador puede haberse ido cuando se pulse la opción.
            var userId = target.UserId ?? -1;
            var label = $"{target.PlayerName} [{TeamTag(target.Team)}]{(target.PawnIsAlive ? "" : " " + T("Menu.Dead"))}";
            var needsPawn = mode == PlayMode.AtPosition && !target.PawnIsAlive;

            menu.AddMenuOption(label, (p, _) =>
            {
                var current = Utilities.GetPlayerFromUserid(userId);
                if (current is null || !IsValidHuman(current))
                {
                    Reply(p, null, T("PlayerGone"));
                    OpenPlayerMenu(p, alias, sound, mode);
                    return;
                }

                Play(p, mode, [current], sound, Config.DefaultVolume);
                AfterPlay(p);
            }, disabled: needsPawn);
        }

        menu.AddMenuOption(T("Menu.Back"), (p, _) => OpenModeMenu(p, alias, sound));

        menu.Open(admin);
    }

    private IMenu CreateMenu(string title)
    {
        IMenu menu = Config.MenuType.Equals("center", StringComparison.OrdinalIgnoreCase)
            ? new CenterHtmlMenu(title, this)
            : new ChatMenu(title);

        // Nosotros abrimos el siguiente menú en el callback; si la librería cerrara después, lo cerraría.
        menu.PostSelectAction = PostSelectAction.Nothing;
        menu.ExitButton = true;
        return menu;
    }

    private void AfterPlay(CCSPlayerController admin)
    {
        if (Config.ReopenMenuAfterPlay)
            OpenSoundMenu(admin);
        else
            MenuManager.CloseActiveMenu(admin);
    }

    private static string TeamTag(CsTeam team) => team switch
    {
        CsTeam.CounterTerrorist => "CT",
        CsTeam.Terrorist => "T",
        _ => "SPEC",
    };

    #endregion

    #region Reproducción

    private void PlayToAll(CCSPlayerController? caller, string sound, float volume, CommandInfo? command = null)
    {
        var players = Utilities.GetPlayers().Where(IsValidHuman).ToList();
        foreach (var player in players)
            EmitFrom(player, sound, volume, new RecipientFilter { player });

        Reply(caller, command, T("PlayingAll", sound, players.Count));
        NotifyAdmins(caller, name => T("Notify.All", name, sound));
    }

    private void Play(CCSPlayerController? caller, PlayMode mode, List<CCSPlayerController> targets, string sound,
        float volume, CommandInfo? command = null)
    {
        foreach (var target in targets)
        {
            if (mode == PlayMode.Private)
            {
                // Se emite desde el propio jugador y solo él lo recibe: suena "pegado" a él.
                EmitFrom(target, sound, volume, new RecipientFilter { target });
            }
            else
            {
                var filter = new RecipientFilter();
                filter.AddAllPlayers();
                EmitFrom(target, sound, volume, filter);
            }
        }

        var who = DescribeTargets(targets);
        var isPrivate = mode == PlayMode.Private;
        Reply(caller, command, T(isPrivate ? "PlayingTo" : "PlayingAt", sound, who));
        NotifyAdmins(caller, name => T(isPrivate ? "Notify.To" : "Notify.At", name, sound, who));
    }

    private static void EmitFrom(CCSPlayerController player, string sound, float volume, RecipientFilter filter)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid) return;

        pawn.EmitSound(sound, filter, volume);
    }

    #endregion

    #region Utilidades

    private bool HasAccess(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller is null || AdminManager.PlayerHasPermissions(caller, Config.AdminFlag))
            return true;

        Reply(caller, command, T("NoPermission"));
        return false;
    }

    private bool TryResolveSound(CCSPlayerController? caller, CommandInfo command, string input, out string sound)
    {
        sound = Config.Sounds.TryGetValue(input, out var mapped) ? mapped : input;
        if (!string.IsNullOrWhiteSpace(sound)) return true;

        Reply(caller, command, T("InvalidSound", ChatName(Config.Commands.List)));
        return false;
    }

    private bool TryGetTargets(CCSPlayerController? caller, CommandInfo command, out List<CCSPlayerController> targets)
    {
        targets = command.GetArgTargetResult(1).Players.Where(IsValidHuman).ToList();
        if (targets.Count > 0) return true;

        Reply(caller, command, T("NoTargets"));
        return false;
    }

    private float ParseVolume(CommandInfo command, int index)
    {
        if (command.ArgCount > index &&
            float.TryParse(command.GetArg(index).Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var volume))
            return Math.Clamp(volume, 0f, 1f);

        return Config.DefaultVolume;
    }

    private static bool IsValidHuman(CCSPlayerController player) =>
        player is { IsValid: true, IsBot: false, IsHLTV: false, Connected: PlayerConnectedState.Connected };

    private string DescribeTargets(List<CCSPlayerController> targets) =>
        targets.Count == 1 ? targets[0].PlayerName : T("PlayerCount", targets.Count);

    private void Reply(CCSPlayerController? caller, CommandInfo? command, string message)
    {
        if (caller is null)
            command?.ReplyToCommand(StripColors(message));
        else
            caller.PrintToChat(ReplaceColors($"{Config.ChatPrefix} {message}"));
    }

    // El texto recibe el nombre de quien lo hizo (cada idioma lo coloca donde toca en la frase).
    private void NotifyAdmins(CCSPlayerController? caller, Func<string, string> text)
    {
        if (!Config.NotifyAdmins) return;

        var name = caller?.PlayerName ?? T("Console");
        var message = ReplaceColors($"{Config.ChatPrefix} {{grey}}{text(name)}");
        foreach (var admin in Utilities.GetPlayers().Where(IsValidHuman))
        {
            if (admin != caller && AdminManager.PlayerHasPermissions(admin, Config.AdminFlag))
                admin.PrintToChat(message);
        }
    }

    private static readonly Dictionary<string, string> ColorTags = typeof(ChatColors)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.FieldType == typeof(char))
        .ToDictionary(f => $"{{{f.Name.ToLowerInvariant()}}}", f => f.GetValue(null)!.ToString()!);

    private static string ReplaceColors(string message)
    {
        foreach (var (tag, color) in ColorTags)
            message = message.Replace(tag, color, StringComparison.OrdinalIgnoreCase);
        return message;
    }

    private static string StripColors(string message) =>
        System.Text.RegularExpressions.Regex.Replace(message, @"\{[a-zA-Z]+\}", string.Empty);

    #endregion
}
