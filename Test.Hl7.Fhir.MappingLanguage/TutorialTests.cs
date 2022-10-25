using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class TutorialTests
    {
        private FhirXmlSerializer _xmlSerializer = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
        private FhirXmlParser _xmlParser = new FhirXmlParser();
        private FhirJsonParser _jsonParser = new FhirJsonParser();

        [TestMethod]
        public void Transform_qr2patgender()
        {
            var expression = System.IO.File.ReadAllText("E:\\git\\OpenSource\\fhir-mapping-tutorial-master\\qrtopat\\map\\qr2patgender.map");
            var qr = _jsonParser.Parse<QuestionnaireResponse>(System.IO.File.ReadAllText(@"E:\git\OpenSource\fhir-mapping-tutorial-master\qrtopat\qr.json"));

            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);
            var worker = CreateWorker();
            var engine = new StructureMapUtilitiesExecute(worker);
            var output = new Patient();
            try
            {
                engine.transform(null, qr, sm, output);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = _xmlSerializer.SerializeToString(output);
            System.Diagnostics.Trace.WriteLine(xml2);
        }

        [TestMethod]
        public void Transform_medicationrequest()
        {
            var worker = CreateWorker();
            var expression = System.IO.File.ReadAllText("E:\\git\\OpenSource\\fhir-mapping-tutorial-master\\medicationrequest\\extension.map");
            var qr = _jsonParser.Parse<MedicationRequest>(System.IO.File.ReadAllText(@"E:\git\OpenSource\fhir-mapping-tutorial-master\medicationrequest\source.json"));

            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);
            var engine = new StructureMapUtilitiesExecute(worker);
            var output = new MedicationRequest();
            try
            {
                engine.transform(null, qr, sm, output);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = _xmlSerializer.SerializeToString(output);
            System.Diagnostics.Trace.WriteLine(xml2);
        }

        private static StructureMapUtilitiesAnalyze.IWorkerContext CreateWorker()
        {
            var source = new CachedResolver(new MultiResolver(
                new DirectorySource(@"c:\temp\analyzetests"),
                ZipSource.CreateValidationSource()
                ));
            var worker = new TestWorker(source);
            return worker;
        }
    }
}