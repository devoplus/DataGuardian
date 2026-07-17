using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Devoplus.DataGuardian;

/// <summary>Service-registration helpers for DataGuardian.</summary>
public static class DataGuardianServiceCollectionExtensions
{
    /// <summary>Registers DataGuardian options configured from a delegate.</summary>
    public static IServiceCollection AddDataGuardian(this IServiceCollection services, Action<DataGuardianOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var options = new DataGuardianOptions();
        configure(options);
        options.Validate();
        services.AddSingleton(options);
        return services;
    }

    /// <summary>Registers DataGuardian options bound from a configuration section (e.g. <c>"DataGuardian"</c>).</summary>
    public static IServiceCollection AddDataGuardian(this IServiceCollection services, IConfiguration section)
    {
        if (section is null) throw new ArgumentNullException(nameof(section));
        var options = new DataGuardianOptions();
        section.Bind(options);
        options.Validate();
        services.AddSingleton(options);
        return services;
    }
}

/// <summary>Pipeline helpers for DataGuardian.</summary>
public static class DataGuardianApplicationBuilderExtensions
{
    /// <summary>Adds the middleware, resolving options registered via <c>AddDataGuardian</c> (or defaults).</summary>
    public static IApplicationBuilder UseDataGuardian(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService(typeof(DataGuardianOptions)) as DataGuardianOptions
                      ?? new DataGuardianOptions();
        options.Validate();
        return app.UseMiddleware<DataGuardianMiddleware>(options);
    }

    /// <summary>Adds the middleware with an explicit options instance.</summary>
    public static IApplicationBuilder UseDataGuardian(this IApplicationBuilder app, DataGuardianOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        return app.UseMiddleware<DataGuardianMiddleware>(options);
    }

    /// <summary>Adds the middleware with options configured inline.</summary>
    public static IApplicationBuilder UseDataGuardian(this IApplicationBuilder app, Action<DataGuardianOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var options = new DataGuardianOptions();
        configure(options);
        options.Validate();
        return app.UseMiddleware<DataGuardianMiddleware>(options);
    }
}

/// <summary>
/// Compatibility shim retained so callers that reference <c>DataGuardianExtensions</c> by type name
/// (e.g. <c>DataGuardianExtensions.UseDataGuardian(app, opt)</c>) continue to compile.
/// Use <see cref="DataGuardianApplicationBuilderExtensions"/> or <see cref="DataGuardianServiceCollectionExtensions"/> instead.
/// </summary>
[Obsolete("Use DataGuardianApplicationBuilderExtensions or DataGuardianServiceCollectionExtensions instead. This type will be removed in a future major version.")]
public static class DataGuardianExtensions
{
    /// <inheritdoc cref="DataGuardianApplicationBuilderExtensions.UseDataGuardian(IApplicationBuilder)"/>
    [Obsolete("Use DataGuardianApplicationBuilderExtensions.UseDataGuardian instead.")]
    public static IApplicationBuilder UseDataGuardian(IApplicationBuilder app)
        => DataGuardianApplicationBuilderExtensions.UseDataGuardian(app);

    /// <inheritdoc cref="DataGuardianApplicationBuilderExtensions.UseDataGuardian(IApplicationBuilder, DataGuardianOptions)"/>
    [Obsolete("Use DataGuardianApplicationBuilderExtensions.UseDataGuardian instead.")]
    public static IApplicationBuilder UseDataGuardian(IApplicationBuilder app, DataGuardianOptions options)
        => DataGuardianApplicationBuilderExtensions.UseDataGuardian(app, options);

    /// <inheritdoc cref="DataGuardianApplicationBuilderExtensions.UseDataGuardian(IApplicationBuilder, Action{DataGuardianOptions})"/>
    [Obsolete("Use DataGuardianApplicationBuilderExtensions.UseDataGuardian instead.")]
    public static IApplicationBuilder UseDataGuardian(IApplicationBuilder app, Action<DataGuardianOptions> configure)
        => DataGuardianApplicationBuilderExtensions.UseDataGuardian(app, configure);
}
