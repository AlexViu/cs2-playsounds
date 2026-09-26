using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace PlaySounds;

public class PlaySounds : BasePlugin, IPluginConfig<PlaySoundsConfig>
{
    public override string ModuleName => "PlaySounds";
    public override string ModuleVersion => "1.2.0";
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
        Config = config;
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

        RegisterCommands();
    }

    #region Comandos

    private void RegisterCommands()
    {
        foreach (var (name, handler) in _registeredCommands)
            RemoveCommand(name, handler);
        _registeredCommands.Clear();

        var names = Config.Commands;
        Register(names.Menu, "Abre el menú de sonidos", OnMenuCommand);
        Register(names.PlayAll, "Reproduce un sonido a todos los jugadores", OnPlayAllCommand);
        Register(names.PlayTo, "Reproduce un sonido solo a los jugadores objetivo", OnPlayToCommand);
        Register(names.PlayAt, "Reproduce un sonido en la posición de un jugador (audible por los cercanos)", OnPlayAtCommand);
        Register(names.List, "Lista los sonidos disponibles", OnListCommand);
        Register(names.Reload, "Recarga PlaySounds.json sin reiniciar el servidor", OnReloadCommand);
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
            command.ReplyToCommand("El menú solo se puede abrir desde el juego.");
            return;
        }
        if (!HasAccess(caller, command)) return;

        OpenSoundMenu(caller);
    }

    // <playall> <sonido> [volumen] -> todos lo oyen "dentro de su cabeza"
    private void OnPlayAllCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;
        if (!RequireArgs(caller, command, 1, $"{ChatName(Config.Commands.PlayAll)} <sonido> [volumen 0-1]")) return;
        if (!TryResolveSound(caller, command, command.GetArg(1), out var sound)) return;

        PlayToAll(caller, sound, ParseVolume(command, 2), command);
    }

    // <playto> <objetivo> <sonido> [volumen] -> solo el objetivo lo oye
    private void OnPlayToCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;
        if (!RequireArgs(caller, command, 2, $"{ChatName(Config.Commands.PlayTo)} <objetivo> <sonido> [volumen 0-1]")) return;
        if (!TryGetTargets(caller, command, out var targets)) return;
        if (!TryResolveSound(caller, command, command.GetArg(2), out var sound)) return;

        Play(caller, PlayMode.Private, targets, sound, ParseVolume(command, 3), command);
    }

    // <playat> <objetivo> <sonido> [volumen] -> suena en la posición del objetivo, lo oyen todos los cercanos en 3D
    private void OnPlayAtCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;
        if (!RequireArgs(caller, command, 2, $"{ChatName(Config.Commands.PlayAt)} <objetivo> <sonido> [volumen 0-1]")) return;
        if (!TryGetTargets(caller, command, out var targets)) return;
        if (!TryResolveSound(caller, command, command.GetArg(2), out var sound)) return;

        Play(caller, PlayMode.AtPosition, targets, sound, ParseVolume(command, 3), command);
    }

    private void OnListCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!HasAccess(caller, command)) return;

        if (Config.Sounds.Count == 0)
        {
            Reply(caller, command, "No hay sonidos en el config. Puedes usar el nombre del soundevent directamente.");
            return;
        }

        Reply(caller, command, "Sonidos disponibles:");
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
            Reply(caller, command, "{red}Error al recargar el config. Revisa que el JSON sea válido (mira la consola).");
            return;
        }

        // Responder antes de re-registrar: el propio comando de recarga puede cambiar de nombre.
        Reply(caller, command, $"Config recargado: {Config.Sounds.Count} sonido(s). Menú: {ChatName(Config.Commands.Menu)}");
        Server.NextFrame(RegisterCommands);
    }

    private bool RequireArgs(CCSPlayerController? caller, CommandInfo command, int count, string usage)
    {
        if (command.ArgCount > count) return true;

        Reply(caller, command, $"Uso: {usage}");
        return false;
    }

    #endregion

    #region Menú

    // Paso 1: elegir sonido
    private void OpenSoundMenu(CCSPlayerController admin)
    {
        var menu = CreateMenu("Sonidos de Halloween");

        if (Config.Sounds.Count == 0)
            menu.AddMenuOption("(no hay sonidos en el config)", (_, _) => { }, disabled: true);

        foreach (var (alias, soundEvent) in Config.Sounds)
            menu.AddMenuOption(alias, (p, _) => OpenModeMenu(p, alias, soundEvent));

        menu.Open(admin);
    }

    // Paso 2: elegir a quién
    private void OpenModeMenu(CCSPlayerController admin, string alias, string sound)
    {
        var menu = CreateMenu($"{alias}: ¿a quién?");

        menu.AddMenuOption("Todos (en su cabeza)", (p, _) =>
        {
            PlayToAll(p, sound, Config.DefaultVolume);
            AfterPlay(p);
        });
        menu.AddMenuOption("Solo a un jugador", (p, _) => OpenPlayerMenu(p, alias, sound, PlayMode.Private));
        menu.AddMenuOption("Junto a un jugador (3D)", (p, _) => OpenPlayerMenu(p, alias, sound, PlayMode.AtPosition));
        menu.AddMenuOption("« Volver", (p, _) => OpenSoundMenu(p));

        menu.Open(admin);
    }

    // Paso 3: elegir jugador
    private void OpenPlayerMenu(CCSPlayerController admin, string alias, string sound, PlayMode mode)
    {
        var title = mode == PlayMode.Private ? $"{alias}: solo a..." : $"{alias}: junto a...";
        var menu = CreateMenu(title);

        menu.AddMenuOption("Jugador vivo aleatorio", (p, _) =>
        {
            var alive = Utilities.GetPlayers().Where(pl => IsValidHuman(pl) && pl.PawnIsAlive).ToList();
            if (alive.Count == 0)
            {
                Reply(p, null, "{red}No hay jugadores vivos.");
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
            var label = $"{target.PlayerName} [{TeamTag(target.Team)}]{(target.PawnIsAlive ? "" : " (muerto)")}";
            var needsPawn = mode == PlayMode.AtPosition && !target.PawnIsAlive;

            menu.AddMenuOption(label, (p, _) =>
            {
                var current = Utilities.GetPlayerFromUserid(userId);
                if (current is null || !IsValidHuman(current))
                {
                    Reply(p, null, "{red}Ese jugador ya no está en el servidor.");
                    OpenPlayerMenu(p, alias, sound, mode);
                    return;
                }

                Play(p, mode, [current], sound, Config.DefaultVolume);
                AfterPlay(p);
            }, disabled: needsPawn);
        }

        menu.AddMenuOption("« Volver", (p, _) => OpenModeMenu(p, alias, sound));

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

        Reply(caller, command, $"Reproduciendo {{green}}{sound}{{default}} a todos ({players.Count}).");
        NotifyAdmins(caller, $"reprodujo {sound} a todos");
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

        var where = mode == PlayMode.Private ? "a" : "junto a";
        Reply(caller, command, $"Reproduciendo {{green}}{sound}{{default}} {where} {DescribeTargets(targets)}.");
        NotifyAdmins(caller, $"reprodujo {sound} {where} {DescribeTargets(targets)}");
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

        Reply(caller, command, "{red}No tienes permiso para usar este comando.");
        return false;
    }

    private bool TryResolveSound(CCSPlayerController? caller, CommandInfo command, string input, out string sound)
    {
        sound = Config.Sounds.TryGetValue(input, out var mapped) ? mapped : input;
        if (!string.IsNullOrWhiteSpace(sound)) return true;

        Reply(caller, command, $"{{red}}Sonido no válido. Usa {ChatName(Config.Commands.List)} para ver la lista.");
        return false;
    }

    private bool TryGetTargets(CCSPlayerController? caller, CommandInfo command, out List<CCSPlayerController> targets)
    {
        targets = command.GetArgTargetResult(1).Players.Where(IsValidHuman).ToList();
        if (targets.Count > 0) return true;

        Reply(caller, command, "{red}No se encontró ningún jugador. Ejemplos: nombre, #userid, @all, @ct, @t, @alive.");
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

    private static string DescribeTargets(List<CCSPlayerController> targets) =>
        targets.Count == 1 ? targets[0].PlayerName : $"{targets.Count} jugadores";

    private void Reply(CCSPlayerController? caller, CommandInfo? command, string message)
    {
        if (caller is null)
            command?.ReplyToCommand(StripColors(message));
        else
            caller.PrintToChat(ReplaceColors($"{Config.ChatPrefix} {message}"));
    }

    private void NotifyAdmins(CCSPlayerController? caller, string action)
    {
        if (!Config.NotifyAdmins) return;

        var name = caller?.PlayerName ?? "Consola";
        var message = ReplaceColors($"{Config.ChatPrefix} {{grey}}{name} {action}.");
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
