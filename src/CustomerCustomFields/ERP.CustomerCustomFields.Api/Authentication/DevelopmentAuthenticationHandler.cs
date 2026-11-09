using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace ERP.CustomerCustomFields.Api.Authentication;
// Development convenience only. Outside Development this scheme challenges with401
// until the host is integrated with the ERP's actual identity provider.
public sealed class DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,IHostEnvironment environment)
 :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
 protected override Task<AuthenticateResult> HandleAuthenticateAsync()
 {
  if(!environment.IsDevelopment()) return Task.FromResult(AuthenticateResult.NoResult());
  var identity=new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"1"),new Claim(ClaimTypes.Name,"Demo user")],Scheme.Name);
  return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
 }
}
