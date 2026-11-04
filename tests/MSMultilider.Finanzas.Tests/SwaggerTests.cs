using Xunit;
namespace MSMultilider.Finanzas.Tests;
public sealed class SwaggerTests {
    [Fact] public async Task OpenApiExponeJwtYEndpointsDelServicio() {
        using var app=new ApiFactory {Development=true};
        using var client=app.Client();
        var document=await(await client.GetAsync("/swagger/v1/swagger.json")).Json();
        Assert.True(document.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer",out _));
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/pagos",out _));
    }
}
