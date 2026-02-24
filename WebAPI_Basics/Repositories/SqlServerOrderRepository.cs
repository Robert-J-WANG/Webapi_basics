// using Microsoft.Data.SqlClient;
// using WebAPI_Basics.Data;
// using WebAPI_Basics.Domain;
//
// namespace WebAPI_Basics.Repositories;
//
// public class SqlServerOrderRepository(AppDbContext db) : IOrderRepository
// {
//     public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
//     {
//         cancellationToken.ThrowIfCancellationRequested();
//         await db.EnsureInitializedAsync(cancellationToken);
//
//         var orders = new List<Order>();
//
//         const string sql = """
//                            SELECT Id, Amount, Status
//                            FROM dbo.Orders
//                            """;
//
//         await using var conn = db.CreateConnection();
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
//         await db.EnsureInitializedAsync(cancellationToken);
//
//         const string sql = """
//                            SELECT Id, Amount, Status
//                            FROM dbo.Orders
//                            WHERE Id = @id
//                            """;
//
//         await using var conn = db.CreateConnection();
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
//         await db.EnsureInitializedAsync(cancellationToken);
//
//         const string sql = """
//                            INSERT INTO dbo.Orders (Amount, Status)
//                            OUTPUT INSERTED.Id, INSERTED.Amount, INSERTED.Status
//                            VALUES (@amount, @status)
//                            """;
//
//         await using var conn = db.CreateConnection();
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
//         await db.EnsureInitializedAsync(cancellationToken);
//
//         const string sql = """
//                            UPDATE dbo.Orders
//                            SET Status = @status
//                            WHERE Id = @id
//                            """;
//
//         await using var conn = db.CreateConnection();
//         await conn.OpenAsync(cancellationToken);
//
//         await using var cmd = new SqlCommand(sql, conn);
//         cmd.Parameters.AddWithValue("@status", status);
//         cmd.Parameters.AddWithValue("@id", id);
//
//         await cmd.ExecuteNonQueryAsync(cancellationToken);
//         // 保持和 InMemory 版一致：找不到不抛异常
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
// }