using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsReader.Services;
using RureSubPostsReader.Workers;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddHostedService<PostsCreateWorker>();
builder.Services.AddHostedService<PostsDeletedWorker>();
builder.Services.AddHostedService<ProfilePropertyChangedWorker>();
builder.Services.AddHostedService<PostsLikedWorker>();
builder.Services.AddHostedService<PostsCommentedWorker>();

#region Kafka

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
var kafkaGroupId = builder.Configuration["Kafka:GroupId"];

if (string.IsNullOrEmpty(kafkaBootstrapServers) || string.IsNullOrEmpty(kafkaGroupId))
{
    throw new Exception("Kafka configuration is null or empty.");
}

var kafkaConsumerConfig = new ConsumerConfig()
{
    BootstrapServers = kafkaBootstrapServers,
    GroupId = kafkaGroupId,
    EnableAutoCommit = false,
    EnableAutoOffsetStore = false,
    AutoOffsetReset = AutoOffsetReset.Earliest
};

builder.Services.AddSingleton(kafkaConsumerConfig);

#endregion

#region MongoDB

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp
        .GetRequiredService<IOptions<MongoDbSettings>>()
        .Value;

    return new MongoClient(settings.ConnectionString);
});

#endregion

#region Jwt

var jwtKey = builder.Configuration["JWT:Key"];

if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("Jwt key is null or empty!");
}

var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,

        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
    };
});

#endregion

#region Http

var ApiUrl = builder.Configuration["Http:Api"];

if (string.IsNullOrEmpty(ApiUrl))
{
    throw new Exception("Bad configuration! Http:Api is null or empty!");
}

builder.Services.AddHttpClient<HttpClient>(client => {
    client.BaseAddress = new Uri("http://orders-api/");
});

#endregion

#region Redis

var redisConnectionString = builder.Configuration["Redis:ConnectionString"];

if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new Exception("Redis was not configured!");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

#endregion

#region Cors

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost", "http://ruresub.loca.lt")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

#endregion

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseCors("Development");
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
