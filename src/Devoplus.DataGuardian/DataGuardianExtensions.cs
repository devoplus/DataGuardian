using Microsoft.AspNetCore.Builder;

namespace Devoplus.DataGuardian;

public static class DataGuardianExtensions
{
    public static IApplicationBuilder UseDataGuardian(this IApplicationBuilder app, DataGuardianOptions? options = null)
        => app.UseMiddleware<DataGuardianMiddleware>(options ?? new DataGuardianOptions());
}
