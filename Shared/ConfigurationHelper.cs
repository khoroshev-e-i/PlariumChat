using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shared
{
    public static class ConfigurationHelper
    {

        public static TSettings GetSettings<TSettings>()
            where TSettings : class, new()
        {
            var result = new TSettings();
            var configuration = (IConfiguration)new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build();

            configuration.Bind(typeof(TSettings).Name, result);

            return result;
        }
    }
}
