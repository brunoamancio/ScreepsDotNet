namespace ScreepsDotNet.Backend.Core.Services;

public interface IBadgeSvgGenerator
{
    string GenerateSvg(object? badgeData, bool includeBorder);
}
