using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Hl7.Fhir.WebApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Xml;

namespace demo_map_server.StructureMapTransform
{
    public class StructureMapTransformFilter : IResultFilter
    {
        public void OnResultExecuted(ResultExecutedContext context)
        {
        }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            // Look for the _format parameter on the query
            var query = context.HttpContext.Request.Query;
            var path = context.HttpContext.Request.Path;
            if (context.Result is ObjectResult result
                && result.Value is OperationOutcome oo
                && oo.HasAnnotation<StructureMapTransformOutput>()
                )
            {
                result.Formatters.Add(new StructureMapTransformOutputFormatter());
            }
        }
    }
}
