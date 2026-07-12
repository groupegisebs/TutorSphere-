using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var services = new ServiceCollection();
services.AddLogging();
services.AddOptions();
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Cookies";
    options.DefaultChallengeScheme = "Cookies";
});
services.AddAuthentication("Bearer");
var sp = services.BuildServiceProvider();
var opts = sp.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
Console.WriteLine($"After AddAuthentication(Bearer): Auth={opts.DefaultAuthenticateScheme} Challenge={opts.DefaultChallengeScheme} Default={opts.DefaultScheme}");

services = new ServiceCollection();
services.AddLogging();
services.AddOptions();
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Cookies";
    options.DefaultChallengeScheme = "Cookies";
});
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
});
sp = services.BuildServiceProvider();
opts = sp.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
Console.WriteLine($"After Configure override: Auth={opts.DefaultAuthenticateScheme} Challenge={opts.DefaultChallengeScheme} Default={opts.DefaultScheme}");
