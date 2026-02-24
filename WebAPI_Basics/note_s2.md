## WEB API (Controller) 进阶

引入 EF Core（SQL Server + Docker）

从内存数据到数据库持久化

#### 1. 要解决什么问题？

第一阶段的 `Order API` 已经有了完整骨架（分层、模型、异常、DI、async、Query 等），但数据仍然存在内存里：

- 程序一重启，订单就没了
- 无法真正积累数据
- 不像真实项目

这一章要解决的是：

> 把项目从“内存数据”升级到“数据库环境已接入”的状态，为后续把 Repository 切到数据库做好准备。

注意这一章的目标是 **先把数据库接入能力准备好**，不是一次性把所有数据访问代码都改完。

#### 2. EF Core 在这里是干什么的？

EF Core 是 .NET 里常用的 ORM（对象关系映射），可以用 C# 对象和 LINQ 操作关系型数据库，而不用一开始就手写大量 SQL。

- 支持多种数据库提供程序（providers），包括 SQL Server；

- SQL Server Provider 同样适用于 Azure SQL。

这章先只记住一句话：

> EF Core 是我们项目连接数据库、读写数据库的主要工具。

#### 3. 如何操作？

顺序很重要，按这个来就不会乱：

1. 启动 SQL Server Docker 容器
2. 给项目安装 EF Core 包
3. 在 `appsettings.json` 配连接字符串
4. 创建 `AppDbContext`
5. 在 `Program.cs` 注册 `DbContext`
6. 先跑通项目（还不改 Repository）

#### 4. 实现步骤

1. 启动 SQL Server（Docker）

    - 先拉取镜像（SQL Server 2022）

        ```bash
        docker pull mcr.microsoft.com/mssql/server:2022-latest
        ```

    - 启动容器（开发环境示例）

        ```bash
        docker run -e "ACCEPT_EULA=Y" \
          -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
          -e "MSSQL_PID=Developer" \
          -p 1433:1433 \
          --name webapi-sqlserver \
          -d mcr.microsoft.com/mssql/server:2022-latest
        ```

        参数解释:

        - `ACCEPT_EULA=Y`：接受协议（必须）
        - `MSSQL_SA_PASSWORD`：`sa` 账号密码（自己设一个强密码）
        - `MSSQL_PID=Developer`：开发版
        - `-p 1433:1433`：把宿主机端口映射到容器里的 SQL Server 端口
        - `--name webapi-sqlserver`：容器名字，后面好管理

2. 确认容器是否启动成功

    ```bash
    docker ps
    ```

    应该能看到 `webapi-sqlserver` 在运行，端口里有 `1433->1433` 映射。

    如果启动失败，先看日志：

    ```bash
    docker logs webapi-sqlserver
    ```

3. 给项目安装 EF Core（SQL Server）包

    这一章我们只装这几个最常用、够用的包。

    在项目目录执行：

    ```bash
    dotnet add package Microsoft.EntityFrameworkCore.SqlServer
    dotnet add package Microsoft.EntityFrameworkCore.Design
    ```

    **为什么装这两个？**

    - `Microsoft.EntityFrameworkCore.SqlServer`：SQL Server Provider（让 EF Core 能连 SQL Server）([Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/?utm_source=chatgpt.com))
    - `Microsoft.EntityFrameworkCore.Design`：后面做 Migration 会用到（这章先装好，避免后面再回头补）

    > 这一章先不装太多包，先保持最小可用。

4. 在 `appsettings.json` 添加连接字符串

    在 `appsettings.json` 里加入（或补充）`ConnectionStrings`：

    ```c#
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=localhost,1433;Database=WebApiBasicsDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
      }
    }
    ```

    **连接字符串先这样写的原因?**

    这一章目标是先跑通本地开发连接，所以先用最直接的方式：

    - `Server=localhost,1433`
    - `Database=WebApiBasicsDb`（数据库名先定好，后面会用到）
    - `sa` 登录
    - `TrustServerCertificate=True;Encrypt=False`（本地开发环境简化连接）

    后面配置章节再系统整理配置管理方式。

5. 创建 `AppDbContext`

    创建文件：`Data/AppDbContext.cs`（文件夹名先用 `Data/` 更直观）

    ```c#
    using Microsoft.EntityFrameworkCore;
    using WebAPI_Basics.Domain;
    
    namespace WebAPI_Basics.Data;
    
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    
        public DbSet<Order> Orders => Set<Order>();
    }
    ```

    这里先讲清楚两个概念（本章会用到的）

    - `DbContext`

        它可以理解成 EF Core 和数据库交互的入口对象。

        以后查询、保存、迁移配置都围绕它展开。

    - `DbSet<Order>`

        表示“订单这张数据集合”。

        现在可以先把它理解成“订单表的入口”。

6. 在 `Program.cs` 注册 `AppDbContext`

    顶部补 `using`：

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using WebAPI_Basics.Data;
    ```

    然后在服务注册区域加入 `DbContext` 注册：

    ```csharp
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    ```

    这一步把：

    - 配置里的连接字符串
    - EF Core SQL Server Provider
    - `AppDbContext`

    连起来了。

#### 5. 如何验证？

这一章先做两个级别的验证。

**验证 1：项目能正常启动**

运行项目，没有因为 `DbContext` 注册报错。

如果报连接字符串为空、`UseSqlServer` 找不到等问题，通常是：

- NuGet 包没装好
- `using Microsoft.EntityFrameworkCore;` 漏了
- `appsettings.json` 键名不一致（`DefaultConnection` 拼错）

**验证 2：SQL Server 容器确实在运行**

```bash
docker ps
```

确认容器还活着，端口映射正常。

> 这一章暂时还没创建表，所以还看不到 `Orders` 表，这是正常的。

#### 6. 本章小结

这一章先把数据库环境接入和 EF Core 基础设施准备好：

- SQL Server（Docker）已启动
- EF Core SQL Server 包已安装
- 连接字符串已配置
- `AppDbContext` 已创建
- `Program.cs` 已注册 `DbContext`

这一步完成后，项目就从“纯内存 API”进入了“可接数据库”的状态。

**新的问题来了?**

现在项目已经能连接数据库了，但数据库里还没有订单表，`Order` 模型和数据库结构之间的关系也还没落地。

下一章就解决这个问题：

**实体与 `DbContext`：让 `Order` 落到数据库表。**



