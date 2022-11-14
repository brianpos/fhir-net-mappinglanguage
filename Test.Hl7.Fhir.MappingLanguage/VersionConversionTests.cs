using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using Test.Hl7.Fhir.MappingLanguage;
using Task = System.Threading.Tasks.Task;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class VersionConversionTests
    {
        // From github https://github.com/FHIR/interversion.git
        const string mappinginterversion_folder = @"c:\git\HL7\interversion";
        // const string mappinginterversion_folder = @"e:\git\HL7\interversion";

        public VersionConversionTests()
        {
            var cvr = new CrossVersionResolver();
            CachedResolver source = new CachedResolver(cvr);
            source.Load += Source_Load;
            _source = source;
            _sourceR3 = new CachedResolver(cvr.OnlyStu3);
            (_sourceR3 as CachedResolver).Load += Source_Load;
            _sourceR4 = new CachedResolver(cvr.OnlyR4);
            (_sourceR4 as CachedResolver).Load += Source_Load;
        }

        private void Source_Load(object sender, CachedResolver.LoadResourceEventArgs e)
        {
            if (e.Resource is IConformanceResource cr)
            {
                System.Diagnostics.Trace.WriteLine($"{e.Url} {cr.Name}");
            }
        }

        IResourceResolver _source;
        IResourceResolver _sourceR3;
        IResourceResolver _sourceR4;
        FhirXmlSerializationSettings _xmlSettings = new FhirXmlSerializationSettings() { Pretty = true };
        FhirJsonSerializationSettings _jsonSettings = new FhirJsonSerializationSettings() { Pretty = true };

        [TestMethod]
        public async Task PrepareStu3CoreStructureDefinitions()
        {
            // Download the cross version packages zip file
            // http://fhir.org/packages/xver-packages.zip
            string crossVersionPackages = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FhirMapper");
            if (!Directory.Exists(crossVersionPackages))
                Directory.CreateDirectory(crossVersionPackages);

            string crossVersionPackagesZipFile = Path.Combine(crossVersionPackages, "xver-packages.zip");

            if (!File.Exists(crossVersionPackagesZipFile))
            {
                HttpClient server = new HttpClient();
                var stream = await server.GetStreamAsync("http://fhir.org/packages/xver-packages.zip");
                using (var outStream = File.OpenWrite(crossVersionPackagesZipFile))
                {
                    await stream.CopyToAsync(outStream);
                    await outStream.FlushAsync();
                }
            }
            
            using (var zipStream = File.OpenRead(crossVersionPackagesZipFile))
            {
                ZipArchive archive = new ZipArchive(zipStream);
                foreach (var item in archive.Entries)
                {
                    if (item.Name.EndsWith(".as.r4b.tgz") && !item.Name.StartsWith("."))
                    {
                        System.Diagnostics.Trace.WriteLine($"{item.Name}");
                        var path = Path.Combine(
                            crossVersionPackages,
                            item.Name.Split('.').Skip(2).First());
                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);

                        // Now extract this package into this folder
                        using (var tarStream = new GZipStream(item.Open(), CompressionMode.Decompress))
                        {
                            TarReader r = new TarReader(tarStream);
                            var a = await r.GetNextEntryAsync();
                            while (a != null)
                            {
                                if (!a.Name.StartsWith("package/other/")
                                    && !a.Name.StartsWith("package/openapi/")
                                    && !a.Name.StartsWith("package/xml/")
                                    && a.Name != "package/.index.json")
                                {
                                    // System.Diagnostics.Trace.WriteLine($"{a.Name}");
                                    await a.ExtractToFileAsync(Path.Combine(path, a.Name.Replace("package/", "")), true);
                                }
                                a = await r.GetNextEntryAsync();
                            }
                        }
                    }
                }
            }

            // Instead of modifying the content, have different directory providers
            var v3 = new Firely.Fhir.Packages.PackageReference("hl7.fhir.core", "3.0.2");
            var v4 = new Firely.Fhir.Packages.PackageReference("hl7.fhir.r4b.core", "4.3.0");
            var pc = Firely.Fhir.Packages.PackageClient.Create();
            var cache = new Firely.Fhir.Packages.DiskPackageCache();
            if (!await cache.IsInstalled(v3))
            {
                var pkg = await pc.GetPackage(v3);
                await cache.Install(v3, pkg);
            }
            if (!await cache.IsInstalled(v4))
            {
                var pkg = await pc.GetPackage(v4);
                await cache.Install(v4, pkg);
            }
            DirectorySource stu3 = new DirectorySource(cache.PackageContentFolder(v3));
            DirectorySource r4 = new DirectorySource(cache.PackageContentFolder(v4));
        }

        [TestMethod]
        public void AnalyzeStructureR3ToR4Map()
        {
            var mapText = File.ReadAllText(@$"{mappinginterversion_folder}\r4\R3toR4\StructureMap.map");
            var worker = new TestWorker(_source);
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(mapText, null);

            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            var analysisResult = analyzer.analyse(null, sm);
        }

        [TestMethod]
        public void ExecuteStructureR3ToR4Map_Observation()
        {
            var mapText = File.ReadAllText(@$"{mappinginterversion_folder}\r4\R3toR4\Observation.map");
            var sourceText = File.ReadAllText(@"TestData\observation-example.xml");
            var sourceNode = FhirXmlNode.Parse(sourceText);
            var worker = new TestWorker(_source, @$"{mappinginterversion_folder}\r4\R3toR4");
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(mapText, null);

            IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(_sourceR3);
            IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(_sourceR4);

            var engine = new StructureMapUtilitiesExecute(worker, null, providerTarget);
            var source = engine.GetSourceInput(sm, sourceNode, providerSource);
            var target = engine.GenerateEmptyTargetOutputStructure(sm);

            try
            {
                engine.transform(null, source, sm, target);

                // Just perform a loop and transform it repeatedly!
                //for (int n = 0; n < 1000; n++)
                //{
                //    target = engine.GenerateEmptyTargetOutputStructure(sm);
                //    engine.transform(null, source, sm, target);
                //}
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = target.ToXml(_xmlSettings);
            // var xml2 = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
            System.Diagnostics.Trace.WriteLine(xml2);

            // Add in some assertions for things that break
            var obs = target.ToPoco<Observation>();
            Assert.AreEqual("http://terminology.hl7.org/CodeSystem/observation-category", obs.Category[0].Coding[0].System);
            Assert.AreEqual(4, obs.Code.Coding.Count);
        }

        [TestMethod]
        public void AnalyseAllR3toR4Maps()
        {
            var parser = new StructureMapUtilitiesParse();
            var xs = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
            var worker = new TestWorker(_source);
            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            foreach (var filename in Directory.EnumerateFiles(@$"{mappinginterversion_folder}\r4\R3toR4", "*.map", SearchOption.AllDirectories))
            {
                System.Diagnostics.Trace.WriteLine("-----------------------");
                System.Diagnostics.Trace.WriteLine(filename);
                var mapText = File.ReadAllText(filename);
                try
                {
                    var sm = parser.parse(mapText, null);
                    var xml = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(sm);
                    // System.Diagnostics.Trace.WriteLine(xml);

                    var canonicalFml = StructureMapUtilitiesParse.render(sm);
                    // System.Diagnostics.Trace.WriteLine(canonicalFml);

                    var result2 = parser.parse(canonicalFml, null);
                    var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(result2);

                    var analysisResult = analyzer.analyse(null, sm);
                    Assert.IsTrue(sm.IsExactly(result2));
                }
                catch (FHIRLexerException ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                }
            }
        }
        [TestMethod]
        public void ParseAllR3toR4Maps()
        {
            var parser = new StructureMapUtilitiesParse();
            var xs = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
            foreach (var filename in Directory.EnumerateFiles(@$"{mappinginterversion_folder}\r4\R3toR4", "*.map", SearchOption.AllDirectories))
            {
                System.Diagnostics.Trace.WriteLine("-----------------------");
                System.Diagnostics.Trace.WriteLine(filename);
                var mapText = File.ReadAllText(filename);
                try
                {
                    var sm = parser.parse(mapText, null);
                    var xml = xs.SerializeToString(sm);
                    // System.Diagnostics.Trace.WriteLine(xml);

                    var canonicalFml = StructureMapUtilitiesParse.render(sm);
                    // System.Diagnostics.Trace.WriteLine(canonicalFml);

                    var result2 = parser.parse(canonicalFml, null);
                    var xml2 = xs.SerializeToString(result2);

                    // Assert.IsTrue(sm.IsExactly(result2));
                }
                catch (FHIRLexerException ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                }
            }
        }

        [TestMethod]
        public void RoundTripStructureR3toR4Map()
        {
            var mapText = File.ReadAllText($"{mappinginterversion_folder}\\r4\\R3toR4\\StructureMap.map");
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(mapText, null);

            var xml = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(sm);
            System.Diagnostics.Trace.WriteLine(xml);

            var canonicalFml = StructureMapUtilitiesParse.render(sm);
            System.Diagnostics.Trace.WriteLine(canonicalFml);

            var result2 = parser.parse(canonicalFml, null);
            var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(result2);

            File.WriteAllText(@"c:\temp\sm1.xml", xml);
            File.WriteAllText(@"c:\temp\sm2.xml", xml2);

            // Assert.AreEqual(xml, xml2);
            Assert.IsTrue(sm.IsExactly(result2));
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ConvertAllStu3ExamplesToR4FromZip()
        {
            // Download the examples zip file
            // https://www.hl7.org/fhir/STU3/examples-json.zip
            string examplesFile = @"c:\temp\examples-json.zip";
            string outputR4Folder = @"c:\temp\r4-converted";
            string outputR3Folder = @"c:\temp\r3-converted";
            string outputR3FolderB = @"c:\temp\r3-original";
            if (!Directory.Exists(outputR3Folder))
                Directory.CreateDirectory(outputR3Folder);
            if (!Directory.Exists(outputR3FolderB))
                Directory.CreateDirectory(outputR3FolderB);
            if (!Directory.Exists(outputR4Folder))
                Directory.CreateDirectory(outputR4Folder);

            if (!File.Exists(examplesFile))
            {
                HttpClient server = new HttpClient();
                var stream = await server.GetStreamAsync("https://www.hl7.org/fhir/STU3/examples-json.zip");
                var outStream = File.OpenWrite(examplesFile);
                await stream.CopyToAsync(outStream);
                await outStream.FlushAsync();
            }

            // mapper engine parts
            var workerR3toR4 = new TestWorker(_source, @$"{mappinginterversion_folder}\r4\R3toR4");
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(_source);
            IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(_sourceR3);
            var engine = new StructureMapUtilitiesExecute(workerR3toR4, null, provider);
            var xmlSettings = new FhirXmlSerializationSettings() { Pretty = true };

            // scan all the files in the zip
            var inputPath = ZipFile.OpenRead(examplesFile);
            var files = inputPath.Entries;
            int resourceErrors = 0;
            int resourceConverted = 0;
            foreach (var file in files)
            {
                // skip to the test file we want to check
                //if (file.Name != "capabilitystatement-capabilitystatement-base(base).json")
                //    continue;

                System.Diagnostics.Trace.WriteLine($"{file.Name}");
                var stream = file.Open();
                //if (file.Length > 10000)
                //    continue;
                using (stream)
                {
                    using (var sr = new StreamReader(stream))
                    {
                        ISourceNode sourceNode;
                        var sourceText = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(sourceText);
                        else
                            sourceNode = FhirXmlNode.Parse(sourceText);

                        try
                        {
                            if (!File.Exists($@"{mappinginterversion_folder}\r4\R3toR4\{sourceNode.Name}.map"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }

                            var mapText = File.ReadAllText($@"{mappinginterversion_folder}\r4\R3toR4\{sourceNode.Name}.map");
                            var sm = parser.parse(mapText, null);

                            var target = engine.GenerateEmptyTargetOutputStructure(sm);
                            var source = engine.GetSourceInput(sm, sourceNode, providerSource);

                            // dump the original format of the file (for comparison later)
                            File.WriteAllText(Path.Combine(outputR3FolderB, $"{file.Name.Replace("json", "xml")}"), source.ToXml(xmlSettings));

                            engine.transform(null, source, sm, target);

                            var xml2 = target.ToXml(xmlSettings);
                            // var xml2 = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                            File.WriteAllText(Path.Combine(outputR4Folder, $"{file.Name.Replace("json", "xml")}"), xml2);
                            // System.Diagnostics.Trace.WriteLine(xml2);
                            resourceConverted++;
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine(ex.Message);
                            resourceErrors++;
                        }
                    }
                }
            }
            System.Diagnostics.Trace.WriteLine($"Processed: {files.Count}");
            System.Diagnostics.Trace.WriteLine($"Complete: {resourceConverted}");
            System.Diagnostics.Trace.WriteLine($"Errors: {resourceErrors}");
            Assert.AreEqual(0, resourceErrors);
        }

        [TestMethod]
        public void ConvertAllExamplesR4ToR3()
        {
            string mapFolder = @$"{mappinginterversion_folder}\r4\R4toR3";
            string R4Folder = @"c:\temp\r4-converted";
            string R3Folder = @"c:\temp\r3-converted";
            if (!Directory.Exists(R3Folder))
                Directory.CreateDirectory(R3Folder);
            if (!Directory.Exists(R4Folder))
                Directory.CreateDirectory(R4Folder);

            // mapper engine parts
            var workerR4toR3 = new TestWorker(_source, mapFolder);
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(_sourceR3);
            IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(_sourceR4);
            var engine = new StructureMapUtilitiesExecute(workerR4toR3, null, providerTarget);

            // scan all the files in the zip
            int filesProcessed = 0;
            int resourceErrors = 0;
            int resourceConverted = 0;
            foreach (var file in new DirectoryInfo(R4Folder).EnumerateFiles())
            {
                filesProcessed++;
                // skip to the test file we want to check
                //if (file.Name != "capabilitystatement-capabilitystatement-base(base).json")
                //    continue;

                System.Diagnostics.Trace.WriteLine($"{file.Name}");
                using (var stream = file.Open(FileMode.Open))
                {
                    using (var sr = new StreamReader(stream))
                    {
                        ISourceNode sourceNode;
                        var sourceText = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(sourceText);
                        else
                            sourceNode = FhirXmlNode.Parse(sourceText);

                        try
                        {
                            if (!File.Exists($@"{mapFolder}\{sourceNode.Name}.map"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }
                            var mapText = File.ReadAllText($@"{mapFolder}\{sourceNode.Name}.map");
                            var sm = parser.parse(mapText, null);

                            var target = engine.GenerateEmptyTargetOutputStructure(sm);
                            var source = engine.GetSourceInput(sm, sourceNode, providerSource);
                            engine.transform(null, source, sm, target);

                            var xml = target.ToXml(_xmlSettings);
                            // var xml = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                            File.WriteAllText(Path.Combine(R3Folder, $"{file.Name.Replace("json", "xml")}"), xml);
                            // System.Diagnostics.Trace.WriteLine(xml2);
                            resourceConverted++;
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine(ex.Message);
                            resourceErrors++;
                        }
                    }
                }
            }
            System.Diagnostics.Trace.WriteLine($"Processed: {filesProcessed}");
            System.Diagnostics.Trace.WriteLine($"Complete: {resourceConverted}");
            System.Diagnostics.Trace.WriteLine($"Errors: {resourceErrors}");
            Assert.AreEqual(0, resourceErrors);
        }

        [TestMethod]
        public void ConvertAllExamplesR3ToR4b()
        {
            string mapFolder = @$"{mappinginterversion_folder}\r4\R3toR4";
            string R3Folder = @"c:\temp\r3-converted";
            string R4Folder = @"c:\temp\r4-converted2";
            if (!Directory.Exists(R3Folder))
                Directory.CreateDirectory(R3Folder);
            if (!Directory.Exists(R4Folder))
                Directory.CreateDirectory(R4Folder);

            // mapper engine parts
            var worker = new TestWorker(_source, mapFolder);
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(_sourceR3);
            IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(_sourceR4);
            var engine = new StructureMapUtilitiesExecute(worker, null, providerTarget);

            // scan all the files in the zip
            int filesProcessed = 0;
            int resourceErrors = 0;
            int resourceConverted = 0;
            foreach (var file in new DirectoryInfo(R3Folder).EnumerateFiles())
            {
                filesProcessed++;
                // skip to the test file we want to check
                //if (file.Name != "capabilitystatement-capabilitystatement-base(base).json")
                //    continue;

                System.Diagnostics.Trace.WriteLine($"{file.Name}");
                using (var stream = file.Open(FileMode.Open))
                {
                    using (var sr = new StreamReader(stream))
                    {
                        ISourceNode sourceNode;
                        var sourceText = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(sourceText);
                        else
                            sourceNode = FhirXmlNode.Parse(sourceText);

                        try
                        {
                            if (!File.Exists($@"{mapFolder}\{sourceNode.Name}.map"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }
                            var mapText = File.ReadAllText($@"{mapFolder}\{sourceNode.Name}.map");
                            var sm = parser.parse(mapText, null);

                            var target = engine.GenerateEmptyTargetOutputStructure(sm);
                            var source = engine.GetSourceInput(sm, sourceNode, providerSource);
                            engine.transform(null, source, sm, target);

                            var xml = target.ToXml(_xmlSettings);
                            // var xml = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                            File.WriteAllText(Path.Combine(R4Folder, $"{file.Name.Replace("json", "xml")}"), xml);
                            // System.Diagnostics.Trace.WriteLine(xml2);
                            resourceConverted++;
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine(ex.Message);
                            resourceErrors++;
                        }
                    }
                }
            }
            System.Diagnostics.Trace.WriteLine($"Processed: {filesProcessed}");
            System.Diagnostics.Trace.WriteLine($"Complete: {resourceConverted}");
            System.Diagnostics.Trace.WriteLine($"Errors: {resourceErrors}");
            Assert.AreEqual(0, resourceErrors);
        }
    }
}