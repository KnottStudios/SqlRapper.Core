using Microsoft.Extensions.Configuration;
using System.IO;

namespace SqlRapper.Core.Tests
{
    public static class AppSettingsReader
    {
        public static IConfigurationRoot GetSettings() {
            IConfigurationBuilder builder = new ConfigurationBuilder();

            builder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

            var root = builder.Build();

            return root;
        }
    }
}
