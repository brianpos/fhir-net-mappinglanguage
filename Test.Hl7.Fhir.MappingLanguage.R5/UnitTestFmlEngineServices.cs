using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.FhirMappingLanguage
{
    internal class UnitTestFmlEngineServices : StructureMapUtilitiesAnalyze.ITransformerServices
    {
        internal UnitTestFmlEngineServices(IStructureDefinitionSummaryProvider provider)
        {
            _provider = provider;
        }
        private IStructureDefinitionSummaryProvider _provider;
        public ITypedElement createResource(object appInfo, ITypedElement res, bool atRootofTransform)
        {
            return res;
        }

        public ITypedElement createType(object appInfo, string name)
        {
            return ElementNode.Root(_provider, name);
        }

        public void log(string category, Func<string> message)
        {
            if (category != "error")
                return;
            switch (category)
            {
                case "error":
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case "debug":
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case "info":
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case "prop":
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }
            Console.WriteLine($"{category}: {message()}");
            Console.ResetColor();
        }

        public List<ITypedElement> performSearch(object appContext, string url)
        {
            throw new NotImplementedException();
        }

        public ITypedElement resolveReference(object appContext, string url)
        {
            throw new NotImplementedException();
        }

        public Coding translate(object appInfo, Coding source, string conceptMapUrl)
        {
            throw new NotImplementedException();
        }
    }
}
