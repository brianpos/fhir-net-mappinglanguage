using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class SimpleMapTests
    {
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
        public void TransformStructureMap()
        {
            // https://fhir.dk.swisstph-mis.ch/matchbox/fhir/StructureMap/emcarea.registration.p
            var expression = System.IO.File.ReadAllText("C:\\Users\\brian\\Downloads\\structuremap-emcarea.registration.p.map");
            var qr = new QuestionnaireResponse();
            qr.Subject = new ResourceReference("Patient/1", "Brian");
            qr.Encounter = new ResourceReference("Encounter/1", "Social Services");
            qr.Item.Add(new QuestionnaireResponse.ItemComponent()
            {
                LinkId = "emcarerelatedpersoncaregiverid"
            });
            qr.Item[0].Answer.Add(new QuestionnaireResponse.AnswerComponent()
            {
                Value = new FhirString("relper1")
            });

            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);
            var engine = new StructureMapUtilitiesExecute(null);
            var bundle = new Bundle();
            try
            {
                engine.transform(null, qr, sm, bundle);
            }
            catch(System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(bundle);
            System.Diagnostics.Trace.WriteLine(xml2);
        }

        [TestMethod]
        public void RoundTripStructureMap()
        {
            // https://fhir.dk.swisstph-mis.ch/matchbox/fhir/StructureMap/emcarea.registration.p
            var expression = System.IO.File.ReadAllText("C:\\Users\\brian\\Downloads\\structuremap-emcarea.registration.p.map");
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
        public void AnalyzeStructureMap()
        {
            // https://fhir.dk.swisstph-mis.ch/matchbox/fhir/StructureMap/emcarea.registration.p
            var expression = System.IO.File.ReadAllText("C:\\Users\\brian\\Downloads\\structuremap-emcarea.registration.p.map");
            var source = new CachedResolver(new MultiResolver(
                new DirectorySource(@"c:\temp\analyzetests"),
                ZipSource.CreateValidationSource()
                ));
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);
            var worker = new TestWorker(source);
            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            var analysisResult = analyzer.analyse(null, sm);
        }

        [TestMethod]
        public void AnalyzeStructureR3ToR4Map()
        {
            var expression = System.IO.File.ReadAllText("E:\\git\\HL7\\fhir-core-build-r5-PA\\implementations\\r3maps\\R3toR4\\StructureMap.map");
            var source = new CachedResolver(new MultiResolver(
                new DirectorySource(@"c:\temp\analyzetests"),
                ZipSource.CreateValidationSource()
                ));
            var worker = new TestWorker(source);
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);

            var analyzer = new StructureMapUtilitiesAnalyze(worker);
            var analysisResult = analyzer.analyse(null, sm);
        }

        [TestMethod]
        public void AnalyseAllR3toR4Maps()
        {
            var parser = new StructureMapUtilitiesParse();
            var xs = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
            var source = new CachedResolver(new MultiResolver(
                new DirectorySource(@"c:\temp\analyzetests"),
                ZipSource.CreateValidationSource()
                ));
            var worker = new TestWorker(source);
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
    }
}