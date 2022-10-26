using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Hl7.Fhir.WebApi;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Xml;

namespace demo_map_server
{
    public class FhirMappingLanugageInputFormatter : FhirMediaTypeInputFormatter
    {
        public FhirMappingLanugageInputFormatter() : base()
        {
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/fhir-mapping"));
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding.EncodingName != Encoding.UTF8.EncodingName)
                throw new FhirServerException(HttpStatusCode.BadRequest, "FHIR Mapping Language supports UTF-8 encoding exclusively, not " + encoding.WebName);

            var request = context.HttpContext.Request;

            using (var stream = new StreamReader(request.Body))
            {
                string rawMapContent = await stream.ReadToEndAsync();

                try
                {
                    var parser = new StructureMapUtilitiesParse();
                    var resource = parser.parse(rawMapContent, null);
                    if (request.RequestUri().LocalPath.Contains("StructureMap"))
                    {
                        // check to see if there is an ID in here the URL
                        // as the FHIR Mapping Language doesn't specify one
                        string localPath = request.RequestUri().LocalPath;
                        string remainingPath = localPath.Substring(localPath.IndexOf("StructureMap") + 12).Trim('/');
                        if (!remainingPath.Contains("/"))
                            resource.Id = remainingPath;
                    }
                    // some default values when posting via the FML format
                    resource.Status = PublicationStatus.Draft;

                    return InputFormatterResult.Success(resource);

                }
                catch (FHIRLexerException exception)
                {
                    OperationOutcome operationOutcome = new OperationOutcome();
                    operationOutcome.Issue.Add(new OperationOutcome.IssueComponent
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Exception,
                        Details = new CodeableConcept
                        {
                            Text = exception.Message
                        }
                    });
                    //if (!Object.Equals(exception.ErrorPosition, Superpower.Model.Position.Empty))
                    //{
                    //    operationOutcome.Issue[0].Location = new[] { exception.ErrorPosition.ToString() };
                    //}

                    throw new FhirServerException(HttpStatusCode.BadRequest, operationOutcome, "Body parsing failed: " + exception.Message);
                }
            }
        }
    }

    public class FhirMappingLanugageOutputFormatter : FhirMediaTypeOutputFormatter
    {
        public FhirMappingLanugageOutputFormatter() : base()
        {
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/fhir-mapping"));
        }

        public override void WriteResponseHeaders(OutputFormatterWriteContext context)
        {
            context.ContentType = FhirMediaType.GetMediaTypeHeaderValue(context.ObjectType, ResourceFormat.Xml);
            // note that the base is called last, as this may overwrite the ContentType where the resource is of type Binary
            base.WriteResponseHeaders(context);
            //   headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "fhir.resource.json" };
        }

        protected override bool CanWriteType(Type type)
        {
            if (typeof(StructureMap).IsAssignableFrom(type))
                return true;
            return base.CanWriteType(type);
        }

        public async override System.Threading.Tasks.Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (selectedEncoding == null)
                throw new ArgumentNullException(nameof(selectedEncoding));

            if (context.ObjectType != null)
            {
                if (context.Object is StructureMap sm)
                {
                    var canonicalFml = StructureMapUtilitiesParse.render(sm);
                    StreamWriter sw = new StreamWriter(context.HttpContext.Response.Body);
                    StringBuilder sb = new StringBuilder(canonicalFml);
                    await sw.WriteAsync(sb, context.HttpContext.RequestAborted);
                    await sw.FlushAsync();
                    await sw.DisposeAsync();
                }
            }
        }
    }
}
