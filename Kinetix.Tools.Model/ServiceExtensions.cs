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
                .AddSingleton<ClassLoader>()
                .AddSingleton<ModelFileLoader>()
                .AddSingleton<ModelStore>();

            if (config != null && rootDir != null)
            {
                config.ModelRoot ??= string.Empty;

                CombinePath(rootDir, config, c => c.ModelRoot);
                services.AddSingleton(config);
            }

            return services;
        }
    }
}
