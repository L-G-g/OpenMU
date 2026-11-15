# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Delegated (plan approved by Gabi on 2026-11-07): React + Vite + TypeScript + Tailwind in `web/portal`, served by a C# ASP.NET Core host (`src/Web/Portal`) inside the OpenMU all-in-one process, built in the Docker image. Public behind Caddy (HTTPS) at `peroniamu.duckdns.org`.

## Users

- Gabi's friends, mostly in Argentina, Spanish speakers (rioplatense "vos"). They play PeroniaMU, a private Mu Online Season 6 server, on Windows PCs, and will open the web from their phone or PC, often while the game is open or between sessions.
- Gabi, the owner and only admin. He hands out invite codes and WC from the admin panel (which stays private, not part of this web).

## Product Purpose

PeroniaMU's public web. Phase 1: a friend gets an invite code, creates a game account, logs in, sees their characters (level, resets, class, map), changes the password, checks rankings and who is online, and downloads the client. Later phases add a player-to-player item market paid in WC (WCoin C), similar in function to warframe.market: sell and buy orders, browse all listings, filter and rank, trades settle automatically (item moves to the buyer's market depot, WC to the seller).

## Positioning

A small private server among friends, not a commercial MU server. It runs on Gabi's home PC. The web has no shop, no donations, no paid WC: WC only come from Gabi's grants (decided 2026-11-07).

## Operating Context

- Game: OpenMU fork (L-G-g/OpenMU), Season 6 rules with Peronia changes: reset system (level 300 to 400 ramp), custom map ladder, dungeons, spectres, flying Dinorant, 3D camera and custom client DLLs.
- Classes: Dark Wizard, Dark Knight, Fairy Elf, Magic Gladiator, Dark Lord, Summoner, Rage Fighter (and their evolutions), all creatable from start.
- Accounts are created through OpenMU's own account API; passwords hashed like the game.
- Registration requires an invite code (friends only).

## Capabilities and Constraints

- Phase 1 screens: home, sign up (invite code), log in, my account (characters, password change), rankings (characters by resets then level; guilds), online now, download client.
- Must work well at 390 px wide (phone) and 1440 px (desktop).
- Server is a home PC with little free RAM: no heavy server-side rendering, small static bundle.
- Undecided: news/announcements section; client download host (currently a private GitHub release, friends may need another route).

## Brand Commitments

- Name: PeroniaMU (server name in game and in the client).
- Gabi's explicit visual brief (2026-11-07): "algo moderno con tonos oscuros". Do NOT follow the screen structure or look of open-mu-cms.
- Spanish (Argentina) copy, informal "vos".

## Evidence on Hand

- Item images by group/number/level/excellent: `src/Web/ItemEditor/wwwroot/img/items/`.
- No logo, no key art, no screenshots committed yet. Do not invent player counts, reviews, or uptime claims.

## Product Principles

1. Friends first: zero friction for someone Gabi invited, closed to everyone else.
2. Show the real game state (characters, resets, rankings) straight from the server, never cached fiction.
3. Safe by construction: the web never edits what the game server holds in memory.
4. Fast on a phone over mobile data.
