namespace RcCar.Core.Models;

/// <summary>The three application-level bytes learned from a pairing reply.</summary>
public readonly record struct CarIdentity(byte First, byte Second, byte Third)
{
    public override string ToString() => $"{First:X2} {Second:X2} {Third:X2}";

    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 3)
        {
            throw new ArgumentException("An identity needs three bytes.", nameof(destination));
        }

        destination[0] = First;
        destination[1] = Second;
        destination[2] = Third;
    }
}
