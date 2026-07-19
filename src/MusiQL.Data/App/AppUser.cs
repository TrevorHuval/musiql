using Microsoft.AspNetCore.Identity;

namespace MusiQL.Data.App;

public class AppUser : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; }
}
