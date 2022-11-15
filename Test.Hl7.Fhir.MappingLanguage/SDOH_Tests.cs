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
    public class SDOH_Tests
    {
        // From github https://github.com/HL7/fhir-sdoh-clinicalcare.git
        const string pathSDOHClinicalCare = @"E:\git\HL7\fhir-sdoh-clinicalcare";
        // const string pathSDOHClinicalCare = @"c:\git\HL7\fhir-sdoh-clinicalcare";

        private StructureMap GetFromMapFile(string relpath)
        {
            var resourceMap = System.IO.File.ReadAllText($"{pathSDOHClinicalCare}{relpath}");
            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(resourceMap, null);
            return sm;
        }

        private T GetFhirResourceFromJson<T>(string relpath)
            where T : Resource, new()
        {
            var resourceJson = System.IO.File.ReadAllText($"{pathSDOHClinicalCare}{relpath}");
            var rp = new FhirJsonParser();
            return rp.Parse<T>(resourceJson);
        }
        private T GetFhirResourceFromXml<T>(string relpath)
            where T : Resource, new()
        {
            var resourceJson = System.IO.File.ReadAllText($"{pathSDOHClinicalCare}{relpath}");
            var rp = new FhirXmlParser();
            return rp.Parse<T>(resourceJson);
        }
        private CachedResolver GetSource()
        {
            var ds = new DirectorySourceSettings()
            {
                IncludeSubDirectories = false,
                Mask = "*.xml"
            };
            return new CachedResolver(
                new MultiResolver(
                    new DirectorySource($"{pathSDOHClinicalCare}\\output", ds),
                    ZipSource.CreateValidationSource()));
        }

        [TestMethod]
        public void TransformPrapareMap()
        {
            var sm = GetFromMapFile(@"\input\map-source\SDOHCC-PRAPARE-Map.map");
            var qr = GetFhirResourceFromXml<QuestionnaireResponse>(@"\input\resources\questionnaireresponse\SDOHCC-QuestionnaireResponsePRAPAREExample.xml");

            var source = GetSource();
            var worker = new TestWorker(source);
            var provider = new StructureDefinitionSummaryProvider(source);
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);
            var target = engine.GenerateEmptyTargetOutputStructure(sm);
            Resource output = null;
            try
            {
                engine.transform(null, qr.ToTypedElement(), sm, target);
                var temp = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                System.Diagnostics.Trace.WriteLine(temp);
                output = target.ToPoco() as Resource;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(output);
            System.Diagnostics.Trace.WriteLine(xml2);
        }

        [TestMethod]
        public void TransformVitalsMap()
        {
            var sm = GetFromMapFile(@"\input\map-source\SDOHCC-Hunger-Vital-Sign-Map.map");
            var qr = GetFhirResourceFromXml<QuestionnaireResponse>(@"\input\resources\questionnaireresponse\SDOHCC-QuestionnaireResponseHungerVitalSignExample.xml");

            var source = GetSource();
            var worker = new TestWorker(source);
            var provider = new StructureDefinitionSummaryProvider(source);
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);
            var target = engine.GenerateEmptyTargetOutputStructure(sm);
            Resource output = null;
            try
            {
                engine.transform(null, qr.ToTypedElement(), sm, target);
                output = target.ToPoco() as Resource;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(output);
            System.Diagnostics.Trace.WriteLine(xml2);

        }
    }
}