﻿using Kinetix.Tools.Model.Loaders;
using Microsoft.Extensions.DependencyInjection;

namespace Kinetix.Tools.Model
{
    using static ModelUtils;

    public static class ServiceExtensions
    {
        public static IServiceCollection AddModelStore(this IServiceCollection services, FileChecker fileChecker, ModelConfig? config = null, string? rootDir = null)
        {
            services
                .AddMemoryCache()
                .AddSingleton(fileChecker)
                .AddSingleton<ModelFileLoader>()
                .AddSingleton<DomainFileLoader>()
                .AddSingleton<ModelStore>();

            if (config != null && rootDir != null)
            {
                config.ModelRoot ??= string.Empty;
                config.Domains ??= "domains.yml";

                CombinePath(rootDir, config, c => c.ModelRoot);
                CombinePath(rootDir, config, c => c.Domains);
                CombinePath(rootDir, config, c => c.StaticLists);
                CombinePath(rootDir, config, c => c.ReferenceLists);
                services.AddSingleton(config);
            }

            return services;
        }
    }
}
