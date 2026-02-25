# WEB API (Controller) 进阶

## 1. 从内存到数据库

### 1. 解决什么问题？

第一阶段的 `Order API` 已经完成了分层结构（Controller / Service / Repository），接口也能正常工作。

但数据存放在内存里，程序一重启就丢失。但是我们有新的需求：

- 订单数据要持久化
- 程序重启后数据不能丢
- 项目要更接近真实开发

因此，需要解决从内存切到数据库的配置问题。

### 2. 连接数据库

使用SQL Server数据库，配合Docker容器镜像使用

启动 SQL Server（Docker）

```bash
docker pull mcr.microsoft.com/mssql/server:2022-latest
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -e "MSSQL_PID=Developer" \
  -p 1433:1433 \
  --name webapi-sqlserver \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

检查容器是否运行：

```bash
docker ps
```

如果启动失败，查看日志：

```bash
docker logs webapi-sqlserver
```

到这里，数据库环境已经准备好。

### 3. 使用数据库

我们之前的版本，使用`InMemoryOrderRepository`来存取数据（通过内存）。

现在创建一个用数据库的仓库`SqlServerOrderRepository`来实现同样的功能。

~~不要看懂代码细节~~

```csharp
using Microsoft.Data.SqlClient;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Repositories;

public class SqlServerOrderRepository : IOrderRepository
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=orderDB;User Id=sa;Password=Mxxxxx12345!;TrustServerCertificate=True;Encrypt=False";

    // 简单进程内保护，避免同一次应用运行中重复初始化
    private static bool _initialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        var orders = new List<Order>();

        const string sql = """
                           SELECT Id, Amount, Status
                           FROM Orders
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            orders.Add(MapOrder(reader));
        }

        return orders;
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        const string sql = """
                           SELECT Id, Amount, Status
                           FROM Orders
                           WHERE Id = @id
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapOrder(reader);
    }

    public async Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        // 保持和 InMemory 版本行为一致：新订单状态 = Created
        // 假设 Id 是 IDENTITY
        const string sql = """
                           INSERT INTO Orders (Amount, Status)
                           OUTPUT INSERTED.Id, INSERTED.Amount, INSERTED.Status
                           VALUES (@amount, @status)
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@status", "Created");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Failed to create order.");

        return MapOrder(reader);
    }

    public async Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        // 保持和 InMemory 版本行为一致：找不到不抛异常
        const string sql = """
                           UPDATE Orders
                           SET Status = @status
                           WHERE Id = @id
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Order MapOrder(SqlDataReader reader)
    {
        return new Order
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            Status = reader.GetString(reader.GetOrdinal("Status"))
        };
    }

    /// <summary>
    /// 确保 Orders 表存在，并在空表时插入与 InMemory 版本一致的初始数据。
    /// </summary>
    private static async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await InitLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync(cancellationToken);

            // 1) 建表（如果不存在）
            const string createTableSql = """
                                          IF OBJECT_ID('dbo.Orders', 'U') IS NULL
                                          BEGIN
                                              CREATE TABLE dbo.Orders
                                              (
                                                  Id INT IDENTITY(1,1) PRIMARY KEY,
                                                  Amount DECIMAL(18,2) NOT NULL,
                                                  Status NVARCHAR(20) NOT NULL
                                              );
                                          END;
                                          """;

            await using (var createCmd = new SqlCommand(createTableSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2) 如果表为空，插入与 InMemoryOrderRepository 对齐的两条默认数据
            const string seedSql = """
                                   IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
                                   BEGIN
                                       INSERT INTO dbo.Orders (Amount, Status)
                                       VALUES (100, N'Created'),
                                              (200, N'Paid');
                                   END;
                                   """;

            await using (var seedCmd = new SqlCommand(seedSql, conn))
            {
                await seedCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }
}
```

在DI容器中注册

```c#
builder.Services.AddScoped<IOrderRepository,SqlServerOrderRepository > ();
```

这样，数据会持久存储在数据库中。

### 4. 直接连接数据库会遇到什么问题？

这种手动连接方式，很快会出现问题：

- 连接字符串到处写，修改时容易漏
- 打开/关闭连接代码重复
- SQL 细节和业务逻辑混在一起
- 后面查询、更新一多，service代码会迅速膨胀

#### **如何优化？**

可以把和数据库操作相关的逻辑抽离出来，封装成一个专用的类 AppDbContext， 类似这样：

```c#
using Microsoft.Data.SqlClient;

namespace WebAPI_Basics.Data;

public class AppDbContext
{
    private readonly string _connectionString;

    private static bool _initialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    /// <summary>
    /// 确保数据库中的 Orders 表存在，并在空表时插入初始数据。
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await InitLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            await using var conn = CreateConnection();
            await conn.OpenAsync(cancellationToken);

