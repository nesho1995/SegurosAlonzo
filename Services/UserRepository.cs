using Dapper;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;

namespace ReclamosWhatsApp.Services
{
    public class UserRepository
    {
        private readonly DbConnectionFactory _db;

        public UserRepository(DbConnectionFactory db)
        {
            _db = db;
        }

        public async Task<User?> GetUserByUsernameAsync(string username, bool includeInactive = false)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync("ALTER TABLE Users ADD COLUMN IF NOT EXISTS CustomPermissionsJson TEXT NULL;");
            var sql = @"
                SELECT
                    u.Id,
                    u.Username,
                    u.PasswordHash,
                    u.RoleId,
                    u.IsActive,
                    u.CustomPermissionsJson,
                    u.FailedLoginAttempts,
                    u.LockoutUntil,
                    u.LastPasswordChange,
                    r.Id Role_Id,
                    r.Name Role_Name
                FROM Users u 
                INNER JOIN Roles r ON u.RoleId = r.Id 
                WHERE u.Username = @Username";

            if (!includeInactive)
                sql += " AND u.IsActive = 1";

            var row = await connection.QueryFirstOrDefaultAsync(sql, new { Username = username });

            if (row is null)
                return null;

            return new User
            {
                Id = row.Id,
                Username = row.Username,
                PasswordHash = row.PasswordHash,
                RoleId = row.RoleId,
                IsActive = row.IsActive,
                CustomPermissionsJson = row.CustomPermissionsJson,
                FailedLoginAttempts = row.FailedLoginAttempts,
                LockoutUntil = row.LockoutUntil,
                LastPasswordChange = row.LastPasswordChange,
                Role = new Role
                {
                    Id = row.Role_Id,
                    Name = row.Role_Name
                }
            };
        }

        public async Task<IEnumerable<UserAdminViewModel>> GetUsersAsync()
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync("ALTER TABLE Users ADD COLUMN IF NOT EXISTS CustomPermissionsJson TEXT NULL;");
            const string sql = @"
                SELECT
                    u.Id,
                    u.Username,
                    u.RoleId,
                    u.IsActive,
                    u.CustomPermissionsJson,
                    r.Name RoleName
                FROM Users u
                INNER JOIN Roles r ON u.RoleId = r.Id
                ORDER BY u.Username;";

            return await connection.QueryAsync<UserAdminViewModel>(sql);
        }

        public async Task<IEnumerable<Role>> GetRolesAsync()
        {
            using var connection = _db.CreateConnection();
            const string sql = @"
                SELECT Id, Name
                FROM Roles
                ORDER BY Id;";

            return await connection.QueryAsync<Role>(sql);
        }

        public async Task<int> CreateUserAsync(string username, string passwordHash, int roleId)
        {
            using var connection = _db.CreateConnection();
            const string sql = @"
                INSERT INTO Users (Username, PasswordHash, RoleId, IsActive)
                VALUES (@Username, @PasswordHash, @RoleId, 1);
                SELECT LAST_INSERT_ID();";
            
            return await connection.ExecuteScalarAsync<int>(sql, new { Username = username, PasswordHash = passwordHash, RoleId = roleId });
        }

        public async Task<int?> GetRoleIdByNameAsync(string roleName)
        {
            using var connection = _db.CreateConnection();
            return await connection.ExecuteScalarAsync<int?>("SELECT Id FROM Roles WHERE Name = @roleName LIMIT 1;", new { roleName });
        }

        public async Task<int> EnsureRoleAsync(string roleName)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync("INSERT IGNORE INTO Roles (Name) VALUES (@roleName);", new { roleName });
            var roleId = await connection.ExecuteScalarAsync<int?>("SELECT Id FROM Roles WHERE Name = @roleName LIMIT 1;", new { roleName });
            return roleId ?? 1;
        }

        public async Task<bool> RoleExistsAsync(int roleId)
        {
            using var connection = _db.CreateConnection();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Roles WHERE Id = @roleId;", new { roleId }) > 0;
        }

        public async Task UpdateUserAsync(int id, int roleId, bool isActive)
        {
            using var connection = _db.CreateConnection();
            const string sql = @"
                UPDATE Users
                SET RoleId = @roleId,
                    IsActive = @isActive
                WHERE Id = @id;";

            await connection.ExecuteAsync(sql, new { id, roleId, isActive });
        }

        public async Task UpdateUserPermissionsAsync(int id, IEnumerable<string> permissions)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync("ALTER TABLE Users ADD COLUMN IF NOT EXISTS CustomPermissionsJson TEXT NULL;");
            var valid = permissions
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => Permissions.All.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToArray();
            var json = System.Text.Json.JsonSerializer.Serialize(valid);
            await connection.ExecuteAsync("UPDATE Users SET CustomPermissionsJson = @json WHERE Id = @id;", new { id, json });
        }

        public async Task DeleteUserAsync(int id)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync("DELETE FROM Users WHERE Id = @id;", new { id });
        }

        public async Task RegisterFailedLoginAsync(int id)
        {
            using var connection = _db.CreateConnection();
            const string sql = @"
                UPDATE Users
                SET FailedLoginAttempts = FailedLoginAttempts + 1,
                    LockoutUntil = CASE WHEN FailedLoginAttempts + 1 >= 5 THEN DATE_ADD(NOW(), INTERVAL 15 MINUTE) ELSE LockoutUntil END
                WHERE Id = @id;";

            await connection.ExecuteAsync(sql, new { id });
        }

        public async Task ResetFailedLoginAsync(int id)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync("UPDATE Users SET FailedLoginAttempts = 0, LockoutUntil = NULL WHERE Id = @id;", new { id });
        }

        public async Task ChangePasswordAsync(int id, string passwordHash)
        {
            using var connection = _db.CreateConnection();
            const string sql = @"
                UPDATE Users
                SET PasswordHash = @passwordHash,
                    LastPasswordChange = NOW(),
                    FailedLoginAttempts = 0,
                    LockoutUntil = NULL
                WHERE Id = @id;";

            await connection.ExecuteAsync(sql, new { id, passwordHash });
        }
    }
}
