// using Microsoft.Data.SqlClient;
//
// namespace WebAPI_Basics.Data;
//
// public class AppDbContext
// {
//     private readonly string _connectionString;
//
//     private static bool _initialized;
//     private static readonly SemaphoreSlim InitLock = new(1, 1);
//
//     public AppDbContext(string connectionString)
//     {
//         _connectionString = connectionString;
//     }
//
//     public SqlConnection CreateConnection()
//     {
//         return new SqlConnection(_connectionString);
//     }
//
//     /// <summary>
//     /// 确保数据库中的 Orders 表存在，并在空表时插入初始数据。
//     /// </summary>
//     public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
//     {
//         if (_initialized) return;
//
//         await InitLock.WaitAsync(cancellationToken);
//         try
//         {
//             if (_initialized) return;
//
//             await using var conn = CreateConnection();
//             await conn.OpenAsync(cancellationToken);
//
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