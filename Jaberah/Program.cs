using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Hangfire;
using Jaberah.Helpers;
using Jaberah.Jobs;
using Jaberah.Middlewares;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<JaberahDBContext>(x => x.UseSqlServer(builder.Configuration.GetConnectionString("DB")));
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddScoped<VerifyTokenAttribute>();
builder.Services.AddScoped<RequireDeployKeyAttribute>();
builder.Services.AddScoped<TokenHelper>();
builder.Services.AddScoped<DropboxService>();
builder.Services.AddScoped<FirebaseService>();
builder.Services.AddScoped<HttpClient>();
builder.Services.AddMemoryCache();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var key = Encoding.UTF8.GetBytes(builder.Configuration["TokenKey"]!);

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});
var serviceAccountFilePath = builder.Configuration["FCM:ServiceAccountFilePath"];
if (string.IsNullOrEmpty(serviceAccountFilePath))
{
    throw new InvalidOperationException("Firebase service account file path is not configured.");
}
FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromFile(serviceAccountFilePath),
});

builder.Services.AddAuthorization(options =>
{
    // All endpoints require authentication unless [AllowAnonymous] is used
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSwaggerGen(sw =>
{
    sw.SwaggerDoc("v1", new OpenApiInfo { Title = "Jaberah API", Version = "V1" });
    sw.EnableAnnotations();
    sw.OrderActionsBy(a => a.GroupName);
    sw.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'bearer' [space] and then your token in the text box below.\r\n\r\nExample: \"bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9......\"",

    });
    sw.AddSecurityRequirement(new OpenApiSecurityRequirement
      {
         {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
         }
      });
});

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DB")));

builder.Services.AddHangfireServer();

// Register your job service
builder.Services.AddScoped<IAttendanceJobService, AttendanceJobService>();


var app = builder.Build();

//await DataSeeder.SeedAsync(app.Services);


// Schedule the job — runs every minute (job logic skips Fridays and no-ops when no absent teachers)
RecurringJob.AddOrUpdate<IAttendanceJobService>(
    "mark-absent-teachers",
    job => job.MarkAbsentTeachersAsync(),
    "59 23 * * *", // 23:59 every day
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time")
    }
);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRouting();
app.UseCors(
    x => x.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod()
    );
app.UseCookiePolicy();


app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthorizationFilter()]
});

app.MapControllers();
app.UseSwagger().UseSwaggerUI(sw =>
{
    sw.SwaggerEndpoint("/swagger/v1/swagger.json", "Jaberah API");
    sw.RoutePrefix = string.Empty;
    sw.DefaultModelsExpandDepth(-1);
    sw.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    sw.DisplayRequestDuration();

    // ✅ ADD THIS LINE
    sw.ConfigObject.PersistAuthorization = true;
    sw.ConfigObject.AdditionalItems["persistAuthorization"] = true;
});


app.Run();
