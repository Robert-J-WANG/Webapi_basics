// using Microsoft.Data.SqlClient;
// using WebAPI_Basics.Domain;
//
// namespace WebAPI_Basics.Repositories;
//
// public class SqlServerOrderRepository : IOrderRepository
// {
//     private const string ConnectionString =
//         "Server=localhost,1433;Database=master;User Id=sa;Password=MYpwd123!;TrustServerCertificate=True;Encrypt=False";
//
//     // 简单进程内保护，避免同一次应用运行中重复初始化
//     private static bool _initialized;
//     private static readonly SemaphoreSlim InitLock = new(1, 1);
//
//     public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
//     {
//         cancellationToken.ThrowIfCancellationRequested();
//         await EnsureInitializedAsync(cancellationToken);
//
//         var orders = new List<Order>();
//
//         const string sql = """
//                            SELECT Id, Amount, Status
//                            FROM Orders
//                            """;
//
//         await using var conn = new SqlConnection(ConnectionString);
//         await conn.OpenAsync(cancellationToken);
//
//         await using var cmd = new SqlCommand(sql, conn);
//         await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
//
//         while (await reader.ReadAsync(cancellationToken))
//         {
//             orders.Add(MapOrder(reader));
//         }
//
//         return orders;
//     }
//
//     public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
//     {
//         cancellationToken.ThrowIfCancellationRequested();
//         await EnsureInitializedAsync(cancellationToken);
//
//         const string sql = """
//                            SELECT Id, Amount, Status
//                            FROM Orders
//                            WHERE Id = @id
//                            """;
//
//         await using var conn = new SqlConnection(ConnectionString);
//         await conn.OpenAsync(cancellationToken);
//
//         await using var cmd = new SqlCommand(sql, conn);
//         cmd.Parameters.AddWithValue("@id", id);
//
//         await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
//
//         if (!await reader.ReadAsync(cancellationToken))
//             return null;
//
//         return MapOrder(reader);
//     }
//
//     public async Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken)
//     {
//         cancellationToken.ThrowIfCancellationRequested();
//         await EnsureInitializedAsync(cancellationToken);
//
//         // 保持和 InMemory 版本行为一致：新订单状态 = Created
//         // 假设 Id 是 IDENTITY
//         const string sql = """
//                            INSERT INTO Orders (Amount, Status)
//                            OUTPUT INSERTED.Id, INSERTED.Amount, INSERTED.Status
//                            VALUES (@amount, @status)
//                            """;
//
//         await using var conn = new SqlConnection(ConnectionString);
//         await conn.OpenAsync(cancellationToken);
//
//         await using var cmd = new SqlCommand(sql, conn);
//         cmd.Parameters.AddWithValue("@amount", amount);
//         cmd.Parameters.AddWithValue("@status", "Created");
//
//         await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
//
//         if (!await reader.ReadAsync(cancellationToken))
//             throw new InvalidOperationException("Failed to create order.");
//
//         return MapOrder(reader);
//     }
//
//     public async Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
//     {
//         cancellationToken.ThrowIfCancellationRequested();
//         await EnsureInitializedAsync(cancellationToken);
//
//         // 保持和 InMemory 版本行为一致：找不到不抛异常
//         const string sql = """
//                            UPDATE Orders
//                            SET Status = @status
//                            WHERE Id = @id
//                            """;
//
//         await using var conn = new SqlConnection(ConnectionString);
//         await conn.OpenAsync(cancellationToken);
//
//         await using var cmd = new SqlCommand(sql, conn);
//         cmd.Parameters.AddWithValue("@status", status);
//         cmd.Parameters.AddWithValue("@id", id);
//
//         await cmd.ExecuteNonQueryAsync(cancellationToken);
//     }
//
//     private static Order MapOrder(SqlDataReader reader)
//     {
//         return new Order
//         {
//             Id = reader.GetInt32(reader.GetOrdinal("Id")),
//             Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
//             Status = reader.GetString(reader.GetOrdinal("Status"))
//         };
//     }
//
//     /// <summary>
//     /// 确保 Orders 表存在，并在空表时插入与 InMemory 版本一致的初始数据。
//     /// </summary>
//     private static async Task EnsureInitializedAsync(CancellationToken cancellationToken)
//     {
//         if (_initialized) return;
//
//         await InitLock.WaitAsync(cancellationToken);
//         try
//         {
//             if (_initialized) return;
//
//             await using var conn = new SqlConnection(ConnectionString);
//             await conn.OpenAsync(cancellationToken);
//
//             // 1) 建表（如果不存在）
//             const string createTableSql = """
//                                           IF OBJECT_ID('dbo.Orders', 'U') IS NULL
//                                           BEGIN
//                                               CREATE TABLE dbo.Orders
//                                               (
//                                                   Id INT IDENTITY(1,1) PRIMARY KEY,
//                                                   Amount DECIMAL(18,2) NOT NULL,
//                                                   Status NVARCHAR(20) NOT NULL
//                                               );
//                                           END;
//                                           """;
//
//             await using (var createCmd = new SqlCommand(createTableSql, conn))
//             {
//                 await createCmd.ExecuteNonQueryAsync(cancellationToken);
//             }
//
//             // 2) 如果表为空，插入与 InMemoryOrderRepository 对齐的两条默认数据
//             const string seedSql = """
//                                    IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
//                                    BEGIN
//                                        INSERT INTO dbo.Orders (Amount, Status)
//                                        VALUES (100, N'Created'),
//                                               (200, N'Paid');
//                                    END;
//                                    """;
//
//             await using (var seedCmd = new SqlCommand(seedSql, conn))
//             {
//                 await seedCmd.ExecuteNonQueryAsync(cancellationToken);
//             }
//
//             _initialized = true;
//         }
//         finally
//         {
//             InitLock.Release();
//         }
//     }
// }