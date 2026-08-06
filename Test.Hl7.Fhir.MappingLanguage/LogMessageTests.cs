using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Specification;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class LogMessageTests
    {
        [TestMethod]
        public void OutputVariableIsCapturedWhenMessageIsCreated()
        {
            var provider = new PocoStructureDefinitionSummaryProvider();
            var output = ElementNode.Root(provider, "Patient");
            var active = output.Add(provider, "active");
            active.Value = true;
            var variables = new StructureMapUtilitiesAnalyze.Variables();
            variables.add(StructureMapUtilitiesAnalyze.VariableMode.OUTPUT, "target", output);

            var message = new LogMessage("Copy", variables);
            active.Value = false;

            var captured = message.variables.Single();
            Assert.AreEqual(StructureMapUtilitiesAnalyze.VariableMode.OUTPUT, captured.Mode);
            Assert.AreEqual("target", captured.Name);
            Assert.AreEqual("Patient", captured.Path);
            StringAssert.Contains(captured.Value, "\"resourceType\":\"Patient\"");
            StringAssert.Contains(captured.Value, "\"active\":true");
            Assert.IsFalse(captured.Value.Contains("\"active\":false"));
        }
    }
}
