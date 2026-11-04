using System.Net;
using Xunit;
namespace MSMultilider.Finanzas.Tests;
public sealed class SessionTests {
    [Theory,InlineData(401,401),InlineData(503,503),InlineData(500,503),InlineData(302,503)]
    public async Task RevocationAndAuthorityFailuresAreChecked(int upstream,int expected) {
        using var factory=new ApiFactory {IdentityStatus=(HttpStatusCode)upstream};
        using var client=factory.Client(ApiFactory.AllPermissions);
        var response=await client.GetAsync("/api/pagos");
        Assert.Equal(expected,(int)response.StatusCode);Assert.Equal(1,factory.IdentityCalls);
    }
    [Fact] public async Task TokenClaimsCannotGrantPermissionsRemovedByAuthority() {
        using var factory=new ApiFactory {AuthoritativePermissions=[]};using var client=factory.Client(ApiFactory.AllPermissions);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/pagos")).StatusCode);
    }
    [Fact] public async Task AuthorityCannotSubstituteAnotherUser() {
        using var factory=new ApiFactory {IdentitySubject=2};using var client=factory.Client(ApiFactory.AllPermissions);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/pagos")).StatusCode);
    }
}
