using Microsoft.AspNetCore.Identity;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Identity;

/// <summary>Extends ASP.NET Identity with profile data and links to the user's travel data.</summary>
public sealed class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserPreference> Preferences { get; set; } = new List<UserPreference>();
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    public ICollection<AiConversation> AiConversations { get; set; } = new List<AiConversation>();
}
