namespace SkyMate.Domain.Enums;

/// <summary>Defines how an activity's cost is calculated.</summary>
public enum CostBasis
{
    /// <summary>The cost is per person.</summary>
    PerPerson,
    /// <summary>The cost is for the whole group.</summary>
    PerGroup,
    /// <summary>The activity is free.</summary>
    Free
}
