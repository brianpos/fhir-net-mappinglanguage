using Firely.Fhir.Packages;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Test.Hl7.Fhir.MappingLanguage;
using Task = System.Threading.Tasks.Task;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class VersionConversionTests
    {
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

        [TestMethod]
        public async Task PrepareStu3CoreStructureDefinitions()
        {
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
            var expression = System.IO.File.ReadAllText("E:\\git\\HL7\\fhir-core-build-r5-PA\\implementations\\r3maps\\R3toR4\\StructureMap.map");
            var worker = new TestWorker(_source);
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);

            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            var analysisResult = analyzer.analyse(null, sm);
        }

        [TestMethod]
        public void ExecuteStructureR3ToR4Map()
        {
            var expression = System.IO.File.ReadAllText(@"E:\git\HL7\interversion\r4\R3toR4\Observation.map");
            var source3 = System.IO.File.ReadAllText(@"c:\temp\observation-example.xml");
            var sourceNode = FhirXmlNode.Parse(source3);
            var worker = new TestWorker(_source, @"E:\git\HL7\interversion\r4\R3toR4");
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);

            IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(
                _source,
                (string name, out string canonical) =>
                {
                    // first assume it's a FHIR resource type and use that core content
                    if (ModelInfo.FhirTypeNameToFhirType(name).HasValue)
                    {
                        canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
                        return true;
                    }
                    canonical = null;
                    return false;
                });

            IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(
                _source,
                (string name, out string canonical) =>
                {
                    // first assume it's a FHIR resource type and use that core content
                    if (ModelInfo.FhirTypeNameToFhirType(name).HasValue)
                    {
                        canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
                        return true;
                    }
                    canonical = null;
                    return false;
                });

            var engine = new StructureMapUtilitiesExecute(worker, null, providerTarget);
            var target = engine.GenerateEmptyTargetOutputStructure(sm);

            try
            {
                engine.transform(null, sourceNode.ToTypedElement(providerSource), sm, target);

                // Just perform a loop and transform it repeatedly!
                for (int n = 0; n < 2000; n++)
                {
                    target = engine.GenerateEmptyTargetOutputStructure(sm);
                    engine.transform(null, sourceNode.ToTypedElement(providerSource), sm, target);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = target.ToXml(new FhirXmlSerializationSettings() { Pretty = true });
            // var xml2 = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
            System.Diagnostics.Trace.WriteLine(xml2);

        }

        [TestMethod]
        public void AnalyseAllR3toR4Maps()
        {
            var parser = new StructureMapUtilitiesParse();
            var xs = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
            var worker = new TestWorker(_source);
            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            foreach (var filename in System.IO.Directory.EnumerateFiles("E:\\git\\HL7\\fhir-core-build-r5-PA\\implementations\\r3maps", "*.map", System.IO.SearchOption.AllDirectories))
            {
                System.Diagnostics.Trace.WriteLine("-----------------------");
                System.Diagnostics.Trace.WriteLine(filename);
                var expression = System.IO.File.ReadAllText(filename);
                try
                {
                    var sm = parser.parse(expression, null);

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
            foreach (var filename in System.IO.Directory.EnumerateFiles("E:\\git\\HL7\\fhir-core-build-r5-PA\\implementations\\r3maps", "*.map", System.IO.SearchOption.AllDirectories))
            {
                System.Diagnostics.Trace.WriteLine("-----------------------");
                System.Diagnostics.Trace.WriteLine(filename);
                var expression = System.IO.File.ReadAllText(filename);
                try
                {
                    var sm = parser.parse(expression, null);

                    var xml = xs.SerializeToString(sm);
                    // System.Diagnostics.Trace.WriteLine(xml);

                    var canonicalFml = StructureMapUtilitiesParse.render(sm);
                    // System.Diagnostics.Trace.WriteLine(canonicalFml);

                    var result2 = parser.parse(canonicalFml, null);
                    var xml2 = xs.SerializeToString(result2);

                    Assert.IsTrue(sm.IsExactly(result2));
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
            var expression = System.IO.File.ReadAllText("E:\\git\\HL7\\fhir-core-build-r5-PA\\implementations\\r3maps\\R3toR4\\StructureMap.map");
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);

            var xml = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(sm);
            System.Diagnostics.Trace.WriteLine(xml);

            var canonicalFml = StructureMapUtilitiesParse.render(sm);
            System.Diagnostics.Trace.WriteLine(canonicalFml);

            var result2 = parser.parse(canonicalFml, null);
            var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(result2);

            System.IO.File.WriteAllText(@"c:\temp\sm1.xml", xml);
            System.IO.File.WriteAllText(@"c:\temp\sm2.xml", xml2);

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
            if (!System.IO.Directory.Exists(outputR3Folder))
                System.IO.Directory.CreateDirectory(outputR3Folder);
            if (!System.IO.Directory.Exists(outputR3FolderB))
                System.IO.Directory.CreateDirectory(outputR3FolderB);
            if (!System.IO.Directory.Exists(outputR4Folder))
                System.IO.Directory.CreateDirectory(outputR4Folder);

            if (!System.IO.File.Exists(examplesFile))
            {
                HttpClient server = new HttpClient();
                var stream = await server.GetStreamAsync("https://www.hl7.org/fhir/STU3/examples-json.zip");
                var outStream = File.OpenWrite(examplesFile);
                await stream.CopyToAsync(outStream);
                await outStream.FlushAsync();
            }

            // mapper engine parts
            var workerR3toR4 = new TestWorker(_source, @"E:\git\HL7\interversion\r4\R3toR4");
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
                        var source3 = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(source3);
                        else
                            sourceNode = FhirXmlNode.Parse(source3);

                        try
                        {
                            if (!System.IO.File.Exists($@"E:\git\HL7\interversion\r4\R3toR4\{sourceNode.Name}.map"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }

                            var expression = System.IO.File.ReadAllText($@"E:\git\HL7\interversion\r4\R3toR4\{sourceNode.Name}.map");
                            var sm = parser.parse(expression, null);

                            var target = engine.GenerateEmptyTargetOutputStructure(sm);
                            var sourceUrl = engine.GetSourceInputStructure(sm);
                            var source = sourceNode.ToTypedElement(providerSource, sourceUrl);
                            // var sd = _source.ResolveByCanonicalUri(sourceUrl) as StructureDefinition;

                            // dump the original format of the file (for comparison later)
                            File.WriteAllText(Path.Combine(outputR3FolderB, $"{file.Name.Replace("json", "xml")}"), source.ToXml(xmlSettings));

                            engine.transform(null, source, sm, target);

                            var xml2 = target.ToXml(xmlSettings);
                            // var xml2 = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                            System.IO.File.WriteAllText(Path.Combine(outputR4Folder, $"{file.Name.Replace("json", "xml")}"), xml2);
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
            string mapFolder = @"E:\git\HL7\interversion\r4\R4toR3";
            string R4Folder = @"c:\temp\r4-converted";
            string R3Folder = @"c:\temp\r3-converted";
            if (!System.IO.Directory.Exists(R3Folder))
                System.IO.Directory.CreateDirectory(R3Folder);
            if (!System.IO.Directory.Exists(R4Folder))
                System.IO.Directory.CreateDirectory(R4Folder);

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
                        var source = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(source);
                        else
                            sourceNode = FhirXmlNode.Parse(source);

                        try
                        {
                            if (!System.IO.File.Exists($@"{mapFolder}\{sourceNode.Name}.map"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }
                            var expression = System.IO.File.ReadAllText($@"{mapFolder}\{sourceNode.Name}.map");
                            var sm = parser.parse(expression, null);

                            var target = engine.GenerateEmptyTargetOutputStructure(sm);
                            var sourceUrl = engine.GetSourceInputStructure(sm);

                            engine.transform(null, sourceNode.ToTypedElement(providerSource, sourceUrl), sm, target);

                            var xml = target.ToXml(new FhirXmlSerializationSettings() { Pretty = true });
                            // var xml = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                            System.IO.File.WriteAllText(Path.Combine(R3Folder, $"{file.Name.Replace("json", "xml")}"), xml);
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
            string mapFolder = @"E:\git\HL7\interversion\r4\R3toR4";
            string R3Folder = @"c:\temp\r3-converted";
            string R4Folder = @"c:\temp\r4-converted2";
            if (!System.IO.Directory.Exists(R3Folder))
                System.IO.Directory.CreateDirectory(R3Folder);
            if (!System.IO.Directory.Exists(R4Folder))
                System.IO.Directory.CreateDirectory(R4Folder);

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
                        var source = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(source);
                        else
                            sourceNode = FhirXmlNode.Parse(source);

                        try
                        {
                            if (!System.IO.File.Exists($@"{mapFolder}\{sourceNode.Name}.map"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }
                            var expression = System.IO.File.ReadAllText($@"{mapFolder}\{sourceNode.Name}.map");
                            var sm = parser.parse(expression, null);

                            var target = engine.GenerateEmptyTargetOutputStructure(sm);
                            var sourceUrl = engine.GetSourceInputStructure(sm);

                            engine.transform(null, sourceNode.ToTypedElement(providerSource, sourceUrl), sm, target);

                            var xml = target.ToXml(new FhirXmlSerializationSettings() { Pretty = true });
                            // var xml = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                            System.IO.File.WriteAllText(Path.Combine(R4Folder, $"{file.Name.Replace("json", "xml")}"), xml);
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