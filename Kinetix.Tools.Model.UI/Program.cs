﻿using System.IO;
using Kinetix.Tools.Model.Loaders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kinetix.Tools.Model.UI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
               .ConfigureWebHostDefaults(webBuilder =>
               {
                   webBuilder.UseStartup<Startup>();
               })
               .ConfigureServices(services =>
               {
                   var fileChecker = new FileChecker();
                   var configFile = new FileInfo(args[0]);
                   var config = fileChecker.Deserialize<ModelConfig>(configFile.OpenText().ReadToEnd());
                   var dn = configFile.DirectoryName;

                   services
                       .AddModelStore(fileChecker, config, dn)
                       .AddHostedService<ModelWatcherService>()
                       .AddSingleton<ModelFileProvider>()
                       .AddSingleton<IModelWatcher>(p => p.GetService<ModelFileProvider>());
               })
               .Build()
               .Run();
        }
    }
}
