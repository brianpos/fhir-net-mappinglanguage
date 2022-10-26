using Hl7.Fhir.ElementModel;

namespace demo_map_server.StructureMapTransform
{
    public class StructureMapTransformOutput
    {
        public string MimeType { get; set; }
        public ElementNode OutputContent { get; set; }
    }
}
