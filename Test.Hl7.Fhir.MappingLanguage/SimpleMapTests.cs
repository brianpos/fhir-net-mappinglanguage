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
        public void TestCapStatement()
        {
            // Not a real unit test, but a good test to debug the ElementNode
            // content with a variety of field type.
            var cs = new CapabilityStatement()
            {
                Id = "smile",
                Title = "TitleXXX",
                Name = "HelloName",
                Date = "2022-12-01"
            };
            cs.Text = new Narrative() { Status = Narrative.NarrativeStatus.Generated, Div = "<div>Observation</div>" };
            cs.TitleElement.SetStringExtension("http://example.org/test", "argh");

            var target = cs.ToTypedElement();
            var xml = target.ToXml(_xmlSettings);
            var json = target.ToJson(_jsonSettings);
            System.Diagnostics.Trace.WriteLine(xml);
            System.Diagnostics.Trace.WriteLine(json);

            var en = ElementNode.FromElement(target);
        }

        [TestMethod]
        public void TransformNarrative()
        {
            var mapText = @"
                map ""http://fhirpath-lab.com/fhir/StructureMap/test-primitives"" = ""TestPrimitives""
                uses ""http://hl7.org/fhir/StructureDefinition/Observation"" alias Observation as source
                uses ""http://hl7.org/fhir/StructureDefinition/Observation"" alias Observation as target
                group tutorial(source src : Observation, target tgt : Observation) {
                    src.text -> tgt.text;
                    src.id as a -> tgt.id = a;
                }
                group Narrative(source src : Narrative, target tgt : Narrative) <<type+>> {
                  src.status -> tgt.status;
                  src.div -> tgt.div;
                }
                group xhtml(source src : xhtml, target tgt : xhtml) <<type+>> {
                  src.value as v -> tgt.value = v ""xhtml-value"";
                }
                group code(source src : code, target tgt : code) <<type+>> {
                  src.value as v -> tgt.value = v ""code-value"";
                }
                ";
            var qr = new Observation();
            qr.Text = new Narrative() { Status = Narrative.NarrativeStatus.Generated, Div = "<div xmlns=\"http://www.w3.org/1999/xhtml\">Observation</div>" };
            qr.Id = "idval";

            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(mapText, null);

            var worker = TutorialTests.CreateWorker();
            var provider = new PocoStructureDefinitionSummaryProvider();
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);

            var target = engine.GenerateEmptyTargetOutputStructure(sm);
            engine.transform(null, qr.ToTypedElement(), sm, target);
            System.Diagnostics.Trace.WriteLine(target.ToJson(_jsonSettings));
            var output = target.ToPoco<Observation>();
            var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(output);
            System.Diagnostics.Trace.WriteLine(xml2);
            Assert.AreEqual("idval", output.Id);
            Assert.AreEqual(Narrative.NarrativeStatus.Generated, output.Text.Status);
            Assert.AreEqual(qr.Text.Div, output.Text.Div);
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