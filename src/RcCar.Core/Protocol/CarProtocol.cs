using RcCar.Core.Models;

namespace RcCar.Core.Protocol;

/// <summary>
/// Encodes the Standard-Car protocol decoded from the Android app.
/// Pairing and control use BLE advertisements, without a GATT connection.
/// </summary>
/// <remarks>
/// These 19-byte payloads sit inside a manufacturer-data section; the BLE company
/// ID is supplied separately to Windows. Byte 0 is the protocol marker, byte 1
/// is the opcode, bytes 2-4 identify the vehicle, and bytes 5-7 identify this client.
/// Control fields occupy bytes 8-17; byte 18 is the outgoing XOR checksum.
/// </remarks>
public sealed class CarProtocol(CarIdentity clientIdentity)
{
    public const ushort CompanyId = 0;
    public const int PayloadLength = 19;
    public CarIdentity ClientIdentity { get; } = clientIdentity;

    public byte[] PairingRequest()
    {
        var payload = NewPacket(0x06);
        SetChecksum(payload);
        return payload;
    }

    public byte[] Control(CarIdentity vehicle, DriveState state, byte counter)
    {
        if (counter is < 1 or > 250)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }

        var payload = NewPacket(0x07);
        vehicle.CopyTo(payload.AsSpan(2, 3));
        // Throttle and steering occupy different bits, so they can be combined.
        var throttleFlags = state.Throttle switch
        {
            ThrottleDirection.Forward => 1,
            ThrottleDirection.Reverse => 2,
            _ => 0
        };
        var steeringFlags = state.Steering switch
        {
            SteeringDirection.Left => 4,
            SteeringDirection.Right => 8,
            _ => 0
        };

        payload[8] = (byte)(throttleFlags | steeringFlags);
        payload[9] = state.Speed;
        // Bit 0 is the extra forward flag. Bit 2 is the tested light-on flag.
        payload[10] = (byte)(
            (state.Throttle == ThrottleDirection.Forward ? 0x01 : 0) |
            (state.LightsOn ? 0x04 : 0));
        payload[11] = 0x64;
        payload[14] = counter;

        SetChecksum(payload);
        return payload;
    }

    public bool TryReadPairingReply(Advertisement advertisement, out CarIdentity identity)
    {
        identity = default;
        var payload = advertisement.Payload;
        // Company/length checking is deliberately stricter than the original Android callback.
        if (advertisement.CompanyId != CompanyId || payload.Length != PayloadLength ||
            payload[0] != 0x04 || payload[1] != 0x0A ||
            payload[5] != ClientIdentity.First ||
            payload[6] != ClientIdentity.Second ||
            payload[7] != ClientIdentity.Third)
        {
            return false;
        }

        identity = new CarIdentity(payload[2], payload[3], payload[4]);
        // The APK does not validate the reply checksum. Do not invent that requirement.
        return true;
    }

    private byte[] NewPacket(byte opcode)
    {
        // A pairing request leaves the vehicle identity zero so a car can reply
        // with its identity. Unassigned fields retain the captured zero values.
        var payload = new byte[PayloadLength];
        payload[0] = 0x04;
        payload[1] = opcode;
        ClientIdentity.CopyTo(payload.AsSpan(5, 3));
        return payload;
    }

    private static void SetChecksum(byte[] payload)
    {
        // Choose the last byte so XOR across the complete packet equals 0xE9.
        byte checksum = 0xE9;
        for (var i = 0; i < PayloadLength - 1; i++)
        {
            checksum ^= payload[i];
        }

        payload[^1] = checksum;
    }

    public static string Hex(byte[] payload) => string.Join(" ", payload.Select(value => value.ToString("X2")));
}
