namespace SkyMate.Domain.Enums;

/// <summary>Categories available for a planned trip activity.</summary>
public enum ActivityType
{
    /// <summary>A visit to a landmark or tourist attraction.</summary>
    Sightseeing,
    /// <summary>A meal or other food-related activity.</summary>
    Food,
    /// <summary>A shopping activity.</summary>
    Shopping,
    /// <summary>An outdoor or nature-related activity.</summary>
    Nature,
    /// <summary>A cultural activity.</summary>
    Culture,
    /// <summary>An adventure activity.</summary>
    Adventure,
    /// <summary>Transportation between locations.</summary>
    Transport,
    /// <summary>Accommodation during the trip.</summary>
    Accommodation,
    /// <summary>An activity that does not fit another category.</summary>
    Other
}
