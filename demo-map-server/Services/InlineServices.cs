using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;

namespace demo_map_server.Services
{
    internal class InlineServices : StructureMapUtilitiesAnalyze.ITransformerServices
    {
        public List<KeyValuePair<string, string>> LogMessages { get; private set; } = new List<KeyValuePair<string, string>>();
        /// <summary>
        /// Debug Mode Off will not evaluate debug/trace messages which can be quite costly in that the variables are serialized out
        /// </summary>
        public bool DebugMode = false;
            
        internal InlineServices(OperationOutcome outcome, IStructureDefinitionSummaryProvider provider)
        {
            _outcome = outcome;
            _provider = provider;
        }
        private OperationOutcome _outcome;
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
            if (DebugMode || category == "error")
                LogMessages.Add(new KeyValuePair<string, string>(category, message()));

            //_outcome.Issue.Insert(0, new OperationOutcome.IssueComponent
            //{
            //    Code = OperationOutcome.IssueType.Informational,
            //    Severity = OperationOutcome.IssueSeverity.Information,
            //    Details = new CodeableConcept(null, null, message)
            //});
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
