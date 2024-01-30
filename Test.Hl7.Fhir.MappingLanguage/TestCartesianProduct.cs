using Hl7.Fhir.MappingLanguage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class CartesianProductTest
    {
        [TestMethod]
        public void CalculateCartesianProduct()
        {
            List<List<string>> input = new List<List<string>>()
            {
				new List<string>() { },
				new List<string>() { "a", "b", "c" },
				new List<string>() { },
				new List<string>() { "1", "2", "3" },
                new List<string>() { "x" },
				new List<string>() { },
			};

            var result = StructureMapUtilitiesExecute.CartesianProduct(input).Select(r => String.Join("", r));
            Console.WriteLine(string.Join(",\n", result));

            Assert.IsTrue(result.Contains("a1x"));
            Assert.IsTrue(result.Contains("a2x"));
            Assert.IsTrue(result.Contains("a3x"));
            Assert.IsTrue(result.Contains("b1x"));
            Assert.IsTrue(result.Contains("b2x"));
            Assert.IsTrue(result.Contains("b3x"));
            Assert.IsTrue(result.Contains("c1x"));
            Assert.IsTrue(result.Contains("c2x"));
            Assert.IsTrue(result.Contains("c3x"));

            Assert.AreEqual(9, result.Count());
        }
    }
}