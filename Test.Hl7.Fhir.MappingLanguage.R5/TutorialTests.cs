using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class TutorialTests
    {
        private FhirXmlSerializer _xmlSerializer = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
        private FhirXmlParser _xmlParser = new FhirXmlParser();
        private FhirJsonParser _jsonParser = new FhirJsonParser();
        const string mappingtutorial_folder = @"C:\git\hl7\fhir-mapping-tutorial";
        // const string mappingtutorial_folder = @"E:\git\OpenSource\fhir-mapping-tutorial-master";

        internal static StructureMapUtilitiesAnalyze.IWorkerContext CreateWorker()
        {
            var source = new CachedResolver(new MultiResolver(
                new DirectorySource(@"c:\temp\analyzetests"),
                ZipSource.CreateValidationSource()
                ));
            source.Load += Source_Load;
            var worker = new TestWorker(source);
            return worker;
        }

        private static void Source_Load(object sender, CachedResolver.LoadResourceEventArgs e)
        {
            if (e.Resource is StructureDefinition sd)
            {
                sd.Abstract = false;
                if (sd.Snapshot == null)
                {
                    sd.Snapshot = new StructureDefinition.SnapshotComponent();
                    sd.Snapshot.Element.AddRange(sd.Differential.Element);
                }
            }
        }

        [TestMethod]
        public void Transform_qr2patgender()
        {
            var expression = System.IO.File.ReadAllText(@$"{mappingtutorial_folder}\qrtopat\map\qr2patgender.map");
            var qr = _jsonParser.Parse<QuestionnaireResponse>(System.IO.File.ReadAllText(@$"{mappingtutorial_folder}\qrtopat\qr.json"));

            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);
            var worker = CreateWorker();
            var provider = new PocoStructureDefinitionSummaryProvider();
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);
            var output = new Patient();
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
            var xml2 = _xmlSerializer.SerializeToString(output);
            System.Diagnostics.Trace.WriteLine(xml2);
        }

        [TestMethod]
        public void Transform_medicationrequest()
        {
            var worker = CreateWorker();
            var expression = System.IO.File.ReadAllText(@$"{mappingtutorial_folder}\medicationrequest\extension.map");
            var qr = _jsonParser.Parse<MedicationRequest>(System.IO.File.ReadAllText(@$"{mappingtutorial_folder}\medicationrequest\source.json"));

            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);
            var provider = new PocoStructureDefinitionSummaryProvider();
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);
            var output = new MedicationRequest();
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
            var xml2 = _xmlSerializer.SerializeToString(output);
            System.Diagnostics.Trace.WriteLine(xml2);
        }

        [TestMethod]
        public void Tutorial_Step1a()
        {
			RunTutorialTest("step1", "step1.map", "source1.xml", "target1a.xml");
        }

		[TestMethod]
		public void Tutorial_Step1b()
		{
			RunTutorialTest("step1", "step1b.map", "source1.xml", "target1b.xml");
		}

		[TestMethod]
        public void Tutorial_Step2()
        {
			RunTutorialTest("step2", "step2.map", "source2.xml", "target2.xml");
        }

        [TestMethod]
        public void Tutorial_Step3a()
        {
			RunTutorialTest("step3", "step3a.map", "source3.xml", "target3a.xml");
        }

        [TestMethod]
        public void Tutorial_Step3b()
        {
			RunTutorialTest("step3", "step3b.map", "source3.xml", "target3b.xml");
        }

		[TestMethod]
		public void Tutorial_Step3bmin()
		{
			RunTutorialTest("step3", "step3b.map", "source3min.xml", "target3bmin.xml");
		}

		[TestMethod]
        public void Tutorial_Step3c()
        {
			RunTutorialTest("step3", "step3c.map", "source3.xml", "target3c.xml", true);
        }

		[TestMethod]
		public void Tutorial_Step4a()
		{
			RunTutorialTest("step4", "step4a.map", "source4.xml", "target5a.xml");
		}

		[TestMethod]
		public void Tutorial_Step4b()
		{
			RunTutorialTest("step4", "step4b.map", "source4.xml", "target4b.xml");
		}

		[TestMethod]
		public void Tutorial_Step4b2()
		{
			RunTutorialTest("step4", "step4b2.map", "source4.xml", "target4b2.xml");
		}

		[TestMethod]
		public void Tutorial_Step4b3()
		{
			RunTutorialTest("step4", "step4b3.map", "source4.xml", "target4b3.xml");
		}

		[TestMethod]
        public void Tutorial_Step5a()
        {
			RunTutorialTest("step5", "step5.map", "source5.xml", "target5a.xml");
        }

        [TestMethod]
        public void Tutorial_Step5b()
        {
			RunTutorialTest("step5", "step5.map", "source5b.xml", "target5b.xml");
        }

		[TestMethod]
		public void Tutorial_Step6a()
		{
			RunTutorialTest("step6", "step6a.map", "source6.xml", "target6a.xml");
		}

		[TestMethod]
		public void Tutorial_Step6b()
		{
			RunTutorialTest("step6", "step6b.map", "source6b.xml", "target6b.xml", true);
		}

		[TestMethod]
		public void Tutorial_Step6c()
		{
			RunTutorialTest("step6", "step6c.map", "source6.xml", "target6c.xml");
		}

		[TestMethod]
		public void Tutorial_Step6d()
		{
			RunTutorialTest("step6", "step6d.map", "source6.xml", "target6d.xml");
		}

		[TestMethod]
		public void Tutorial_Step7a()
		{
			RunTutorialTest("step7", "step7.map", "source7.xml", "target7a.xml");
		}

		[TestMethod]
		public void Tutorial_Step7b()
		{
			RunTutorialTest("step7", "step7b.map", "source7.xml", "target7b.xml");
		}

		[TestMethod]
		public void Tutorial_Step8()
		{
			RunTutorialTest("step8", "step8.map", "source8.xml", "target8.xml");
		}

		[TestMethod]
		public void Tutorial_Step9a()
		{
			RunTutorialTest("step9", "step9.map", "source9.xml", "target9a.xml");
		}

		[TestMethod]
		public void Tutorial_Step9b()
		{
			RunTutorialTest("step9", "step9.map", "source9b.xml", "target9b.xml");
		}

		[TestMethod]
		public void Tutorial_Step10()
		{
            RunTutorialTest("step10", "step10.map", "source10.xml", "target10.xml");
		}

		public void RunTutorialTest(string stepFolderName, string mapFile, string sourceFile, string expectedResultFile, bool expectFailure = false)
		{
			var parser = new StructureMapUtilitiesParse();
			var mapStep5 = System.IO.File.ReadAllText(@$"{mappingtutorial_folder}\maptutorial\{stepFolderName}\map\{mapFile}");
			var sm = parser.parse(mapStep5, mapFile);
			//System.IO.File.WriteAllText(
			//	@$"{mappingtutorial_folder}\maptutorial\step5\map\step5.xml.new",
			//	_xmlSerializer.SerializeToString(sm));

			var source3 = System.IO.File.ReadAllText(@$"{mappingtutorial_folder}\maptutorial\{stepFolderName}\source\{sourceFile}");
			var sourceNode = FhirXmlNode.Parse(source3);

            var directorySource = new DirectorySource(@$"{mappingtutorial_folder}\maptutorial\{stepFolderName}\logical");
            Dictionary<string, string> mapLogicalModelTypes = new Dictionary<string, string>();
			foreach (var sd in directorySource.FindAll<StructureDefinition>())
            {
				mapLogicalModelTypes.Add(sd.Type, sd.Url);

			}
			var source = new CachedResolver(new MultiResolver(directorySource, ZipSource.CreateValidationSource()));
			source.Load += Source_Load;
			var worker = new TestWorker(source);

			IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(
				source,
				(string name, out string canonical) => {
                    if (mapLogicalModelTypes.TryGetValue(name, out canonical))
						return true;
					return StructureDefinitionSummaryProvider.DefaultTypeNameMapper(name, out canonical);
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
				if (!expectFailure)
				{
					Assert.Fail(ex.Message);
				}
				return;
			}
			var xml2 = target.ToXml(new FhirXmlSerializationSettings() { Pretty = true });
			// var xml2 = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
			System.Diagnostics.Trace.WriteLine(xml2);

			// Now validate (or learn) the result of this test
			var outputFolder = @$"{mappingtutorial_folder}\maptutorial\{stepFolderName}\output";
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
			var expectedResultFilename = @$"{mappingtutorial_folder}\maptutorial\{stepFolderName}\output\{expectedResultFile}";
			if (File.Exists(expectedResultFile))
            {
				// compare the result with the expected result
    			var expectedResult = System.IO.File.ReadAllText(expectedResultFilename);
				Assert.AreEqual(expectedResult, xml2);
			}
            else
            {
                // "Learn" this result for next time (and can manually tweak the value if wasn't what was expected)
                File.WriteAllText(expectedResultFilename, xml2);
			}
		}
	}
}