using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace WebAPI_Basics.Data;

public class AppDbContext:DbContext
{
   
   public AppDbContext(DbContextOptions<AppDbContext> options):base(options){}
   
   //
  
}