            const string createTableSql = """
                                          IF OBJECT_ID('dbo.Orders', 'U') IS NULL
                                          BEGIN
                                              CREATE TABLE dbo.Orders
                                              (
                                                  Id INT IDENTITY(1,1) PRIMARY KEY,
                                                  Amount DECIMAL(18,2) NOT NULL,
                                                  Status NVARCHAR(20) NOT NULL
                                              );
                                          END;
                                          """;

            await using (var createCmd = new SqlCommand(createTableSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string seedSql = """
                                   IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
                                   BEGIN
                                       INSERT INTO dbo.Orders (Amount, Status)
                                       VALUES (100, N'Created'),
                                              (200, N'Paid');
                                   END;
                                   """;

            await using (var seedCmd = new SqlCommand(seedSql, conn))
            {
                await seedCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }
}
```

如果，需求增多时，需要让他实现大量功能：

- 连接管理
- 命令执行
- 查询结果映射到对象
- 参数化查询
- 更新保存
- 模型和表结构关系管理
- 后续数据库结构变更管理
- ...

我们使用时，直接通过DI容器注入使用。

**但是，如何实现这么复杂的逻辑，能完美工作？？需要自己编写？？？**

理论上可以自己写一个 `AppDbContext`，把连接和执行逻辑封装起来。

但有人已经完成了这个工作，不需要自己造轮子，直接可以**继承使用。**



### 5. 使用第三方库 - EF Core？

EF Core 是 .NET 里常用的 ORM（对象关系映射），可以用 C# 对象和 LINQ 操作关系型数据库，而不用一开始就手写大量 SQL。

- 支持多种数据库提供程序（providers），包括 SQL Server；

- SQL Server Provider 同样适用于 Azure SQL。

**EF Core 提供了一个核心基类：**

- `DbContext`

它本质上就是“数据库访问上下文”的成熟实现。

于是当前项目的做法就自然变成：

- 自定义 `AppDbContext`
- 继承 EF Core 的 `DbContext`
- 在此基础上对当前项目自己的数据库访问定义



### 6. 如何操作？

1. #### 安装 EF Core 相关包

    ```bash
    dotnet add package Microsoft.EntityFrameworkCore.SqlServer
    dotnet add package Microsoft.EntityFrameworkCore.Design
    ```

    这两个包先各自承担一个角色：

    - `SqlServer` 包：让 EF Core 能连接 SQL Server
    - `Design` 包：管理数据库结构

2. #### 配置连接字符串

    `appsettings.json`中集中管理，不写死在业务代码里

    ```c#
    // 添加字段
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=localhost,1433;Database=WebApiBasicsDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
      }
    }
    ```

3. #### 优化 `AppDbContext`（继承 `DbContext`）

    优化文件：`Data/AppDbContext.cs`内容

    ```c#
    using Microsoft.EntityFrameworkCore;
    
    namespace WebAPI_Basics.Data;
    
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    
        // 其他逻辑
    }
    ```
    
    暂时只跑通配置
    
    `AppDbContext : DbContext`
    
    我们定义的数据库上下文类继承 **EF Core** 提供的基类 ` DbContext`, 这样，就能使用它的全部功能
    
4. #### 在 `Program.cs` 注册 `AppDbContext`

    先补 `using`：

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using WebAPI_Basics.Data;
    ```

    然后在服务注册区域加入：

    ```csharp
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    ```

以后需要使用 AppDbContext 时，仍然通过 DI 方式。

### 7. 如何验证？

这一章是“数据库基础设施是否接通”，不是“订单表是否已经创建”。

#### **验证 1：SQL Server 容器运行正常**

```bash
docker ps
```

确认容器 `webapi-sqlserver` 正在运行，并且有 `1433:1433` 端口映射。

#### **验证 2：手动连接演示可以成功**

运行前面的手动连接示例代码，确认能输出连接成功信息。

这一步是为了确认：

- SQL Server 环境可用
- 用户名/密码/端口配置正确

#### **验证 3：项目启动不报 `DbContext` 配置错误**

运行项目，确认因为 `AddDbContext` / `UseSqlServer` 相关配置不会报错。

如果报错，优先检查：

- EF Core 包是否安装成功
- `using Microsoft.EntityFrameworkCore;` 是否遗漏
- `DefaultConnection` 名字是否拼错
- SQL Server 容器是否已启动

#### **验证 4：现有接口行为保持不变**

切换回`InMemoryOrderRepository`，当前主线仍然是内存版。

所以以下接口行为应与第一阶段末尾一致：

- `GET /orders`
- `GET /orders/{id}`
- `POST /orders`
- `POST /orders/{id}/pay`

