using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Language.Debugging;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Hl7.Fhir.WebApi;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;

namespace demo_map_server.StructureMapTransform
{
    public class StructureMapTransformOutputFormatter : FhirMediaTypeOutputFormatter
    {
        public StructureMapTransformOutputFormatter() : base()
        {
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/xml"));
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/json"));

            // This could be anything, and doesn't really matter as this formatter is explicitly
            // attached to only the response that it's required on
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("*/*"));
        }

        public override void WriteResponseHeaders(OutputFormatterWriteContext context)
        {
            context.ContentType = FhirMediaType.GetMediaTypeHeaderValue(context.ObjectType, ResourceFormat.Json);
            if (context.Object is OperationOutcome oo && oo.HasAnnotation<StructureMapTransformOutput>())
            {
                var sto = oo.Annotation<StructureMapTransformOutput>();
                if (!string.IsNullOrEmpty(sto.MimeType))
                {
                    MediaTypeHeaderValue header = new MediaTypeHeaderValue(sto.MimeType);
                    header.Charset = Encoding.UTF8.WebName;
                    context.ContentType = new StringSegment(header.ToString());
                }
                // if there is only a small amount of logging, throw it in the headers
                // https://stackoverflow.com/questions/1097651/is-there-a-practical-http-header-length-limit
                if (!string.IsNullOrEmpty(sto.LogMessages) && sto.LogMessages.Length < 4000)
                {
                    context.HttpContext.Response.Headers.Add("debug", sto.LogMessages.Split("\r\n"));
                }
            }

            // note that the base is called last, as this may overwrite the ContentType where the resource is of type Binary
            base.WriteResponseHeaders(context);
            //   headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "fhir.resource.json" };
        }

        protected override bool CanWriteType(Type type)
        {
            return true;
        }

        public override async System.Threading.Tasks.Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (selectedEncoding == null)
                throw new ArgumentNullException(nameof(selectedEncoding));

            if (context.Object is OperationOutcome oo && oo.HasAnnotation<StructureMapTransformOutput>())
            {
                var sto = oo.Annotation<StructureMapTransformOutput>();
                string content;
                // this output formatter really only does XML or json
                if (sto.MimeType?.Contains("xml") == true)
                    content = sto.OutputContent.ToXml(new FhirXmlSerializationSettings() { Pretty = true });
                else
                    content = sto.OutputContent.ToJson(new FhirJsonSerializationSettings() { Pretty = true });

                StreamWriter sw = new StreamWriter(context.HttpContext.Response.Body);
                StringBuilder sb = new StringBuilder(content);
                await sw.WriteAsync(sb, context.HttpContext.RequestAborted);
                await sw.FlushAsync();
                await sw.DisposeAsync();
            }
        }
    }
}
