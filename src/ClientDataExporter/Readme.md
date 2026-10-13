# Client Data Exporter

This tool reads the data tables (`*.bmd`) of an original game client and exports
them into CSV files, so the values of the original client can be compared with
the configuration of OpenMU, or taken over into it.

Currently, it exports the item definitions of `Data\Local\item.bmd` in the
layout of Season 6 Episode 3 (client 1.04d). Other client versions use other
record layouts; the tool reports the record size it found instead of guessing.

## Usage

This is a command line tool. The first parameter is the folder of the game
client (or the path of the `item.bmd` itself), the optional second parameter is
the output folder. If a path contains spaces, wrap it by quotes.

Example:
```MUnique.OpenMU.ClientDataExporter.exe "D:\MU Client 1.04d - Season 6E3" "D:\client-data\s6e3"```

The output folder then contains `items.csv` with one row per defined item.

The file checksum is verified before anything is exported, so a damaged or
modified file is reported instead of producing wrong values.

## Notes

The exported values belong to the game client. Keep the exported files out of
this repository.
