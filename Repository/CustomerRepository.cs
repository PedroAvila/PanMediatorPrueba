using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using PanMediatorPrueba.Entities;

namespace PanMediatorPrueba.Repository;

public class CustomerRepository(string connectionString) : ICustomerRepository
{
    private IDbConnection CreateConnection() => new SqlConnection(connectionString);

    public async Task<int> CreateAsync(Customer customer)
    {
        using var connection = CreateConnection();
        const string sqlQuery = """
                INSERT INTO Customers(Name, Address, Phone)
                VALUES(@Name, @Address, @Phone);
                SELECT CAST(SCOPE_IDENTITY() as int);
            """;

        return await connection.ExecuteScalarAsync<int>(sqlQuery, customer);
    }
}
