using Assets.Scripts.Objects;

namespace DeadlyElectricity.Extensions
{
    internal static class ThingExtensions
    {
        internal static void CatchFire(this Thing thing)
        {
            thing.IsBurning = true;
            thing.OnFireStart();
        }
    }
}
