using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game;

public static class KnockbackImmunity
{
    public const ushort ArmsLength = 1209;
    public const ushort Surecast = 160;

    public static bool Active(SimCharacter character)
        => character.HasStatus(ArmsLength) || character.HasStatus(Surecast);
}
