# Minimap spots

Adds the monster spots of the server (see `MonsterSpotGrouping` in the season 6 initialization) as markers
to the map which the Season 6 client opens with TAB. Each marker shows the name of the monster on mouse over,
and "(Hotspot)" for a spot with 7 monsters.

The client reads the markers of a map from `Data\Local\Eng\Minimap\Minimap_World<map + 1>_eng.bmd`:
100 entries of 116 bytes (kind, x, y, rotation, name), each XOR-ed with `FC CF AB`, 45 more bytes and a
checksum (key `0x2BC1`). Kind 1 is drawn as an NPC, kind 2 as a gate. Because of the limit of 100 markers,
the nearest spots of the same monster are joined into one marker ("Bull Fighter x2") when a map has more.

1. Export the spots of the live database, one line per spot: `map;x1;y1;quantity;monster name`.
2. `dotnet run MinimapSpots.cs -- spots.csv <original Minimap folder> <output folder>`
3. Copy the output files into the `Data\Local\Eng\Minimap` folder of the client.

Always start from the original files of the client, so that running it again doesn't add the spots twice.
The client files themselves are not part of this repository.
