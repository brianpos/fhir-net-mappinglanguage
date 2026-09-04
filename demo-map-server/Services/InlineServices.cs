using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using System.Text;

namespace demo_map_server.Services
{
    internal class InlineServices : StructureMapUtilitiesAnalyze.ITransformerServices
    {
        public List<KeyValuePair<string, LogMessage>> LogMessages { get; private set; } = new List<KeyValuePair<string, LogMessage>>();
        /// <summary>
        /// Debug Mode Off will not evaluate debug/trace messages which can be quite costly in that the variables are serialized out
        /// </summary>
        public bool DebugMode = false;

        public string FormatOutput()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var log in LogMessages)
            {
                sb.AppendLine($"{log.Key}: {log.Value.message}");
            }
            return sb.ToString();
        }

        internal InlineServices(OperationOutcome outcome, IStructureDefinitionSummaryProvider provider, IResourceResolver source)
        {
            _outcome = outcome;
            _provider = provider;
            _source = source;
        }
        private OperationOutcome _outcome;
        private IStructureDefinitionSummaryProvider _provider;
        private IResourceResolver _source;
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
			var result = message();
            if (DebugMode || category == "error")
                LogMessages.Add(new KeyValuePair<string, LogMessage>(category, new LogMessage(result, null)));

            if (category == "error")
            {
                _outcome.Issue.Insert(0, new OperationOutcome.IssueComponent
                {
                    Code = OperationOutcome.IssueType.Informational,
                    Severity = OperationOutcome.IssueSeverity.Information,
                    Details = new CodeableConcept(null, null, message())
                });
            }
        }

		public void log(string category, Func<LogMessage> message)
		{
			var result = message();
			if (DebugMode || category == "error")
				LogMessages.Add(new KeyValuePair<string, LogMessage>(category, result));

			if (category == "error")
			{
				_outcome.Issue.Insert(0, new OperationOutcome.IssueComponent
				{
					Code = OperationOutcome.IssueType.Informational,
					Severity = OperationOutcome.IssueSeverity.Information,
					Details = new CodeableConcept(null, null, result.message)
				});
			}
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
            var cm = this._source.ResolveByCanonicalUri(conceptMapUrl) as ConceptMap;
            if (cm != null)
            {
                // Yes, collect all possible matches
                List<Coding> results = new List<Coding>();
                var groups = cm.Group.Where(g => g.Source == source.System);
                foreach (var g in groups)
                {
                    var sourceElements = g.Element.Where(e => e.Code == source.Code);
                    var targetElements = sourceElements.SelectMany(s => s.Target.Where(t => t.Equivalence != ConceptMapEquivalence.Disjoint));
                    if (targetElements.Count() == 1)
                    {
                        // this is the one!
                        results.Add(new Coding(g.Target, targetElements.First().Code));
                    }
                }
                if (results.Count == 1)
                    return results.First();
                else if (results.Count > 1)
                {
                    // Ambiguous, return the first one
                    log("warning", () => $"Ambiguous translation for {source.System}|{source.Code} in {conceptMapUrl}, returning first of {results.Count} matches");
                    return results.First();
                }
                else
                {
                    log("warning", () => $"No translation found for {source.System}|{source.Code} in {conceptMapUrl}");
                    return null;
                }
            }
            throw new NotImplementedException();
        }
    }
}
