namespace ScreepsDotNet.Common.Extensions;

using ScreepsDotNet.Common.Types;

public static class DirectionExtensions
{
    public static bool TryParseDirection(int value, out Direction direction)
    {
        if (value is >= 1 and <= 8) {
            direction = (Direction)value;
            return true;
        }

        direction = default;
        return false;
    }

    public static int ToInt(this Direction direction)
        => (int)direction;
}
