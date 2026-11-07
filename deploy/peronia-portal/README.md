# Web pública de PeroniaMU (portal)

El portal corre dentro del mismo proceso que el server (`openmu-startup`), en el puerto 8090,
y Caddy lo publica en `https://peroniamu.duckdns.org` con HTTPS automático.
El panel de admin sigue solo en la PC/red local: Caddy no lo expone.

## Qué hace (fase 1)

- Crear cuenta con código de invitación (los códigos están en `PORTAL_INVITE_CODES`).
- Entrar con el usuario y contraseña del juego; ver personajes, resets, nivel, mapa y WCoin.
- Cambiar contraseña (solo con la cuenta fuera del juego).
- Ranking (top 100 por resets, nivel o master, filtro por clase y búsqueda) y ranking de guilds.
- Ficha de cada personaje y de cada guild (escudo, miembros, alianza).
- Quién está jugando ahora y en qué mapa.
- Eventos de las próximas 24 horas con cuenta regresiva (sale de los plugins de eventos activos).
- Info del server (resets, escalera de mapas, cliente) y estadísticas.
- Novedades, leídas de `noticias.md`.

## Novedades

Se escriben en `deploy/all-in-one/noticias.md` (formato explicado arriba del archivo). La web lo relee sola
cuando cambia; no hace falta reiniciar nada.

## Configuración (variables de entorno de `openmu-startup`)

| Variable | Ejemplo | Para qué |
| --- | --- | --- |
| `Portal__Urls` | `http://+:8090` | Prende el portal. Vacío = apagado. |
| `Portal__InviteCodes` | `asado2026,lorencia` | Códigos válidos, separados por coma. Vacío = registro cerrado. |
| `Portal__ClientDownloadUrl` | `https://...` | Link de descarga del cliente (opcional). |

## Pasos en la PC del server

1. Copiar `Caddyfile`, `docker-compose.portal.yml` y `noticias.md` a `deploy/all-in-one/`.
2. En `deploy/all-in-one/.env` agregar `PORTAL_INVITE_CODES=...` (no se sube a GitHub).
3. Liberar el puerto 80: en `docker-compose.yml`, el servicio `nginx-80` pasa de `127.0.0.1:80:80` a `127.0.0.1:8088:80`.
4. En el router, redirigir TCP 80 y 443 a 192.168.0.50.
5. Construir la imagen y levantar con los tres archivos de compose
   (`docker compose -f docker-compose.yml -f docker-compose.override.yml -f docker-compose.portal.yml up -d`).

Si Telecentro bloquea el 80, Caddy igual puede validar el certificado por el 443.
