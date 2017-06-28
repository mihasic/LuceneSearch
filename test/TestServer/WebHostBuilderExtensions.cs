namespace TestServer
{
    using System;
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder UseApplicationKey(this IWebHostBuilder hostBuilder, string applicationKey) =>
            hostBuilder.UseSetting(WebHostDefaults.ApplicationKey, applicationKey);
        public static IWebHostBuilder UseStartup(this IWebHostBuilder hostBuilder, IStartup startupInstance)
        {
            var typeInfo = startupInstance.GetType().GetTypeInfo();
            return hostBuilder
                .UseApplicationKey(typeInfo.Assembly.GetName().Name)
                .ConfigureServices(services => services.AddSingleton(typeof(IStartup), _ => startupInstance));
        }

        public static IWebHostBuilder UseStartup(this IWebHostBuilder hostBuilder, Action<IApplicationBuilder> configure)
        {
            return hostBuilder
                .ConfigureServices(services => services.AddSingleton(typeof(IStartup), sp => new DelegateStartup(configure)));
        }
        public static IWebHostBuilder UseStartup(this IWebHostBuilder hostBuilder, Action<IApplicationBuilder, IHostingEnvironment> configure)
        {
            return hostBuilder
                .ConfigureServices(services =>
                    services.AddSingleton(typeof(IStartup),
                        sp => new DelegateStartup(app => configure(app, sp.GetRequiredService<IHostingEnvironment>()))));
        }
        public static IWebHostBuilder UseStartup(this IWebHostBuilder hostBuilder,
            Action<IApplicationBuilder, IHostingEnvironment, ILoggerFactory> configure)
        {
            return hostBuilder
                .ConfigureServices(services =>
                    services.AddSingleton(typeof(IStartup),
                        sp => new DelegateStartup(app => configure(app, sp.GetRequiredService<IHostingEnvironment>(), sp.GetRequiredService<ILoggerFactory>()))));
        }

        private class DelegateStartup : IStartup
        {
            private readonly Action<IApplicationBuilder> _configure;

            public DelegateStartup(Action<IApplicationBuilder> configure)
            {
                _configure = configure;
            }

            public void Configure(IApplicationBuilder app) => _configure(app);

            public IServiceProvider ConfigureServices(IServiceCollection services) => services.BuildServiceProvider();
        }
    }
}