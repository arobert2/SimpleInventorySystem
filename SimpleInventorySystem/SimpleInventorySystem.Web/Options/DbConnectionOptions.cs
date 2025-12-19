namespace SimpleInventorySystem.Web.Options
{
    public class DbConnectionOptions
    {
        public static readonly string CONFIG_SECTION_NAME = "DbConnection";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>
        /// Gets a connection string
        /// </summary>
        /// <returns>The connection string</returns>
        public string GetDbConnectionString()
        {
            return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Trust Server Certificate=true";
        }

        public string GetPostgresConnectionString()
        {
            return $"Host={Host};Port={Port};Database=postgres;Username={Username};Password={Password};Trust Server Certificate=true";
        }
    }
}
