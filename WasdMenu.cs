using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace PlaySounds;

public class WasdMenuOption
{
    public string Display { get; init; } = "";
    public Action<CCSPlayerController> OnChoose { get; init; } = null!;
    public bool Disabled { get; init; }
}

public class WasdMenu(string title)
{
    public string Title { get; } = title;
    public List<WasdMenuOption> Options { get; } = [];
}

/// <summary>Menú en el centro de la pantalla: W/S para moverse, E para elegir y R para cerrar.</summary>
public class WasdMenuManager
{
    private const int VisibleOptions = 6;
    private const int MaxNameLength = 30;

    private class OpenMenuState
    {
        public CCSPlayerController Player = null!;
        public WasdMenu Menu = null!;
        public int Selected;
        public PlayerButtons Buttons;
        public string Html = "";
    }

    private readonly Dictionary<int, OpenMenuState> _open = [];

    public void Open(CCSPlayerController player, WasdMenu menu)
    {
        if (menu.Options.Count == 0) return;

        // Al pasar de un menú a otro no se suelta al jugador: así no da un paso con la W entre medias
        var wasOpen = _open.Remove(player.Slot);
        var first = menu.Options.FindIndex(o => !o.Disabled);
        var state = new OpenMenuState { Player = player, Menu = menu, Buttons = player.Buttons, Selected = Math.Max(0, first) };
        _open[player.Slot] = state;
        if (!wasOpen) SetMoveType(player, MoveType_t.MOVETYPE_NONE);
        Render(state);
    }

    public void Close(CCSPlayerController player)
    {
        if (!_open.Remove(player.Slot)) return;
        if (!player.IsValid) return;
        SetMoveType(player, MoveType_t.MOVETYPE_WALK);
        player.PrintToCenterHtml(" ");
    }

    /// <summary>Olvida a un jugador desconectado sin tocar su controller (ya no es válido).</summary>
    public void Remove(int slot) => _open.Remove(slot);

    public void CloseAll()
    {
        foreach (var state in _open.Values.ToList())
            Close(state.Player);
    }

    public void OnTick()
    {
        foreach (var state in _open.Values.ToList())
        {
            var player = state.Player;

            // Un callback anterior en este mismo tick puede haber cerrado o cambiado este menú
            if (!_open.TryGetValue(player.Slot, out var current) || current != state) continue;

            if (!player.IsValid)
            {
                _open.Remove(player.Slot);
                continue;
            }

            var pressed = player.Buttons & ~state.Buttons;
            state.Buttons = player.Buttons;

            if ((pressed & PlayerButtons.Forward) != 0)
            {
                Move(state, -1);
            }
            else if ((pressed & PlayerButtons.Back) != 0)
            {
                Move(state, 1);
            }
            else if ((pressed & PlayerButtons.Use) != 0)
            {
                var option = state.Menu.Options[state.Selected];
                if (!option.Disabled) option.OnChoose(player);
            }
            else if ((pressed & PlayerButtons.Reload) != 0)
            {
                Close(player);
            }

            // El HTML del centro se desvanece si no se reenvía cada tick
            if (_open.TryGetValue(player.Slot, out current))
                player.PrintToCenterHtml(current.Html);
        }
    }

    // Salta las opciones deshabilitadas (si todas lo están se queda donde está)
    private static void Move(OpenMenuState state, int step)
    {
        var count = state.Menu.Options.Count;
        for (int i = 1; i <= count; i++)
        {
            int next = ((state.Selected + step * i) % count + count) % count;
            if (state.Menu.Options[next].Disabled) continue;
            state.Selected = next;
            break;
        }
        Render(state);
    }

    private static void Render(OpenMenuState state)
    {
        var options = state.Menu.Options;
        int first = Math.Clamp(state.Selected - VisibleOptions + 1, 0, Math.Max(0, options.Count - VisibleOptions));
        int last = Math.Min(options.Count, first + VisibleOptions);

        var sb = new StringBuilder();
        sb.Append($"<b><font color='#ff4444' class='fontSize-m'>{state.Menu.Title}</font></b><br>");

        for (int i = first; i < last; i++)
        {
            var option = options[i];

            // El panel del centro tiene tamaño fijo: un nombre que salte de línea empuja la ayuda fuera
            string name = option.Display.Length > MaxNameLength
                ? option.Display[..(MaxNameLength - 1)].TrimEnd() + "…"
                : option.Display;

            if (option.Disabled)
                sb.Append($"<font color='gray' class='fontSize-sm'>{name}</font><br>");
            else if (i == state.Selected)
                sb.Append($"<font color='yellow'>►[</font> <font color='#9acd32' class='fontSize-sm'>{name}</font> <font color='yellow'>]◄</font><br>");
            else
                sb.Append($"<font color='white' class='fontSize-sm'>{name}</font><br>");
        }

        if (last < options.Count)
            sb.Append("<font color='gray'>▼ ▼ ▼</font><br>");

        sb.Append("<font color='#ff3333' class='fontSize-sm'>Move: <font color='#f5a142'>[W/S]</font> Select: <font color='#f5a142'>[E]</font> Exit: <font color='#f5a142'>[R]</font></font>");
        state.Html = sb.ToString();
    }

    private static void SetMoveType(CCSPlayerController player, MoveType_t moveType)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid) return;

        pawn.MoveType = moveType;
        Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", moveType);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
    }
}
