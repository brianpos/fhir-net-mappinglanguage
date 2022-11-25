using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Test.Hl7.Fhir.MappingLanguage;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class CsvTests
    {
        [TestMethod]
        public void CheckCsvHeaderSimple()
        {
            var csvData = "aa,bb,cc\r\nzzz,yyy,xxx";
            using (var sr = new StringReader(csvData))
            {
                CsvReader reader = new CsvReader(sr);
                reader.ParseHeader();
                Console.WriteLine($">{reader.rawHeader}<");
                Console.WriteLine(string.Join("\r\n", reader.Columns.Select(s => $"[{s}]")));
                Assert.AreEqual("aa", reader.Columns[0]);
                Assert.AreEqual("bb", reader.Columns[1]);
                Assert.AreEqual("cc", reader.Columns[2]);
            }
        }

        [TestMethod]
        public void CheckCsvHeaderQuoted()
        {
            var csvData = "aa,bb,\"cc\"\r\nzzz,yyy,xxx";
            using (var sr = new StringReader(csvData))
            {
                CsvReader reader = new CsvReader(sr);
                reader.ParseHeader();
                Console.WriteLine($">{reader.rawHeader}<");
                Console.WriteLine(string.Join("\r\n", reader.Columns.Select(s => $"[{s}]")));
                Assert.AreEqual("aa", reader.Columns[0]);
                Assert.AreEqual("bb", reader.Columns[1]);
                Assert.AreEqual("cc", reader.Columns[2]);
            }
        }

        [TestMethod]
        public void CheckCsvHeaderQuotedComma()
        {
            var csvData = "aa,bb,\" c,c \",dd\r\nzzz,yyy,xxx";
            using (var sr = new StringReader(csvData))
            {
                CsvReader reader = new CsvReader(sr);
                reader.ParseHeader();
                Console.WriteLine($">{reader.rawHeader}<");
                Console.WriteLine(string.Join("\r\n", reader.Columns.Select(s => $"[{s}]")));
                Assert.AreEqual("aa", reader.Columns[0]);
                Assert.AreEqual("bb", reader.Columns[1]);
                Assert.AreEqual(" c,c ", reader.Columns[2]);
                Assert.AreEqual("dd", reader.Columns[3]);
            }
        }

        [TestMethod]
        public void CheckCsvHeaderQuotedQuote()
        {
            var csvData = "aa,bb,\"c\"\"c\",dd\r\nzzz,yyy,xxx";
            using (var sr = new StringReader(csvData))
            {
                CsvReader reader = new CsvReader(sr);
                reader.ParseHeader();
                Console.WriteLine($">{reader.rawHeader}<");
                Console.WriteLine(string.Join("\r\n", reader.Columns.Select(s => $"[{s}]")));
                Assert.AreEqual("aa", reader.Columns[0]);
                Assert.AreEqual("bb", reader.Columns[1]);
                Assert.AreEqual("c\"c", reader.Columns[2]);
                Assert.AreEqual("dd", reader.Columns[3]);
            }
        }
        [TestMethod]
        public void CheckCsvHeaderQuoted2()
        {
            var csvData = "aa,bb,\"cc\",dd\r\nzzz,yyy,xxx";
            using (var sr = new StringReader(csvData))
            {
                CsvReader reader = new CsvReader(sr);
                reader.ParseHeader();
                Console.WriteLine($">{reader.rawHeader}<");
                Console.WriteLine(string.Join("\r\n", reader.Columns.Select(s => $"[{s}]")));
                Assert.AreEqual("aa", reader.Columns[0]);
                Assert.AreEqual("bb", reader.Columns[1]);
                Assert.AreEqual("cc", reader.Columns[2]);
                Assert.AreEqual("dd", reader.Columns[3]);
            }
        }

        [TestMethod]
        public void CheckCsvHeaderNewLines()
        {
            var csvData = "\"aa\",\"bb\r\n  bb\",\"cc\"\r\nzzz,yyy,xxx";
            using (var sr = new StringReader(csvData))
            {
                CsvReader reader = new CsvReader(sr);
                reader.ParseHeader();
                Console.WriteLine($">{reader.rawHeader}<");
                Console.WriteLine(string.Join("\r\n", reader.Columns.Select(s => $"[{s}]")));
                Assert.AreEqual("aa", reader.Columns[0]);
                Assert.AreEqual("bb\r\n  bb", reader.Columns[1]);
                Assert.AreEqual("cc", reader.Columns[2]);
            }
        }

        //[TestMethod]
        //public void ScanCsvFile()
        //{
        //    using (var stream = File.OpenRead("C:\\temp\\Loinc_2.73\\LoincTable\\Loinc.csv"))
        //    // using (var stream = File.OpenRead("E:\\git\\HL7\\fhir-core-build-r5-PA\\source\\endpoint\\endpoint-examples-general-template.csv"))
        //    using (var sr = new StreamReader(stream))
        //    {
        //        CsvReader reader = new CsvReader(sr);
        //        reader.ParseHeader();
        //        Console.WriteLine($">{reader.rawHeader}<");
        //        var node = reader.GetNextEntry();
        //        int count = 0;
        //        while (node != null)
        //        {
        //            count++;
        //             Console.WriteLine($"{node.Children("LOINC_NUM").First().Value}\t{node.Children("COMPONENT").First().Value}");
        //            // node.ToXml();
        //            node = reader.GetNextEntry();
        //        }
        //        Console.WriteLine($"Total Rows: {count}");
        //    }
        //}

        //[TestMethod]
        //public void Tutorial_Step1()
        //{
        //    var parser = new StructureMapUtilitiesParse();
        //    var mapStep1 = "";
        //    var sm1 = parser.parse(mapStep1, "Step1");

        //    var source1 = "";
        //    var sourceNode = FhirXmlNode.Parse(source1);

        //    var source = new CachedResolver(new MultiResolver(
        //       ZipSource.CreateValidationSource()
        //       ));
        //    var worker = new TestWorker(source);

        //    IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(
        //        source,
        //        (string name, out string canonical) => {
        //            switch(name)
        //            {
        //                case "TLeft1":
        //                    canonical = "http://hl7.org/fhir/StructureDefinition/tutorial-left1";
        //                    return true;
        //                case "TRight1":
        //                    canonical = "http://hl7.org/fhir/StructureDefinition/tutorial-right1";
        //                    return true;
        //            }
        //            return StructureDefinitionSummaryProvider.DefaultTypeNameMapper(name, out canonical);
        //        });
        //    var engine = new StructureMapUtilitiesExecute(worker, null, provider);
        //    // var ti = provider.Provide("http://hl7.org/fhir/StructureDefinition/tutorial-left1");

        //    var target = ElementNode.Root(provider, "TRight1");
        //    try
        //    {
        //        engine.transform(null, sourceNode.ToTypedElement(provider), sm1, target);
        //    }
        //    catch (System.Exception ex)
        //    {
        //        System.Diagnostics.Trace.WriteLine(ex.Message);
        //    }
        //    var xml2 = target.ToXml(new FhirXmlSerializationSettings() { Pretty = true });
        //    // var xml2 = target.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
        //    System.Diagnostics.Trace.WriteLine(xml2);
        //    Assert.AreEqual("<TRight1 xmlns=\"http://hl7.org/fhir\">\r\n  <a value=\"step1\" />\r\n</TRight1>", xml2);
        //}
    }
}