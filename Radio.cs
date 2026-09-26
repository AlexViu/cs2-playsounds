using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace PlaySounds;

// Radio personal: cada jugador elige canciones que solo oye él.
public partial class PlaySounds
{
    private static readonly float[] RadioVolumeSteps = [0.25f, 0.5f, 0.75f, 1f];

    private sealed class RadioState
    {
        public float Volume;
        public uint? Guid;
        public string? Song;
    }

    // Por SteamID, para que el volumen elegido se mantenga entre mapas y reconexiones.
    private readonly Dictionary<ulong, RadioState> _radio = [];

    private void LoadRadio()
    {
        // Al cambiar de mapa los clientes ya cortan todos los sonidos.
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            foreach (var state in _radio.Values)
                (state.Guid, state.Song) = (null, null);
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } player && _radio.TryGetValue(player.SteamID, out var state))
                (state.Guid, state.Song) = (null, null);
            return HookResult.Continue;
        });
    }

    private void OnRadioCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller is null)
        {
            command.ReplyToCommand("La radio solo se puede usar desde el juego.");
            return;
        }
        if (!HasRadioAccess(caller)) return;

        OpenRadioMenu(caller);
    }

    private void OnRadioStopCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller is null || !HasRadioAccess(caller)) return;

        if (StopRadio(caller))
            Reply(caller, null, "Radio parada.");
        else
            Reply(caller, null, "No está sonando nada.");
    }

    private void OpenRadioMenu(CCSPlayerController player)
    {
        var state = GetRadioState(player);
        var menu = CreateMenu(state.Song is null ? "Radio" : $"Radio - {state.Song}");

        menu.AddMenuOption("Parar", (p, _) =>
        {
            StopRadio(p);
            OpenRadioMenu(p);
        }, disabled: state.Song is null);

        menu.AddMenuOption($"Volumen: {state.Volume * 100:0}%", (p, _) =>
        {
            var s = GetRadioState(p);
            var next = RadioVolumeSteps.FirstOrDefault(v => v > s.Volume + 0.01f);
            s.Volume = next == 0 ? RadioVolumeSteps[0] : next;

            if (s.Song is not null)
                Reply(p, null, $"Volumen al {s.Volume * 100:0}%. Se aplica a la siguiente canción.");
            OpenRadioMenu(p);
        });

        var songs = Config.Radio.Songs.ToList();
        if (songs.Count == 0)
            menu.AddMenuOption("(no hay canciones en el config)", (_, _) => { }, disabled: true);
        else
            menu.AddMenuOption("Aleatoria", (p, _) =>
            {
                var (name, soundEvent) = songs[Random.Shared.Next(songs.Count)];
                PlayRadio(p, name, soundEvent);
            });

        foreach (var (name, soundEvent) in songs)
        {
            var label = name == state.Song ? $"▶ {name}" : name;
            menu.AddMenuOption(label, (p, _) => PlayRadio(p, name, soundEvent));
        }

        menu.Open(player);
    }

    private void PlayRadio(CCSPlayerController player, string name, string soundEvent)
    {
        MenuManager.CloseActiveMenu(player);

        var pawn = player.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid)
        {
            Reply(player, null, "{red}No se puede poner la radio ahora mismo.");
            return;
        }

        StopRadio(player);

        var state = GetRadioState(player);
        state.Guid = pawn.EmitSound(soundEvent, new RecipientFilter { player }, state.Volume);
        state.Song = name;

        Reply(player, null, $"Sonando {{green}}{name}{{default}}. Para pararla: {ChatName(Config.Radio.StopCommands)}");
    }

    private bool StopRadio(CCSPlayerController player)
    {
        if (!_radio.TryGetValue(player.SteamID, out var state) || state.Guid is not { } guid)
            return false;

        (state.Guid, state.Song) = (null, null);

        try
        {
            using var message = UserMessage.FromPartialName("SosStopSoundEvent");
            message.SetUInt("soundevent_guid", guid);
            message.Send(new RecipientFilter { player });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "No se pudo parar la canción de la radio");
        }

        return true;
    }

    private RadioState GetRadioState(CCSPlayerController player)
    {
        if (!_radio.TryGetValue(player.SteamID, out var state))
        {
            state = new RadioState { Volume = Config.Radio.DefaultVolume };
            _radio[player.SteamID] = state;
        }
        return state;
    }

    private bool HasRadioAccess(CCSPlayerController player)
    {
        var permission = Config.Radio.Permission;
        if (string.IsNullOrWhiteSpace(permission) || AdminManager.PlayerHasPermissions(player, permission))
            return true;

        Reply(player, null, "{red}No tienes permiso para usar la radio.");
        return false;
    }
}
