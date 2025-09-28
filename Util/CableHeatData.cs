using Assets.Scripts.GridSystem;

namespace DeadlyElectricity.Util
{
    internal struct CableHeatData(WorldGrid worldGrid, double heatEnergy)
    {
        public WorldGrid worldGrid = worldGrid;
        public double heatEnergy = heatEnergy;
    }
}
