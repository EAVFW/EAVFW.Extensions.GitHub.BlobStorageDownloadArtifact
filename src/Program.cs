using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;


namespace EAVFW.Extensions.GitHub.BlobStorageDownloadArtifact
{
   
      
    public class App : System.CommandLine.RootCommand
    {
        [Alias("--name")]
        [Alias("-n")]
        [Description("\"Staring with a slash / will use github-action-artifacts as the container name, otherwise the first segment is used as container name.\"")]
        public string NameOption { get; set; }// = new Option<string>("--name", );


        [Alias("--path")]
        [Description("The output path")]
        public string Path { get; set; }


        [Required]
        public Option<string> ConnectionString { get; } = new Option<string>("--connection-string", "connectionstring");



        public App(IEnumerable<Command> commands)
        {  
            Handler = COmmandExtensions.Create(this,commands, Run);
        }
        public async Task<int> Run(ParseResult parseResult, IConsole console)  
        {
            console.WriteLine("Hello World 2");

            var storage = new BlobServiceClient(ConnectionString.GetValue(parseResult));
            var name = NameOption.Replace("\\","/");
            var containerName = name.Split("/").First() ?? "github-action-artifacts";
        
            var basePath = string.Join("/", name.Split('/').Skip(1));
            var runid = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
            var destinationPath = $"artifacts/{basePath.Trim('/')}/runs/{runid}";


            if (containerName.Length < 3)
            {   
                destinationPath = $"{containerName}/artifacts/{basePath.Trim('/')}/runs/{runid}";
                containerName = "github-action-artifacts";
            }

            var container = storage.GetBlobContainerClient(containerName);
             

            console.WriteLine($"Downloading from {destinationPath}");

            var currentFolder = Directory.GetCurrentDirectory().Replace("\\", "/");

        var outPath = Path.Replace("\\", "/");
            var blob = container.GetBlobClient(destinationPath + ".zip");

            if (await blob.ExistsAsync())
            {
              
                using (var archive = new ZipArchive(await blob.OpenReadAsync(true), ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                      
                        using (var w = entry.Open())
                        {
                            using (var f = File.OpenWrite(System.IO.Path.Combine(outPath, entry.FullName)))
                            {
                                await w.CopyToAsync(f);
                            } 
                        }

                    }
                }
            }

            return 0;
            



            
        }
        
    }
    internal sealed class ConsoleHostedService<TApp> : IHostedService where TApp:RootCommand
    {
        private readonly IHostApplicationLifetime appLifetime;
        private readonly TApp app;
        public int Result = 0;
        public ConsoleHostedService(
            IHostApplicationLifetime appLifetime,
            IServiceProvider serviceProvider,
            TApp app)
        {
            this.appLifetime = appLifetime;
            this.app = app;
            //...
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
    return             app.InvokeAsync(System.Environment.GetCommandLineArgs().Skip(1).ToArray())
                .ContinueWith(result =>
                {
                    Result = result.Result;
                    appLifetime.StopApplication();
                    
                });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
           
            return Task.CompletedTask;
        }
    }

    internal class Program
    {
        static   async Task<int> Main(string[] args)
        {
            

            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) => services.AddConsoleApp<App>()
                .AddSingleton<ConsoleHostedService<App>>()
                .AddHostedService(sp=>sp.GetRequiredService<ConsoleHostedService<App>>())
                .AddSingleton<App>()
               // .AddSingleton<Command, UploadCommand>())
                ).Build();
           
          var result=  await host.RunConsoleApp<App>();


            return result;

        }
    }
}