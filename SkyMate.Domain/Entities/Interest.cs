namespace SkyMate.Domain.Entities;

/// <summary>Represents an interest that can be shared by multiple trips.</summary>
public sealed class Interest
{
    private string _name = string.Empty;

    public int Id { get; set; }

    /// <summary>The interest name, trimmed when a value is assigned.</summary>
    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }

    public ICollection<TripInterest> TripInterests { get; set; } = new List<TripInterest>();
}
