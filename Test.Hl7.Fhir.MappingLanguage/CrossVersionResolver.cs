using Hl7.Fhir.Language.Debugging;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Hl7.Fhir.MappingLanguage
{
    static class CanonicalExtensions
    {
        public static string Version(this Canonical url)
        {
            int indexOfVersion = url.Value.IndexOf("|");
            if (indexOfVersion == -1)
                return null;
            var result = url.Value.Substring(indexOfVersion + 1);
            int indexOfFragment = result.IndexOf("#");
            if (indexOfFragment != -1)
                return result.Substring(0, indexOfFragment);
            return result;
        }

        public static string BaseCanonicalUrl(this Canonical url)
        {
            int indexOfVersion = url.Value.IndexOf("|");
            if (indexOfVersion != -1)
                return url.Value.Substring(0, indexOfVersion);
            int indexOfFragment = url.Value.IndexOf("#");
            if (indexOfFragment != -1)
                return url.Value.Substring(0, indexOfFragment);
            return url.Value;
        }
    }

    internal class CrossVersionResolver : IResourceResolver
    {
        public CrossVersionResolver()
        {
            var settingsJson = new FhirJsonParsingSettings() { PermissiveParsing = true };
            var settingsXml = new FhirXmlParsingSettings() { PermissiveParsing = true };
            var settingsDir = new DirectorySourceSettings() { JsonParserSettings = settingsJson, XmlParserSettings = settingsXml };
            stu3 = new DirectorySource(@"C:\Users\brian\.fhir\packages\hl7.fhir.r3.core#3.0.2\package", settingsDir);
            stu3.ParserSettings.ExceptionHandler = CustomExceptionHandler;
            r4 = new DirectorySource(@"C:\Users\brian\.fhir\packages\hl7.fhir.r4b.core#4.3.0\package", settingsDir);
            r5 = new DirectorySource(@"C:\Users\brian\.fhir\packages\hl7.fhir.r5.core#5.0.0-ballot\package", settingsDir);
        }

        void CustomExceptionHandler(object source, ExceptionNotification args)
        {
            // System.Diagnostics.Trace.WriteLine(args.Message);
        }

        DirectorySource stu3;
        DirectorySource r4;
        DirectorySource r5;
        const string fhirBaseCanonical = "http://hl7.org/fhir/";

        private string ConvertCanonical(string uri)
        {
            // convert this from the old format into the versioned format
            // http://hl7.org/fhir/3.0/StructureDefinition/Account
            // =>
            // http://hl7.org/fhir/StructureDefinition/Account|3.0
            // http://hl7.org/fhir/StructureDefinition/Account|3.0.1
            // http://hl7.org/fhir/StructureDefinition/Account|4.0.1
            // i.e. https://github.com/microsoft/fhir-codegen/blob/dev/src/Microsoft.Health.Fhir.SpecManager/Manager/FhirPackageCommon.cs#L513
            int index = uri.IndexOf("/StructureDefinition/");
            if (uri.StartsWith(fhirBaseCanonical) && index > fhirBaseCanonical.Length)
            {
                string version = uri.Substring(fhirBaseCanonical.Length, index - fhirBaseCanonical.Length);
                switch (version)
                {
                    case "DSTU2":
                        version = "3.0"; // stub these into the STU3 namespace
                        break;
                    case "3.0.0":
                    case "3.0.1":
                    case "3.0.2":
                    case "STU3":
                        version = "3.0";
                        break;

                    case "4.0.0":
                    case "4.0.1":
                    case "R4":
                        version = "4.0";
                        break;

                    case "4.3.0":
                    case "R4B":
                        version = "4.3";
                        break;

                    case "5.0.0":
                    case "R5":
                        version = "5.0";
                        break;
                }
                return $"{fhirBaseCanonical}StructureDefinition/{uri.Substring(index + 21)}|{version}";
            }
            return uri;
        }

        public Resource ResolveByCanonicalUri(string uri)
        {
            string convertedUrl = ConvertCanonical(uri);
            Canonical cu = new Canonical(convertedUrl);
            if (cu.Version() == "3.0")
                return stu3.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
            if (cu.Version() == "5.0")
                return r5.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
            return r4.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
        }

        public Resource ResolveByUri(string uri)
        {
            string convertedUrl = ConvertCanonical(uri);
            Canonical cu = new Canonical(convertedUrl);
            if (cu.Version() == "3.0")
                return stu3.ResolveByUri(cu.BaseCanonicalUrl());
            if (cu.Version() == "5.0")
                return r5.ResolveByUri(cu.BaseCanonicalUrl());
            return r4.ResolveByUri(cu.BaseCanonicalUrl());
        }
    }
}
