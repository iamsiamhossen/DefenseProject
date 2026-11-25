using KYAUCPChapter.API.Auth;
using KYAUCPChapter.API.Errors;
using KYAUCPChapter.API.Security;
using KYAUCPChapter.Infrastructure;
using KYAUCPChapter.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured."));
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddControllers(options => options.Filters.Add<ValidateAntiforgeryTokenFilter>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

if (args is [ProvisionOrganizerCommand.Name, ..])
{
    return await ProvisionOrganizerCommand.RunAsync(app.Services, args[1..], ProvisionOrganizerCommand.ReadConsoleSecret, Console.Out);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

await app.RunAsync();
return 0;