这说明当前改动只是在“接入数据库基础设施”，没有破坏现有业务主线。

### 8. 本章小结

这一章完成了从内存到数据库的第一步过渡：

- 先证明直接连接数据库是可行的
- 再看到直接连接会带来重复和混乱
- 因此需要统一的数据库访问入口
- 使用 EF Core 的 `DbContext` 作为成熟实现
- 定义 `AppDbContext` 并注册到 DI
- 完成数据库基础设施接入

到这里，项目已经具备了“连接数据库的能力”，但还没有真正把订单模型落到数据库表里。

**新的问题来了？**

现在已经有了数据库入口 `AppDbContext`，但还有一个关键问题没解决：

- `Order` 在代码里只是一个类
- 数据库并不知道它该变成什么表、哪些字段怎么约束

因此需要解决：

如何把 `Order` 这类对象关联数据库模型，并明确它和表结构的对应关系。



## 2. 实体关联数据库模型

### 1. 解决什么问题？

第1章已经完成了两件事：

- 项目有了数据库环境（SQL Server + Docker）
- 项目里有了统一数据库入口 `AppDbContext`，并且已经注册到 DI

但是现在还不能说“订单已经进入数据库”了。原因很简单：

- `Order` 目前只是一个 C# 类
- `AppDbContext` 目前只是一个数据库入口类
- 数据库还不知道 `Order` 应该对应什么表、字段怎么存

这一章要解决的问题就是：

**如何把利用 `Order` 类生成数据库模型（表结构），并明确它们之间的对应关系？**

### 2. AppDbContext 里到底要放什么？

我们已经明确了 `AppDbContext`这个类，将作为操作数据库统一入口。

现在如何编写逻辑代码，来实现 `Order` 类（实体entity) 和数据库模型（表结构）的关系？？

目前它只是一个空类：

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
```

它虽然“能连数据库”，但还没有告诉框架：

- `Order` 对应的表名是什么？
- `Id` 是不是主键？
- `Amount` 金额字段用什么精度？
- `Status` 是否允许为空？长度多大？
- 哪些字段不能为空？

------

### 3. 设置模型映射参数

我们的`AppDbContext`类继承与EF core的基类 `DbContext`。

而基类 `DbContext`里定义了创建数据库模型的方法 `OnModelCreating`。

需要重写这个方法，利用我们的实体模型设置相应的参数。

实体模型Order

```c#
public class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

基于实体模型，创建数据库模型

```c#
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 重写OnModelCreating方法
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
```

#### **这段配置在表达什么？**

##### `modelBuilder.Entity<Order>()`

表示：传递 `Order` 这类的实体开始构建在数据库里的规则。

##### `ToTable("Orders")`

表示：`Order` 对应数据库里的哪张表？

##### `HasKey(x => x.Id)`

表示：哪个字段是主键？

##### `HasPrecision(18, 2)`

金额字段通常需要明确精度，这里指定为常见的 `18,2`。

##### `IsRequired().HasMaxLength(20)`

表示：

- 状态 `Status` 能不能空？
- 最长多少？

当前业务状态值（如 `Created`、`Paid`）都比较短，而且业务上不应为空，所以这里明确约束。

**`entity.HasData()`**

添加数据库初始种子数据

### 4. 如何验证？

这一章还没开始创建数据库表，**只是填写了映射的规则**。所以验证重点是：

- 代码里的数据库模型定义是否已经写对
- 项目是否还能正常运行

#### 验证 1：项目编译通过

如果报错，优先检查：

- `using Microsoft.EntityFrameworkCore;` 是否遗漏
- `Order` 命名空间是否正确
- `Amount / Status` 字段名是否和模型一致



#### 验证 2：项目正常启动

项目能启动，说明：

- `AppDbContext` 注册仍然正常
- `OnModelCreating(...)` 中的配置语法正确



#### 验证 3：现有接口行为保持不变

这一章仍然没有切换 Repository 实现，当前主线还是内存版。

所以接口行为应保持不变。

这说明这一章改动范围控制正确：只定义数据库模型规则，不提前改业务主线。



### 5. 本章小结

这一章完成的是“如何利用实例模型，并把关键表结构规则写清楚”：

- `AppDbContext` 不再只是一个“空入口”
- 在 `OnModelCreating(...)` 中明确了表名、主键、金额精度、状态字段约束

到这里，`Order` 和数据库表之间的关系已经在代码里定义好了。

**新的问题来了？**

现在只是把规则写在代码里，数据库里还没有真正创建出 `Orders` 表。

也就是说：

- 模型有了
- 规则有了
- 数据库结构还没落地

因此需要解决：如何把表结构规则真正应用到 SQL Server，并且以后字段变化时还能持续管理？

