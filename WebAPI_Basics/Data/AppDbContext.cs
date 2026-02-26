using Microsoft.EntityFrameworkCore;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Order> Orders => Set<Order>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            // ===== 1) 映射规则（Model / Mapping） =====

            // 映射到数据库表名 Orders
            entity.ToTable("Orders");

            // 主键
            entity.HasKey(x => x.Id);

            // Id 由数据库生成（Identity）
            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            // Amount: decimal(18,2), 必填
            entity.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            // Status: nvarchar(20), 必填
            entity.Property(x => x.Status)
                .HasMaxLength(20)
                .IsRequired();

            // ===== 2) 种子数据配置（Seed Data Configuration） =====
            // 注意：这是“配置”，不是立刻插入数据库。
            // 真正插入发生在 migration + database update 执行时。

            entity.HasData(
                new Order
                {
                    Id = 1,
                    Amount = 100m,
                    Status = "Created"
                },
                new Order
                {
                    Id = 2,
                    Amount = 200m,
                    Status = "Paid"
                }
            );
        });
    }
}