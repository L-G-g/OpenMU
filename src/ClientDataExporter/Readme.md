# Client Data Exporter

This tool reads the data tables (`*.bmd`) of an original game client and exports
them into CSV files, so the values of the original client can be compared with
the configuration of OpenMU, or taken over into it.

Currently, it exports the item definitions in the layout of Season 6 Episode 3
(client 1.04d). Other client versions use other record layouts; the tool reports
the record size it found instead of guessing.

The client reads its items from its language folder, e.g.
`Data\Local\Eng\item_eng.bmd`. The other item files of that client are
leftovers: `Data\Local\item.bmd` is the one of the Japanese client with fewer
items, and `Data\Local\Eng\item.bmd` is outdated as well.

## Usage

This is a command line tool with these parameters:

1. The folder of the game client, or the path of an item file.
2. Optional: the output folder. Default: `client-data` in the current folder.
3. Optional: the language folder of the client, e.g. `Eng` (default), `Spn` or
   `Por`. The item names are taken from it.

If a path contains spaces, wrap it by quotes.

Example:
```MUnique.OpenMU.ClientDataExporter.exe "D:\MU Client 1.04d - Season 6E3" "D:\client-data\s6e3" Spn```

The output folder then contains `items.csv` with one row per defined item.

Staffs and sticks keep their durability in the `MagicDurability` column, not in
`Durability`.

The file checksum is verified before anything is exported, so a damaged or
modified file is reported instead of producing wrong values.

## Notes

The exported values belong to the game client. Keep the exported files out of
this repository.
