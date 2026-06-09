using Hunting.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddHuntingStandaloneWeb();

var app = builder.Build();
await app.UseHuntingStandaloneWebAsync();
app.Run();
