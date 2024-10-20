﻿using JobBank1111.Infrastructure.TraceContext;
using JobBank1111.Job.DB;
using Microsoft.EntityFrameworkCore;

namespace JobBank1111.Job.WebAPI;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddEnvironments(this IServiceCollection services)
    {
        services.AddSingleton<SYS_DATABASE_CONNECTION_STRING>();
        return services;
    }
    
    public static IServiceCollection AddContextAccessor(this IServiceCollection services)
    {
        services.AddSingleton<ContextAccessor<TraceContext>>();
        services.AddSingleton<IContextGetter<TraceContext>>(p => p.GetService<ContextAccessor<TraceContext>>());
        services.AddSingleton<IContextSetter<TraceContext>>(p => p.GetService<ContextAccessor<TraceContext>>());
        return services;
    }

    public static void AddDatabase(this IServiceCollection services)
    {
        services.AddDbContextFactory<MemberDbContext>((provider, builder) =>
        {
            var environment = provider.GetService<SYS_DATABASE_CONNECTION_STRING>();
            var connectionString = environment.Value;
            builder.UseSqlServer(connectionString)
                .UseLoggerFactory(provider.GetService<ILoggerFactory>())
                .EnableSensitiveDataLogging()
                ;
        });
    }

    public static IHttpClientBuilder AddExternalApiHttpClient(this IServiceCollection services)
    {
        return services.AddHttpClient("externalApi",
                                      (provider, client) =>
                                      {
                                          var traceContext = provider.GetService<TraceContext>();
                                          var externalApi = provider.GetService<EXTERNAL_API>();
                                          var traceId = traceContext.TraceId;
                                          client.BaseAddress = new Uri(externalApi.Value);
                                          client.DefaultRequestHeaders.Add(SysHeaderNames.TraceId, traceId);
                                      })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // 改成 true，會快取 Cookie
                UseCookies = false
            });
    }

    public static IServiceCollection AddSysEnvironments(this IServiceCollection services)
    {
        services.AddSingleton<SYS_DATABASE_CONNECTION_STRING>();
        services.AddSingleton<SYS_REDIS_URL>();
        return services;
    }
}