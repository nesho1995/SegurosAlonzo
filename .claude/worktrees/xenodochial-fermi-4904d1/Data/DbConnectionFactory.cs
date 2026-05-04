using System.Data;
using MySqlConnector;

namespace ReclamosWhatsApp.Data
{
    public class DbConnectionFactory
    {
        private readonly IConfiguration _configuration;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IDbConnection CreateConnection()
        {
            var connectionString = _configuration.GetConnectionString("Default");
            return new MySqlConnection(connectionString);
        }
    }
}