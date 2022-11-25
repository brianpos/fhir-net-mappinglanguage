using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Hl7.Fhir.MappingLanguage
{
    /// <summary>
    /// Expression.Equal(left, right, true, null)
    /// </summary>
    public class CsvReader
    {
        public List<string> Columns { get; private set; } = new List<string>();
        public string rawHeader;
        TextReader _reader;

        public CsvReader(TextReader reader)
        {
            _reader = reader;
        }

        public IEnumerable<string> ParseHeader()
        {
            IEnumerable<string> header = ReadLine();
            Columns.AddRange(header);
            return Columns;
        }

        private IEnumerable<string> ReadLine()
        {
            List<string> fields = new List<string>();
            string currentLine = _reader.ReadLine();
            if (currentLine == null)
                return null;

            // Check for quoted content
            var indexStartQuote = currentLine.IndexOf('"');
            while (indexStartQuote != -1)
            {
                // locate the end quote
                var indexEndQuote = currentLine.IndexOf('"', indexStartQuote + 1);
                var lastLength = currentLine.Length;
                while (indexEndQuote == -1)
                {
                    System.Diagnostics.Trace.WriteLine("Needed another line");
                    var nextLine = _reader.ReadLine();
                    currentLine += "\r\n" + nextLine;
                    if (nextLine == null)
                        break;
                    indexEndQuote = currentLine.IndexOf('"', lastLength);
                    lastLength = currentLine.Length;
                }
                indexStartQuote = currentLine.IndexOf('"', indexEndQuote + 1);
            }

            if (string.IsNullOrEmpty(rawHeader))
                rawHeader = currentLine;

            // Have the full line now in currentLine (including any spanned rows)
            var tokens = new[] { ',', '"' };
            int index = currentLine.IndexOfAny(tokens);
            int startIndex = 0;
            while (index != -1)
            {
                if (currentLine[index] == ',')
                {
                    fields.Add(currentLine.Substring(startIndex, index - startIndex).Trim());
                    startIndex = index + 1;
                    index = currentLine.IndexOfAny(tokens, index + 1);
                }
                else
                {
                    // this is a quoted entry, so check for the end marker
                    startIndex = index + 1;
                    int endQuote = currentLine.IndexOf('"', startIndex);
                    while (currentLine.Length > endQuote + 2 && currentLine[endQuote + 1] == '"')
                    {
                        endQuote = currentLine.IndexOf('"', endQuote + 2);
                    }
                    fields.Add(currentLine.Substring(startIndex, endQuote - startIndex).Replace("\"\"", "\""));
                    startIndex = endQuote + 2;
                    if (startIndex > currentLine.Length)
                        break;
                    index = currentLine.IndexOfAny(tokens, startIndex);
                }
                if (index == -1)
                {
                    // last node, just add the rest in
                    fields.Add(currentLine.Substring(startIndex).Trim());
                }
            }

            return fields;
        }

        public ITypedElement GetNextEntry()
        {
            var fields = ReadLine();
            if (fields == null)
                return null;
            if (fields.Count() > 0 && Columns.Count == 0)
            {
                // Dummy in fieldX as the names
                Columns.AddRange(fields.Select((f, i) => $"field{i}"));
            }

            var tes = fields.Select((f, i) => SourceNode.Valued(Columns[i], f));
            var sn = SourceNode.Node("Entry", tes.ToArray());
            if (_provider == null)
                _provider = new CsvHackedSummaryProvider(Columns);
            return sn.ToTypedElement();
            // return sn.ToTypedElement(_provider, "Entry");
        }
        CsvHackedSummaryProvider _provider;

        public class CsvHackedSummaryProvider : IStructureDefinitionSummaryProvider
        {
            public CsvHackedSummaryProvider(IEnumerable<string> fields)
            {
                _fields = fields;
            }
            IEnumerable<string> _fields;

            public IStructureDefinitionSummary Provide(string canonical)
            {
                Console.WriteLine(canonical);
                return new CsvHackedStructureDefinitionSummary(_fields);
            }
        }

        public class CsvHackedStructureDefinitionSummary : IStructureDefinitionSummary
        {
            public CsvHackedStructureDefinitionSummary(IEnumerable<string> fields)
            {
                _fields = fields;
            }
            IEnumerable<string> _fields;

            public string TypeName => "Entry";

            public bool IsAbstract => false;

            public bool IsResource => true;

            public IReadOnlyCollection<IElementDefinitionSummary> GetElements()
            {
                return _fields.Select((f, i) => new CvsItemElementDefinitionSummary(f, i)).ToList();
            }
        }

        public class CvsItemElementDefinitionSummary : IElementDefinitionSummary
        {
            public CvsItemElementDefinitionSummary(string fieldName, int position)
            {
                _field = fieldName;
                _position = position;
            }

            string _field;
            int _position;

            public string ElementName => _field;

            public bool IsCollection => false;

            public bool IsRequired => false;

            public bool InSummary => true;

            public bool IsChoiceElement => false;

            public bool IsResource => false;

            public bool IsModifier => false;

            public ITypeSerializationInfo[] Type => new[] { new CsvElementSerialization() };

            public string DefaultTypeName => "string";

            public string NonDefaultNamespace => null;

            public XmlRepresentation Representation => XmlRepresentation.XmlElement;

            public int Order => _position;
        }

        public class CsvElementSerialization : IStructureDefinitionSummary
        {
            public string TypeName => "string";
            
            public bool IsAbstract => false;

            public bool IsResource => false;

            public IReadOnlyCollection<IElementDefinitionSummary> GetElements()
            {
                return new List<IElementDefinitionSummary>();
            }
        }
    }
}
