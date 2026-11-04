using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// Identity protocol v1. Same contract in all consumers; no application/domain data is replicated.
namespace Multilider.Identity;
internal static class SessionValidation {
    public static void Configure(JwtBearerOptions options,IConfiguration config) {
        options.MapInboundClaims=false;options.IncludeErrorDetails=false;
        options.TokenValidationParameters=new TokenValidationParameters {
            ValidateIssuer=true,ValidateAudience=true,ValidateLifetime=true,RequireExpirationTime=true,
            ValidateIssuerSigningKey=true,RequireSignedTokens=true,ValidAlgorithms=[SecurityAlgorithms.HmacSha256],
            ValidIssuer=config["Jwt:Issuer"],ValidAudience=config["Jwt:Audience"],
            IssuerSigningKeyResolver=(_,_,_,_)=> {
                var key=config["Jwt:Key"];
                return !string.IsNullOrWhiteSpace(key) && Encoding.UTF8.GetByteCount(key)>=32
                    ? [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))] : [];
            },ClockSkew=TimeSpan.FromSeconds(30),NameClaimType="sub"
        };
        options.Events=new JwtBearerEvents {
            OnTokenValidated=async context=> {
                var principal=context.Principal!;
                if(!int.TryParse(principal.FindFirstValue("sub"),out var id) || id<=0 ||
                    !Guid.TryParse(principal.FindFirstValue("jti"),out _) || !long.TryParse(principal.FindFirstValue("exp"),out var exp)) {
                    context.Fail("Invalid identity.");return;
                }
                if(principal.HasClaim(x=>x.Type=="ver")) {
                    if(!long.TryParse(principal.FindFirstValue("ver"),out var version) || version<0 ||
                        !long.TryParse(principal.FindFirstValue("iat"),out var issued) || issued>DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds() ||
                        !principal.HasClaim(x=>x.Type=="nbf")) {context.Fail("Invalid claims.");return;}
                } else if(!DateTimeOffset.TryParse(config["Identity:LegacyUntil"],out var deadline) ||
                    DateTimeOffset.UtcNow>deadline.AddSeconds(30) || exp>deadline.ToUnixTimeSeconds()) {
                    context.Fail("Legacy credential expired.");return;
                }
                try {
                    var client=context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("identity-status");
                    var request=new HttpRequestMessage(HttpMethod.Get,"api/auth/me");
                    request.Headers.Authorization=AuthenticationHeaderValue.Parse(context.Request.Headers.Authorization.ToString());
                    using var response=await client.SendAsync(request,context.HttpContext.RequestAborted);
                    if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {context.Fail("Session inactive.");return;}
                    if(!response.IsSuccessStatusCode) throw new HttpRequestException();
                    using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                    if(!body.RootElement.TryGetProperty("id",out var subject) || subject.GetInt32()!=id ||
                        !body.RootElement.TryGetProperty("estado",out var state) || state.ValueKind!=JsonValueKind.True) {context.Fail("Session inactive.");return;}
                    var identity=(ClaimsIdentity)principal.Identity!;
                    foreach(var claim in identity.FindAll("permission").ToArray()) identity.RemoveClaim(claim);
                    foreach(var row in body.RootElement.GetProperty("rolesPermisos").EnumerateArray()) {
                        var name=row.GetProperty("nombrePermiso").GetString();
                        if(!string.IsNullOrWhiteSpace(name)) identity.AddClaim(new Claim("permission",name));
                    }
                } catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException) {
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Identity.Status")
                        .LogWarning("Session authority check failed: {FailureType}", e.GetType().Name);
                    context.HttpContext.Items["identity-unavailable"]=true;context.Fail("Identity unavailable.");
                }
            },
            OnChallenge=async context=> {
                if(context.HttpContext.Items.ContainsKey("identity-unavailable")) {
                    context.HandleResponse();context.Response.StatusCode=503;
                    await context.Response.WriteAsJsonAsync(new {message="Seguridad no está disponible.",code="SERVICE_UNAVAILABLE"});
                }
            }
        };
    }
}
