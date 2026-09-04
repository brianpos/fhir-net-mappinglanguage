using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
        const string mappinginterversion_folder = @"C:\git\hl7\fhir-cross-version\input";
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
			_sourceR5 = new CachedResolver(cvr.OnlyR4);
			(_sourceR5 as CachedResolver).Load += Source_Load;
		}

        void SetFallbackResolver(IResourceResolver resolver)
        {
            ((_source as CachedResolver).Source as CrossVersionResolver).fallbackResolver = resolver;
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
		IResourceResolver _sourceR5;
		FhirXmlSerializationSettings _xmlSettings = new FhirXmlSerializationSettings() { Pretty = true };
        FhirJsonSerializationSettings _jsonSettings = new FhirJsonSerializationSettings() { Pretty = true };

		[TestMethod]
        public void AnalyzeStructureR3ToR4Map()
        {
            var mapText = File.ReadAllText(@$"{mappinginterversion_folder}\R3toR4\StructureMap.fml");
            var worker = new TestWorker(_source);
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(mapText, null);

            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            var analysisResult = analyzer.analyse(null, sm);
        }

        [TestMethod]
        public void ExecuteStructureR3ToR4Map_Observation()
        {
            var mapText = File.ReadAllText(@$"{mappinginterversion_folder}\R3toR4\Observation.fml");
            var sourceText = File.ReadAllText(@"TestData\observation-example.xml");
            var sourceNode = FhirXmlNode.Parse(sourceText);
            var worker = new TestWorker(_source, @$"{mappinginterversion_folder}\R3toR4");
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(mapText, null);

            IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(_sourceR3);
            IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(_sourceR4);

            var services = new UnitTestFmlEngineServices(providerTarget);
            var engine = new StructureMapUtilitiesExecute(worker, services, providerTarget);
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
            Assert.IsTrue(xml2.Contains("<extension url=\"http://example.org/testme\">"));
            Assert.IsTrue(xml2.Contains("<valueString value=\"test\" />"));
        }

        [TestMethod]
        public void AnalyseAllR3toR4Maps()
        {
            var parser = new StructureMapUtilitiesParse();
            var xs = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
            var worker = new TestWorker(_source);
            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            foreach (var filename in Directory.EnumerateFiles(@$"{mappinginterversion_folder}\R3toR4", "*.fml", SearchOption.AllDirectories))
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

        /// <summary>
        /// Parse all the maps in the given folder
        /// </summary>
        /// <param name="versionMapFolder"></param>
        /// <param name="onlyRunFile">Optional parameter to make testing single files across versions quicker during development</param>
		public void ParseAllMaps(string versionMapFolder, string onlyRunFile = null)
		{
			var parser = new StructureMapUtilitiesParse();
			var xs = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
			foreach (var filename in Directory.EnumerateFiles(@$"{mappinginterversion_folder}\{versionMapFolder}", "*.fml", SearchOption.AllDirectories))
			{
                if (onlyRunFile != null && !filename.EndsWith(onlyRunFile, StringComparison.OrdinalIgnoreCase)) continue;
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
		[DataRow("R2toR3")]
		[DataRow("R3toR2")]
		[DataRow("R3toR4")]
		[DataRow("R4toR3")]
		[DataRow("R4toR5")]
		[DataRow("R5toR4")]
		[DataRow("R4BtoR5")]
		[DataRow("R5toR4B")]
		public void ParseAllCrossVersionMaps(string versionMapFolder)
		{
			ParseAllMaps(versionMapFolder);
		}

		[TestMethod]
        public void RoundTripStructureR3toR4Map()
        {
            var mapText = File.ReadAllText($"{mappinginterversion_folder}\\R3toR4\\StructureMap.fml");
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

        class ResourceTestResult
        {
            public int resourceErrors = 0;
            public int validationErrors = 0;
            public int resourceConverted = 0;
            public int resourceConvertedBack = 0;
            public int identicalXml = 0;
            public int identicalJson = 0;
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ConvertAllStu3ExamplesToR4FromZip()
        {
            // This test will end up producing an error report like the one at https://www.hl7.org/fhir/r3maps.html
            Dictionary<string, ResourceTestResult> results = new Dictionary<string, ResourceTestResult>();

            // Download the examples zip file
            // https://www.hl7.org/fhir/STU3/examples-json.zip
            string examplesFile = @"c:\temp\examples-json.zip";
            if (!File.Exists(examplesFile))
            {
                HttpClient server = new HttpClient();
                var stream = await server.GetStreamAsync("https://www.hl7.org/fhir/STU3/examples-json.zip");
                var outStream = File.OpenWrite(examplesFile);
                await stream.CopyToAsync(outStream);
                await outStream.FlushAsync();
            }

            // mapper engine parts
            var workerR3toR4 = new TestWorker(_source, @$"{mappinginterversion_folder}\R3toR4");
            var workerR4toR3 = new TestWorker(_source, @$"{mappinginterversion_folder}\R4toR3");
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider providerR4 = new StructureDefinitionSummaryProvider(_sourceR4);
            IStructureDefinitionSummaryProvider providerR3 = new StructureDefinitionSummaryProvider(_sourceR3);
            var services3to4 = new UnitTestFmlEngineServices(providerR4);
            var services4to3 = new UnitTestFmlEngineServices(providerR3);
            var engine3to4 = new StructureMapUtilitiesExecute(workerR3toR4, services3to4, providerR4);
            var engine4to3 = new StructureMapUtilitiesExecute(workerR4toR3, services4to3, providerR3);
            SetFallbackResolver(_sourceR4);

            var validator = new Validator(new ValidationSettings() { ResourceResolver = _source });

            // scan all the files in the zip
            var inputPath = ZipFile.OpenRead(examplesFile);
            var files = inputPath.Entries;
            //int resourceErrors = 0;
            //int validationErrors = 0;
            //int resourceConverted = 0;
            //int resourceConvertedBack = 0;
            //int identicalXml = 0;
            //int identicalJson = 0;
            foreach (var file in files)
            {
                // skip to the test file we want to check
                //if (file.Name != "capabilitystatement-capabilitystatement-base(base).json")
                //    continue;
                System.Diagnostics.Trace.WriteLine($"{file.Name}");
                using (var stream = file.Open())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        ISourceNode sourceNode;
                        var sourceText = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(sourceText);
                        else
                            sourceNode = FhirXmlNode.Parse(sourceText);

                        ResourceTestResult itemResult;
                        if (!results.ContainsKey(sourceNode.Name))
                        {
                            itemResult = new ResourceTestResult();
                            results.Add(sourceNode.Name, itemResult);
                        }
                        itemResult = results[sourceNode.Name];

                        try
                        {
                            // Convert up to R4
                            if (!File.Exists($@"{mappinginterversion_folder}\R3toR4\{sourceNode.Name}.fml"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }

                            var mapText = File.ReadAllText($@"{mappinginterversion_folder}\R3toR4\{sourceNode.Name}.fml");
                            var sm = parser.parse(mapText, null);
                            var source = engine3to4.GetSourceInput(sm, sourceNode, providerR3);
                            var target = engine3to4.GenerateEmptyTargetOutputStructure(sm);
                            engine3to4.transform(null, source, sm, target);

                            var xmlR4 = target.ToXml(_xmlSettings);
                            var jsonR4 = target.ToJson(_jsonSettings);

                            // Validate this content as R4
                            // var poco = target.ToPoco<Resource>(new PocoBuilderSettings() { AllowUnrecognizedEnums = true });
                            var output = validator.Validate(target);
                            if (!output.Success)
                            {
                                itemResult.validationErrors++;
                                System.Diagnostics.Trace.WriteLine(output.ToXml(_xmlSettings));
                            }
                            itemResult.resourceConverted++;

                            // Convert back down to STU3
                            if (!File.Exists($@"{mappinginterversion_folder}\R4toR3\{target.Name}.fml"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({target.Name}) that has no backward map");
                                continue;
                            }
                            mapText = File.ReadAllText($@"{mappinginterversion_folder}\R4toR3\{sourceNode.Name}.fml");
                            sm = parser.parse(mapText, null);
                            var targetR3 = engine4to3.GenerateEmptyTargetOutputStructure(sm);
                            engine4to3.transform(null, target, sm, targetR3);

                            itemResult.resourceConvertedBack++;

                            // and compare the results!
                            var sourceXmlR3 = source.ToXml(_xmlSettings);
                            var sourceJsonR3 = source.ToJson(_jsonSettings);
                            var targetXmlR3 = targetR3.ToXml(_xmlSettings);
                            var targetJsonR3 = targetR3.ToJson(_jsonSettings);

                            if (sourceXmlR3 == targetXmlR3)
                                itemResult.identicalXml++;
                            if (sourceJsonR3 == targetJsonR3)
                                itemResult.identicalJson++;
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine(ex.Message);
                            itemResult.resourceErrors++;
                        }
                    }
                }
            }
            System.Diagnostics.Trace.WriteLine($"Processed: {files.Count}");
            System.Diagnostics.Trace.WriteLine($"Complete:  {results.Values.Sum(v => v.resourceConverted)}");
            System.Diagnostics.Trace.WriteLine($"And back:  {results.Values.Sum(v => v.resourceConvertedBack)}");
            System.Diagnostics.Trace.WriteLine($"Same xml:  {results.Values.Sum(v => v.identicalXml)}");
            System.Diagnostics.Trace.WriteLine($"Same json: {results.Values.Sum(v => v.identicalJson)}");
            System.Diagnostics.Trace.WriteLine($"Validation:{results.Values.Sum(v => v.validationErrors)}");
            System.Diagnostics.Trace.WriteLine($"Exceptions:{results.Values.Sum(v => v.resourceErrors)}");

            // Now output the table!
            foreach (var r in results)
            {
                System.Diagnostics.Trace.WriteLine($"{r.Key}\t{r.Value.resourceConverted}\t{r.Value.identicalXml},{r.Value.identicalJson}\t{(r.Value.resourceConverted-r.Value.validationErrors)/ r.Value.resourceConverted}\t{r.Value.validationErrors}");
            }

            Assert.AreEqual(0, results.Values.Sum(v => v.resourceErrors));
        }

        [TestMethod]
        public void ConvertAllExamplesR4ToR3()
        {
            string mapFolder = @$"{mappinginterversion_folder}\R4toR3";
            string R4Folder = @"c:\temp\r4-converted";
            string R3Folder = @"c:\temp\r3-converted";
            if (!Directory.Exists(R3Folder))
                Directory.CreateDirectory(R3Folder);
            if (!Directory.Exists(R4Folder))
                Directory.CreateDirectory(R4Folder);

            // mapper engine parts
            var workerR4toR3 = new TestWorker(_source, mapFolder);
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(_sourceR4);
            IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(_sourceR3);
            var services = new UnitTestFmlEngineServices(providerTarget);
            var engine = new StructureMapUtilitiesExecute(workerR4toR3, services, providerTarget);
            SetFallbackResolver(_sourceR3);

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
                        try
                        {
                            ISourceNode sourceNode;
                            var sourceText = sr.ReadToEnd();
                            if (file.Name.EndsWith(".json"))
                                sourceNode = FhirJsonNode.Parse(sourceText);
                            else
                                sourceNode = FhirXmlNode.Parse(sourceText);

                            if (!File.Exists($@"{mapFolder}\{sourceNode.Name}.fml"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }
                            var mapText = File.ReadAllText($@"{mapFolder}\{sourceNode.Name}.fml");
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
            string mapFolder = @$"{mappinginterversion_folder}\R3toR4";
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
            var services = new UnitTestFmlEngineServices(providerTarget);
            var engine = new StructureMapUtilitiesExecute(worker, services, providerTarget);

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
                            if (!File.Exists($@"{mapFolder}\{sourceNode.Name}.fml"))
                            {
                                System.Diagnostics.Trace.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }
                            var mapText = File.ReadAllText($@"{mapFolder}\{sourceNode.Name}.fml");
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