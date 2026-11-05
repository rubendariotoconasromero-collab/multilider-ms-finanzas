using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MSMultilider.Finanzas.Infrastructure;

public sealed class BusinessException(string message, int status = 422) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var status = error switch {
            BusinessException b => b.Status,
            Multilider.Operations.ProtocolException p => p.Status,
            DbUpdateConcurrencyException => 409,
            DbUpdateException => 409,
            HttpRequestException or TaskCanceledException => 503,
            _ => 500
        };
        if (status == 500) logger.LogError("Operación no completada: {FailureType}", error.GetType().Name);
        var detail = error is BusinessException or Multilider.Operations.ProtocolException ? error.Message : status switch {
            409 => "Los datos cambiaron o existe un registro duplicado. Actualiza y vuelve a intentar.",
            503 => "El servicio relacionado no está disponible. No se confirmó la operación.",
            _ => "No se pudo completar la operación."
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = detail }, ct);
        return true;
    }
}
public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [System.Text.Json.Serialization.JsonIgnore] public Guid Version { get; set; } = Guid.NewGuid();
}
public abstract class ServiceDbContext(DbContextOptions options) : DbContext(options)
{
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>().Where(e => e.State is EntityState.Added or EntityState.Modified)) {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
            entry.Entity.Version = Guid.NewGuid();
        }
        return base.SaveChangesAsync(ct);
    }
    protected override void OnModelCreating(ModelBuilder model)
    {
        foreach (var entity in model.Model.GetEntityTypes()) {
            if (typeof(Entity).IsAssignableFrom(entity.ClrType))
                model.Entity(entity.ClrType).Property(nameof(Entity.Version)).IsConcurrencyToken();
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))) {
                property.SetPrecision(18); property.SetScale(2);
            }
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
                property.SetMaxLength(1000);
        }
        foreach (var foreignKey in model.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
public sealed record Page<T>(IReadOnlyList<T> Data, int Total, int PageNumber, int PerPage, int LastPage);
public static class Api
{
    public static void Require(bool condition, string message, int status = 422) {
        if (!condition) throw new BusinessException(message, status);
    }
    public static async Task<T> Find<T>(DbSet<T> set, int id, CancellationToken ct) where T:Entity =>
        await set.FindAsync([id], ct) ?? throw new BusinessException("Registro no encontrado.",404);
    public static async Task<Page<T>> Paginate<T>(IQueryable<T> query, int page, int perPage, CancellationToken ct) where T:Entity {
        page = Math.Clamp(page,1,1000000); perPage = Math.Clamp(perPage,1,100);
        var total = await query.CountAsync(ct);
        return new(await query.OrderByDescending(x=>x.Id).Skip((page-1)*perPage).Take(perPage).ToListAsync(ct),
            total,page,perPage,Math.Max(1,(int)Math.Ceiling(total/(double)perPage)));
    }
    public static decimal Money(decimal value) => decimal.Round(value,2,MidpointRounding.AwayFromZero);
    public static void Currency(string value) => Require(value is "BOB" or "USD","Moneda inválida: usa BOB o USD.");
    public static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue("sub"),out var id) && id>0
        ? id : throw new BusinessException("El token no identifica al usuario.",401);
}
public class RelatedApi(HttpClient client, IHttpContextAccessor accessor)
{
    private HttpRequestMessage Request(HttpMethod method,string path) {
        var request = new HttpRequestMessage(method,path);
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization)) request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        return request;
    }
    public async Task<T> Get<T>(string path,CancellationToken ct) {
        using var request = Request(HttpMethod.Get,path);
        using var response = await client.SendAsync(request,ct);
        if (response.StatusCode==HttpStatusCode.NotFound) throw new BusinessException("Registro relacionado no encontrado.",422);
        if (response.StatusCode==HttpStatusCode.Forbidden) throw new BusinessException("Falta permiso en el servicio relacionado.",403);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions,ct) ?? throw new HttpRequestException("Respuesta vacía.");
    }
    public async Task Post(string path,CancellationToken ct) {
        using var request = Request(HttpMethod.Post,path);
        request.Content = JsonContent.Create(new {});
        using var response = await client.SendAsync(request,ct);
        if (response.StatusCode==HttpStatusCode.Conflict || (int)response.StatusCode==422)
            throw new BusinessException("La operación relacionada no permite esta transición.",409);
        if(response.StatusCode==HttpStatusCode.Forbidden) throw new BusinessException("Falta permiso en el servicio relacionado.",403);
        response.EnsureSuccessStatusCode();
    }
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower };
}

