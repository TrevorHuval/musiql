using Microsoft.EntityFrameworkCore;

namespace MusiQL.Data;

public class MusiQLDbContext(DbContextOptions<MusiQLDbContext> options) : DbContext(options)
{
}
