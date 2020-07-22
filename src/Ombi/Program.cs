using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Ombi.Store.Context;
using Ombi.Store.Entities;
using CommandLine;
using CommandLine.Text;
using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ombi.Extensions;
using Ombi.Helpers;
using Ombi.Store.Context.MySql;
using Ombi.Store.Context.Sqlite;

namespace Ombi
{
    public class Program
    {
        private static string UrlArgs { get; set; }
        private static StartupLog _log = new StartupLog(true);

        public static void Main(string[] args)
        {
            Console.Title = "Ombi";

            var host = string.Empty;
            var storagePath = string.Empty;
            var baseUrl = string.Empty;
            var demo = false;
            var result = Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    host = o.Host;
                    storagePath = o.StoragePath;
                    baseUrl = o.BaseUrl;
                    demo = o.Demo;
                }).WithNotParsed(err =>
                {
                    foreach (var e in err)
                    {
                        _log.LogError(e);
                    }
                });

            Console.WriteLine(HelpOutput(result));

            UrlArgs = host;

            var urlValue = string.Empty;
            var instance = StoragePathSingleton.Instance;
            var demoInstance = DemoSingleton.Instance;
            demoInstance.Demo = demo;
            instance.StoragePath = storagePath ?? string.Empty;
            // Check if we need to migrate the settings
            DeleteSchedules();
            //CheckAndMigrate();

            var services = new ServiceCollection();
            services.ConfigureDatabases();
            using (var provider = services.BuildServiceProvider())
            {
                try
                {
                    var settingsDb = provider.GetRequiredService<SettingsContext>();
                    var config = settingsDb.ApplicationConfigurations.ToList();
                    var url = config.FirstOrDefault(x => x.Type == ConfigurationTypes.Url);
                    var dbBaseUrl = config.FirstOrDefault(x => x.Type == ConfigurationTypes.BaseUrl);
                    if (url == null)
                    {
                        url = new ApplicationConfiguration
                        {
                            Type = ConfigurationTypes.Url,
                            Value = "http://*:5000"
                        };
                        using (var tran = settingsDb.Database.BeginTransaction())
                        {
                            settingsDb.ApplicationConfigurations.Add(url);
                            settingsDb.SaveChanges();
                            tran.Commit();
                        }

                        urlValue = url.Value;
                    }

                    if (!url.Value.Equals(host))
                    {
                        url.Value = UrlArgs;

                        using (var tran = settingsDb.Database.BeginTransaction())
                        {
                            settingsDb.SaveChanges();
                            tran.Commit();
                        }

                        urlValue = url.Value;
                    }

                    if (dbBaseUrl == null)
                    {
                        if (baseUrl.HasValue() && baseUrl.StartsWith("/"))
                        {
                            dbBaseUrl = new ApplicationConfiguration
                            {
                                Type = ConfigurationTypes.BaseUrl,
                                Value = baseUrl
                            };

                            using (var tran = settingsDb.Database.BeginTransaction())
                            {
                                settingsDb.ApplicationConfigurations.Add(dbBaseUrl);
                                settingsDb.SaveChanges();
                                tran.Commit();
                            }
                        }
                    }
                    else if (baseUrl.HasValue() && !baseUrl.Equals(dbBaseUrl.Value))
                    {
                        dbBaseUrl.Value = baseUrl;

                        using (var tran = settingsDb.Database.BeginTransaction())
                        {
                            settingsDb.SaveChanges();
                            tran.Commit();
                        }
                    }

                    _log.LogInformation($"We are running on {urlValue}");
                    CreateWebHostBuilder(args).Build().Run();
                }
                catch (MySql.Data.MySqlClient.MySqlException e) when (e.SqlState.Equals("28000") || e.SqlState.Equals("42000"))
                {
                    // numbre = 1045, sqlstate = 28000, "Access denied for user 'ombi_dev'@'x.x.x.x' (using password: NO)"
                    // number = 1044, sqlstate = 42000, "Access denied for user 'ombi_dev'@'x.x.x.x' to database 'OmbiDev'"
                    _log.LogError("MySQL > " + e.Message);
                    return;
                }
                catch (MySql.Data.MySqlClient.MySqlException e)
                {
                    _log.LogError(e.ToString());
                    throw;
                }
                catch (System.InvalidOperationException e) when (e.InnerException is MySql.Data.MySqlClient.MySqlException && e.InnerException.Message.Equals("Unable to connect to any of the specified MySQL hosts."))
                {
                    _log.LogError("MySQL > " + e.InnerException.Message);
                    return;
                }
                catch (System.Exception e)
                {
                    _log.LogError(e.ToString());
                    throw;
                }

            }
        }

        private static void DeleteSchedules()
        {
            try
            {
                if (File.Exists("Schedules.db"))
                {
                    File.Delete("Schedules.db");
                }
            }
            catch (Exception e)
            {
                _log.LogError(e.ToString());
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls(UrlArgs)
                .PreferHostingUrls(true);

        private static string HelpOutput(ParserResult<Options> args)
        {
            var result = new StringBuilder();

            result.AppendLine("Hello, welcome to Ombi");
            result.AppendLine("Valid options are:");
            result.AppendLine(HelpText.AutoBuild(args, null, null));

            return result.ToString();
        }
    }

    public class Options
    {
        [Option("host", Required = false, HelpText =
            "Set to a semicolon-separated (;) list of URL prefixes to which the server should respond. For example, http://localhost:123." +
            " Use \"*\" to indicate that the server should listen for requests on any IP address or hostname using the specified port and protocol (for example, http://*:5000). " +
            "The protocol (http:// or https://) must be included with each URL. Supported formats vary between servers.", Default = "http://*:5000")]
        public string Host { get; set; }

        [Option("storage", Required = false, HelpText = "Storage path, where we save the logs and database")]
        public string StoragePath { get; set; }

        [Option("baseurl", Required = false, HelpText = "The base URL for reverse proxy scenarios")]
        public string BaseUrl { get; set; }

        [Option("demo", Required = false, HelpText = "Demo mode, you will never need to use this, fuck that fruit company...")]
        public bool Demo { get; set; }

    }
}
