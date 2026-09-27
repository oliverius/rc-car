namespace RcCar.Core.Models;

/// <summary>One received BLE manufacturer-data section. Payload excludes the company ID; RSSI is signal strength in dBm.</summary>
public sealed record Advertisement(ushort CompanyId, byte[] Payload, ulong Address, short Rssi);

/// <summary>A pairing reply associates a protocol identity with its observed Bluetooth address and signal strength.</summary>
public sealed record CarCandidate(CarIdentity Identity, ulong Address, short Rssi)
{
    public string DisplayName => $"{Identity}   ·   {Address:X12}   ·   {Rssi} dBm";
}

/// <summary>Windows adapter capabilities. These flags alone do not guarantee that advertising will start.</summary>
public sealed record AdapterInformation(
    string Name,
    string Address,
    bool LowEnergySupported,
    bool PeripheralRoleSupported,
    bool AdvertisementOffloadSupported,
    string RadioState);
