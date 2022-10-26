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

            DirectorySystemService<System.IServiceProvider>.Directory = @"c:\temp\demo-map-server";
            if (!System.IO.Directory.Exists(DirectorySystemService<System.IServiceProvider>.Directory))
                System.IO.Directory.CreateDirectory(DirectorySystemService<System.IServiceProvider>.Directory);

            builder.Services.AddSingleton<IFhirSystemServiceR4<IServiceProvider>>((s) =>
            {
                var result = new MapServerSystemService();
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

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.Run();
        }
    }
}