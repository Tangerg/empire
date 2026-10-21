namespace Empire.Core.Progression;

public interface IRankProgressionPolicy
{
    int RankFor(double progress);
    double? NextThreshold(int rank);
}

public sealed class ThresholdRankProgressionPolicy : IRankProgressionPolicy
{
    public double VeteranThreshold { get; }
    public double EliteThreshold { get; }

    public ThresholdRankProgressionPolicy(double veteranThreshold = 120, double eliteThreshold = 320)
    {
        if (veteranThreshold <= 0 || eliteThreshold <= veteranThreshold)
            throw new DomainInvariantException("rank thresholds must be positive and strictly increasing");
        VeteranThreshold = veteranThreshold;
        EliteThreshold = eliteThreshold;
    }

    public int RankFor(double progress) => progress >= EliteThreshold ? 2 : progress >= VeteranThreshold ? 1 : 0;
    public double? NextThreshold(int rank) => rank switch { 0 => VeteranThreshold, 1 => EliteThreshold, _ => null };
}
