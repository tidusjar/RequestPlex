using Microsoft.EntityFrameworkCore;

namespace Ombi.Store.Context.MySql
{
    public sealed class ExternalMySqlContext : ExternalContext
    {
        private static bool _created;
        public ExternalMySqlContext(DbContextOptions<ExternalMySqlContext> options) : base(options)
        {
            if (_created) return;

            _created = true;
            try
            {
                Database.Migrate();
            }
            catch (System.InvalidOperationException)
            {
            }
            catch (System.Exception)
            {
            }
        }
    }
}