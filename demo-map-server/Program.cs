using Hl7.Fhir.DemoFileSystemFhirServer;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.NetCoreApi;
using Hl7.Fhir.Rest;
using Hl7.Fhir.WebApi;
using Hl7.FhirPath;

namespace demo_map_server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // initialize the fhirpath FHIR extensions
            FhirPathCompiler.DefaultSymbolTable.AddFhirExtensions();

            var appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            appFolder = Path.Combine(appFolder, "demo-map-server", "data");
            DirectorySystemService<System.IServiceProvider>.Directory = appFolder;
            // DirectorySystemService<System.IServiceProvider>.Directory = @"c:\temp\demo-map-server";
            if (!System.IO.Directory.Exists(DirectorySystemService<System.IServiceProvider>.Directory))
                System.IO.Directory.CreateDirectory(DirectorySystemService<System.IServiceProvider>.Directory);


            // CORS Support
            builder.Services.AddCors(o => o.AddDefaultPolicy(builder =>
            {
                // Better to use with Origins to only permit locations that we really trust
                builder.AllowAnyOrigin();
                // builder.WithOrigins(settings.AllowedOrigins);
                builder.AllowAnyHeader();
                builder.AllowAnyMethod();
                // builder.AllowCredentials();
                builder.WithExposedHeaders(new[] { "Content-Location", "Location", "ETag" });
            }));

            builder.Services.AddSingleton<IFhirSystemServiceR4<IServiceProvider>>((s) =>
            {
                var result = new Services.MapServerSystemService();
                result.InitializeIndexes();
                return result;
            });
            builder.Services.UseFhirServerController(options =>
            {
                // FML formatters
                options.InputFormatters.Insert(0, new FhirMappingLanugageInputFormatter());
                options.OutputFormatters.Add(new FhirMappingLanugageOutputFormatter());

                // HTML formatter
                options.OutputFormatters.Add(new SimpleHtmlFhirOutputFormatter());

                // StructureMap transform input/output formatter
                options.Filters.Add(new StructureMapTransform.StructureMapTransformFilter());
            });

            var app = builder.Build();
            app.UseCors();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.Run();
        }
    }
}