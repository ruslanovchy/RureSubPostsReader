using Confluent.Kafka;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsReader.Services;
using RureSubPostsWriter.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddHostedService<PostsCreateProcesser>();

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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
