# C1 FB - CharacterResetCount (by server)

## Is sent when

The character information was sent to the client, e.g. when entering the game or after a reset. This packet is not part of the original protocol; it's understood by the client mod resets.dll of the PeroniaMU season 6 client. The original client ignores it.

## Causes the following actions on the client side

The reset count is shown next to the level in the character information window.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   7   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xFB  | Packet header - packet type identifier |
| 3 | 4 | IntegerBigEndian |  | Resets; Gets or sets the number of resets of the character. |