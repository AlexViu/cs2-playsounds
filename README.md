# PlaySounds (CounterStrikeSharp)

Plugin para que los admins reproduzcan sonidos a los jugadores durante la partida. Pensado para un evento de Halloween.

## Comandos

| Comando | Qué hace |
|---|---|
| `css_soundmenu` | Abre el menú: sonido → a quién → jugador. |
| `css_sound <sonido> [volumen]` | Todos lo oyen, "dentro de su cabeza". |
| `css_soundto <objetivo> <sonido> [volumen]` | Solo lo oye el objetivo. Los demás no se enteran. |
| `css_soundat <objetivo> <sonido> [volumen]` | Suena en la posición del objetivo y lo oye cualquiera que esté cerca (sonido 3D). |
| `css_sounds` | Lista los alias configurados. |
| `css_soundsreload` | Recarga `PlaySounds.json` sin reiniciar el servidor. |

### Menú (`!soundmenu`)

1. Eliges el sonido (salen en el mismo orden que en el JSON).
2. Eliges a quién: **Todos**, **Solo a un jugador** o **Junto a un jugador (3D)**.
3. Si es para un jugador, eliges de la lista (vivos primero, con su equipo) o **Jugador vivo aleatorio**.

Después de reproducir, el menú vuelve a la lista de sonidos para poder encadenar sustos (se desactiva con `ReopenMenuAfterPlay: false`). Con el menú de chat se elige escribiendo `!1`, `!2`...; si prefieres el menú en el centro de la pantalla, pon `"MenuType": "center"`.

En el chat también funcionan como `!sound`, `!soundto`, etc. (o `/sound` para que no se vea el mensaje).

- `<sonido>`: un alias del config (`grito`) o el nombre del soundevent directamente (`halloween.scream`).
- `<objetivo>`: nombre, `#userid`, `@all`, `@ct`, `@t`, `@alive`, `@dead`, `@me`...
- `[volumen]`: opcional, de `0` a `1`.

Ejemplos:
```
css_soundto "Pepe" susurro          // solo Pepe oye un susurro
css_soundat @ct puerta 0.8          // suena una puerta donde está cada CT
css_sound grito                     // grito para todo el servidor
```

Por defecto hace falta el permiso `@css/generic` (se puede cambiar en el config). Desde la consola del servidor se puede usar siempre.

## Instalación

1. Compila: `dotnet build -c Release` (necesita el SDK de .NET 10, igual que CounterStrikeSharp 1.0.375).
2. Copia `bin/Release/net10.0/PlaySounds.dll` a `game/csgo/addons/counterstrikesharp/plugins/PlaySounds/`.
3. Arranca el servidor una vez; se crea el config en `game/csgo/addons/counterstrikesharp/configs/plugins/PlaySounds/PlaySounds.json`.

## Sonidos personalizados (importante)

En CS2 no se pueden reproducir `.mp3`/`.wav` sueltos desde el servidor. Los sonidos tienen que ser **soundevents** que el cliente ya tenga descargados, así que hay que meterlos en un **addon del Workshop**:

1. Instala las CS2 Workshop Tools y crea un addon.
2. Mete tus audios (`.wav` o `.mp3`) en `content/csgo_addons/<tu_addon>/sounds/halloween/`.
3. Copia `addon-ejemplo/soundevents/soundevents_addon.vsndevts` a `content/csgo_addons/<tu_addon>/soundevents/` y ajusta nombres y rutas (en el `.vsndevts` las rutas van con extensión `.vsnd`).
4. Compila el addon (abre el `.vsndevts` en el Asset Browser o compila el mapa/addon) y súbelo al Workshop.
5. En el servidor, instala [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager) y añade el ID del Workshop a `mm_extra_addons` para que los jugadores lo descarguen al entrar.
6. Pon los alias en el config:

```json
{
  "AdminFlag": "@css/generic",
  "ChatPrefix": " {darkred}[Halloween]{default}",
  "SoundEventFiles": [ "soundevents/soundevents_addon.vsndevts" ],
  "DefaultVolume": 1.0,
  "NotifyAdmins": true,
  "MenuType": "chat",
  "ReopenMenuAfterPlay": true,
  "Sounds": {
    "grito": "halloween.scream",
    "risa": "halloween.laugh",
    "susurro": "halloween.whisper",
    "puerta": "halloween.door",
    "latido": "halloween.heartbeat"
  }
}
```

Para añadir un sonido, basta con una línea nueva en `"Sounds"`: `"nombre que sale en el menú": "nombre.del.soundevent"`. Después usa `css_soundsreload`.

El plugin precachea los archivos de `SoundEventFiles` al cargar cada mapa. Si cambias esa lista, recarga el mapa.

### Consejos para asustar

- Los sonidos tipo `csgo_mega` suenan igual estés donde estés: úsalos con `css_sound` / `css_soundto`.
- Los tipo `csgo_3d` se atenúan con la distancia: con `css_soundat` parece que algo está *al lado* del jugador.
- Un `latido` a una sola persona con `css_soundto` mientras está sola en un pasillo funciona muy bien.
- Antes del evento, prueba cada sonido en un servidor local: si un soundevent no existe o no se ha descargado, no suena y no da error.
