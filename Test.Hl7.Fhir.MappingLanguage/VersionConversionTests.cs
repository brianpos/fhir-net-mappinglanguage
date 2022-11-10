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
using Test.Hl7.Fhir.MappingLanguage;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class VersionConversionTests
    {
        public VersionConversionTests()
        {
            CachedResolver source = new CachedResolver(
                new CrossVersionResolver()
                );
            source.Load += Source_Load;
            _source = source;
        }

        private void Source_Load(object sender, CachedResolver.LoadResourceEventArgs e)
        {
            if (e.Resource is IConformanceResource cr)
            {
                System.Diagnostics.Trace.WriteLine($"{e.Url} {cr.Name}");
            }
        }

        IResourceResolver _source;

        [TestMethod]
        public void PrepareStu3CoreStructureDefinitions()
        {
            // Instead of modifying the content, have different directory providers
            DirectorySource stu3 = new DirectorySource(@"C:\Users\brian\.fhir\packages\hl7.fhir.r3.core#3.0.2\package");
            DirectorySource r4 = new DirectorySource(@"C:\Users\brian\.fhir\packages\hl7.fhir.r4b.core#4.3.0\package");
            
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

            IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(
                _source,
                (string name, out string canonical) => {
                    // first assume it's a FHIR resource type and use that core content
                    if (ModelInfo.FhirTypeNameToFhirType(name).HasValue)
                    {
                        canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
                        return true;
                    }
                    canonical = null;
                    return false;
                });
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);
            var target = engine.GenerateEmptyTargetOutputStructure(sm);

            try
            {
                engine.transform(null, sourceNode.ToTypedElement(provider), sm, target);
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
    }
}