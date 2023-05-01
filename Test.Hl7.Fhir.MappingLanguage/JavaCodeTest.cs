using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class JavaCodeTest
    {
        [TestMethod]
        public void UnitTestFromJavaCore()
        {
            // https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r5/src/test/java/org/hl7/fhir/r5/test/StructureMapUtilitiesTest.java
            var expression = System.IO.File.ReadAllText("E:\\git\\HL7\\fhir-test-cases\\r5\\structure-mapping\\syntax.map");
            System.Diagnostics.Trace.WriteLine(expression);
            System.Diagnostics.Trace.WriteLine("--------------------------------");


            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(expression, null);

            var canonicalFml = StructureMapUtilitiesParse.render(sm);
            System.Diagnostics.Trace.WriteLine(canonicalFml);

            var result2 = parser.parse(canonicalFml, null);

            assertSerializeDeserialize(sm);
            assertSerializeDeserialize(result2);
            Assert.IsTrue(sm.IsExactly(result2));
        }

        private void assertSerializeDeserialize(StructureMap structureMap)
        {
            Assert.AreEqual("syntax", structureMap.Name);
            Assert.AreEqual("Title of this map\r\nAuthor", structureMap.Description);
            Assert.AreEqual("http://github.com/FHIR/fhir-test-cases/r5/fml/syntax", structureMap.Url);
            Assert.AreEqual("Patient", structureMap.Structure[0].Alias);
            Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Patient", structureMap.Structure[0].Url);
            Assert.AreEqual("Source Documentation", structureMap.Structure[0].Documentation);
            Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Patient", structureMap.Structure[0].Url);
            Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Basic", structureMap.Structure[1].Url);
            Assert.AreEqual("Target Documentation", structureMap.Structure[1].Documentation);
            Assert.AreEqual("Groups\r\nrule for patient group", structureMap.Group[0].Documentation);
            Assert.AreEqual("Comment to rule", structureMap.Group[0].Rule[0].Documentation);
            Assert.AreEqual("Copy identifier short syntax", structureMap.Group[0].Rule[1].Documentation);

            StructureMap.TargetComponent target = structureMap.Group[0].Rule[2].Target[1];
            Assert.AreEqual("'urn:uuid:' + r.lower()", target.Parameter[0].Value.ToString());
        }
    }
}