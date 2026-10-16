# C1 FA - ObjectHealthStatus (by server)

## Is sent when

The health of an observed non-player object (e.g. a monster) changed by a hit, or a damaged object came into view. This packet is not part of the original protocol; it's understood by the client mod camera3d.dll of the PeroniaMU season 6 client. The original client ignores it.

## Causes the following actions on the client side

A health bar with the remaining health is shown above the object.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   6   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xFA  | Packet header - packet type identifier |
| 3 | 2 | ShortBigEndian |  | ObjectId |
| 5 | 1 | Byte |  | HealthStatus; Gets or sets the status of the remaining health in fractions of 1/250. |