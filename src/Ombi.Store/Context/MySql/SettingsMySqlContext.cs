using Microsoft.EntityFrameworkCore;

namespace Ombi.Store.Context.MySql
{
    public sealed class SettingsMySqlContext : SettingsContext
    {
        private static bool _created;
        public SettingsMySqlContext(DbContextOptions<SettingsMySqlContext> options) : base(options)
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