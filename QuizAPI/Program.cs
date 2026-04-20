using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QuizAPI;
using QuizAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseServices>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor",
        policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddSingleton<MultiplayerManager>();
builder.Services.AddSingleton<RankedMultiplayerManager>();
builder.Services.AddSingleton<UsernameValidatorService>();
builder.Services.AddSingleton<CustomQuizMultiplayerManager>();
builder.Services.AddSignalR();

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("OpenTdb", client =>
{
    client.BaseAddress = new Uri("https://opentdb.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<JwtService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    path.StartsWithSegments("/hubs/ranked-multiplayer"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

app.UseCors("AllowBlazor");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapHub<MultiplayerHub>("/hubs/multiplayer");
app.MapHub<RankedMultiplayerHub>("/hubs/ranked-multiplayer");
app.MapHub<CustomQuizMultiplayerHub>("/hubs/custom-quiz-multiplayer");

app.Run();