using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.FhirPath;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class SimpleMapTests
    {
        FhirXmlSerializationSettings _xmlSettings = new FhirXmlSerializationSettings() { Pretty = true };
        FhirJsonSerializationSettings _jsonSettings = new FhirJsonSerializationSettings() { Pretty = true };

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

            var worker = TutorialTests.CreateWorker();
            var provider = new PocoStructureDefinitionSummaryProvider();
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);
            var output = new Bundle();
            var target = ElementNode.FromElement(output.ToTypedElement());
            try
            {
                engine.transform(null, qr.ToTypedElement(), sm, target);
                target.ToPoco().CopyTo(output);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(output);
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
        public void TestPeriodInvariantStartNoEnd()
        {
            Period p = new Period() { Start = "2022" };
            Assert.IsTrue(p.Predicate("start.hasValue().not() or end.hasValue().not() or (start <= end)"));
        }

        [TestMethod]
        public void TestPeriodInvariantStartAndEnd()
        {
            Period p = new Period() { Start = "2022", End="2022" };
            Assert.IsTrue(p.Predicate("start.hasValue().not() or end.hasValue().not() or (start <= end)"));
        }
    }
}