using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.FhirPath;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class LloydMultiVarCrash
    {
		private StructureMap GetMap()
		{
			const string resourceMap = """
            /// name = "SDOHCC_ACC_HSRN_Map"
            /// status = draft
            /// title = "SDOHCC Accountable Health Communities (AHC) Health-related Social Needs Screening (HSRN) QuestionnaireResponse mapping"
            /// description = "A StructureMap instance that transforms answers to a Accountable Health Communities (AHC) Health-related Social Needs Screening (HSRN) questionnaire into a transaction Bundle creating corresponding Observation and derived Condition instances."

            map "http://hl7.org/fhir/us/sdoh-clinicalcare/StructureMap/SDOHCC-ACC-HSRN-Map" = "SDOHCC_ACC_HSRN_Map"

            uses "http://hl7.org/fhir/StructureDefinition/QuestionnaireResponse" alias questionnaireResponse as source
            uses "http://hl7.org/fhir/StructureDefinition/Bundle" alias bundle as target
            uses "http://hl7.org/fhir/StructureDefinition/Condition" alias sdohccCondition as target

            group sdohMapping(source src : questionnaireResponse, target bundle : Bundle) {
              src -> bundle.id = uuid() "bundleId";
              src -> bundle.type = 'transaction' "bundleType";

              src.item as item where (linkId = '/71802-3') then
              {
                item.answer as itemAnswer -> bundle.entry as entry, entry.resource = create('Condition') as condition then
                  TransformCondition(src, bundle, item, itemAnswer, condition, entry) "cond1";
              } "condGroupA";
            }


            // Generic condition creation from Questionnaire item transform
            group TransformCondition(source src: QuestionnaireResponse, source bundle: Bundle, source item, source itemAnswer, target condition: Condition, target entry)
            {
              // Set the ID to a composite value: static text + linkID + answer value.
              // THIS HERE IS WHERE THE BUG IS, having multiple input variables here leads to the cartesian product of them all.
              // In this case we will have 1 linkId in the first variable, then n answers to the question, each joining with that first link ID
              // (the actual use in question has the same functionality for several linkIds, hence not as forced as this is)

              // item.linkId as itemId, itemAnswer -> condition.id = ('condition_' + itemId.substring(1) + '_' + itemAnswer.code.value) then
              item.linkId as itemId log('linkId' & %item.linkId), itemAnswer.value as valCoding, itemAnswer log('answerValue: ' & %itemAnswer.value.code) -> condition.id = evaluate(itemAnswer, ('condition_' & value.code)) then
                SetConditionFullUrl(condition, entry) "conditionId";

              // Follow up question: should the first log here have the variable %itemId available to it, or just the input variables as I've used here

              // src -> condition.code = cc('http://snomed.info/sct', '1156191002', 'Housing instability (finding)') "conditionCode1";
            }

            group SetConditionFullUrl(source condition: Condition, target entry)
            {
              condition.id as id -> entry.fullUrl = append('http://hl7.org/fhir/us/sdoh-clinicalcare/Condition/', id);
            }
            """;
			var parser = new StructureMapUtilitiesParse();
			var sm = parser.parse(resourceMap, null);
			return sm;
		}

		private QuestionnaireResponse GetTestResource()
        {
			const string resourceJson = """
            {
              "resourceType": "QuestionnaireResponse",
              "id": "MultiVarBug-QR",
              "meta": {
                "versionId": "1",
                "lastUpdated": "2023-10-13T05:13:41.947+00:00",
                "source": "#cIElrgX87hsvj2GB"
              },
              "questionnaire": "http://someurl.org",
              "status": "completed",
              "subject": {
                "reference": "Patient/example",
                "display": "COLIN ABBAS"
              },
              "authored": "2021-04-26T13:56:33.747Z",
              "item": [ {
                "linkId": "/71802-3",
                "answer": [ {
                  "valueCoding": {
                    "system": "http://loinc.org",
                    "code": "LA31994-9"
                  }
                }, {
                  "valueCoding": {
                    "system": "http://loinc.org",
                    "code": "LA31995-6"
                  }
                }, {
                  "valueCoding": {
                    "system": "http://loinc.org",
                    "code": "LA31995-3"
                  }
                }
                ]
              } ]
            }
            """;
			var rp = new FhirJsonParser();
            return rp.Parse<QuestionnaireResponse>(resourceJson);
        }

        private CachedResolver GetSource()
        {
            var source = new CachedResolver(ZipSource.CreateValidationSource());
            source.Load += Source_Load;
            return source;
        }

        private void Source_Load(object sender, CachedResolver.LoadResourceEventArgs e)
        {
            if (e.Resource is StructureDefinition sd)
            {
                if (!sd.HasSnapshot)
                {
                    SnapshotGenerator sg = new SnapshotGenerator(sender as IResourceResolver);
                    sg.Update(sd);
                }

            }
        }

        [TestMethod]
        public void TransformLloydsDualInputVar()
        {
            var sm = GetMap();
            var qr = GetTestResource();
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
                var xml2 = new FhirXmlSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(output);
                System.Diagnostics.Trace.WriteLine(xml2);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }
    }
}