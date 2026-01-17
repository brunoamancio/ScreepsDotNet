namespace ScreepsDotNet.Common.Utilities;

/// <summary>
/// Shared helpers for decoding Screeps terrain characters into bitmask values.
/// </summary>
public static class TerrainEncoding
{
    public static int Decode(char value)
    {
        TryDecode(value, out var mask);
        return mask;
    }

    public static bool TryDecode(char value, out int mask)
    {
        if (value is >= '0' and <= '9')
        {
            mask = value - '0';
            return true;
        }

        if (value is >= 'a' and <= 'z')
        {
            mask = 10 + (value - 'a');
            return true;
        }

        if (value is >= 'A' and <= 'Z')
        {
            mask = 10 + (value - 'A');
            return true;
        }

        mask = 0;
        return false;
    }
}
