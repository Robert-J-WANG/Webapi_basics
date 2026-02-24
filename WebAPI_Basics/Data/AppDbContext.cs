using Microsoft.EntityFrameworkCore;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options):DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}