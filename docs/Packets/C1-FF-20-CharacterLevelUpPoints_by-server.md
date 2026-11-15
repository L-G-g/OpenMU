# C1 FF 20 - CharacterLevelUpPoints (by server)

## Is sent when

The character has more level up points than the original season 6 client can know (65535), and the points were sent to the client or changed by adding stat points. This packet is not part of the original protocol; it's understood by the client mod points.dll of the PeroniaMU season 6 client. The original client ignores it.

## Causes the following actions on the client side

The client keeps up to 65535 points itself and shows the real amount in the character information window.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   8   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xFF  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x20  | Packet header - sub packet type identifier |
| 4 | 4 | IntegerBigEndian |  | LevelUpPoints; Gets or sets the free level up points of the character. |