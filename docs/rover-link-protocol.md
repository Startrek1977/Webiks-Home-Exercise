# RL-100 Telemetry Link — Integration Notes

**Vendor:** RoverLink Systems Ltd.
**SDK version in use:** 1.4.2 (`lib/RoverLink.Telemetry.dll`)
**Last reviewed:** March 2019

These notes were copied out of the RL-100 Integration Manual (revision D) when we
first wired the station up. The manual itself is in the site cabinet if you need
the parts about antenna placement.

---

## Transport

Both directions are plain UDP on the site network. The base station relays
whatever arrives on the command port to the addressed vehicle.

| Direction | Default port | Frame size |
|---|---|---|
| Rover → station (telemetry) | 14550 | 37 bytes |
| Station → rover (commands) | 14551 | 10 bytes |

Telemetry is advertised as 10 Hz. In practice the base station we have is
configured lower than that; check the actual rate before you rely on it for
anything time sensitive.

All multi-byte fields are little endian.

---

## Telemetry frame (37 bytes)

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0 | 2 | Marker | ASCII `R`, `L` (0x52 0x4C) |
| 2 | 1 | Protocol version | Currently 1 |
| 3 | 1 | Rover id | |
| 4 | 4 | Sequence | uint32, wraps |
| 8 | 8 | Timestamp | uint64, milliseconds since the unix epoch, UTC |
| 16 | 4 | Latitude | int32, degrees × 10⁷ |
| 20 | 4 | Longitude | int32, degrees × 10⁷ |
| 24 | 2 | Heading | uint16, degrees × 10 (0–3599) |
| 26 | 2 | Ground speed | uint16, **centimetres per second** |
| 28 | 2 | Battery | uint16, millivolts |
| 30 | 1 | Signal quality | 0–100 |
| 31 | 2 | Motor temperature | int16, °C × 10 |
| 33 | 2 | Tilt | int16, degrees × 10 |
| 35 | 1 | Status flags | see below |
| 36 | 1 | Checksum | CRC-8 over bytes 0–35 |

### Status flags (offset 35)

| Bit | Meaning |
|---|---|
| 0 | Armed |
| 1 | Emergency stop asserted |
| 2 | On charge |
| 3 | GPS fix valid |

> When the receiver has not acquired a fix, bit 3 is clear **and the latitude and
> longitude fields are zeroed**. Frames with no fix are still transmitted so the
> link stays alive — the position in them is not a position.

---

## Command frame (10 bytes)

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0 | 2 | Marker | ASCII `R`, `C` (0x52 0x43) |
| 2 | 1 | Protocol version | Currently 1 |
| 3 | 1 | Rover id | |
| 4 | 2 | Throttle | int16, −1000 to 1000 |
| 6 | 2 | Steering | int16, −1000 to 1000 |
| 8 | 1 | Flags | bit 0 emergency stop, bit 1 arm |
| 9 | 1 | Checksum | CRC-8 over bytes 0–8 |

The controller clamps throttle and steering to ±1000. Values outside that range
are not rejected, they are clamped.

---

## Checksum

CRC-8, polynomial `0x07`, initial value `0x00`, no final XOR, most significant
bit first. The same routine covers both frame types.

```
crc = 0
for each byte b:
    crc = crc XOR b
    repeat 8 times:
        if crc AND 0x80: crc = (crc << 1) XOR 0x07
        else:            crc = crc << 1
    keep crc to 8 bits
```

Worked example: the CRC of the three bytes `01 02 03` is `0x48`.

---

## Codec

The vendor shipped `RoverLink.Telemetry.dll` as a 32-bit-only binary and then
stopped trading, so a 64-bit process could not load it. It has been replaced by
a managed reimplementation of the above, written against this document and the
simulator's `FrameWriter`, in `RoverRally.Core.Telemetry`:

- `FrameCodec.TryDecode(byte[] buffer, int length, out TelemetryFrame frame)`
- `FrameCodec.EncodeCommand(byte roverId, short throttle, short steering, bool emergencyStop, bool armed)`
- `Crc8.Compute(byte[] buffer, int offset, int count)`

The signatures are unchanged from the vendor's, so calling code did not move.
This page is now the specification rather than a summary of one — if the wire
format is ever revised, change it here and in the codec together.

---

## Firmware behaviour worth knowing

- A vehicle acts on the command frame it most recently received. It does not
  remember an earlier one, so the station has to keep sending while a control
  input is held.
- Sequence numbers are per transmission, not per vehicle. Two rovers reporting
  in the same cycle carry the same sequence number.
- The pack voltage sense on the older RR-4 chassis intermittently fails to read
  and reports zero millivolts for that frame. RoverLink acknowledged this and
  never shipped a fix.
