// extern alias R5;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using System.IO;
using System.Reflection;

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

        public static void Version(this Canonical url, string version)
        {
            if (string.IsNullOrEmpty(version))
                return;

            string newUrl = $"{url.BaseCanonicalUrl()}|{version}";
            string fragment = url.Fragment();
            int indexOfVersion = url.Value.IndexOf("|");
            if (!string.IsNullOrEmpty(fragment))
                newUrl += $"#{fragment}";
            url.Value = newUrl;
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

        public static string Fragment(this Canonical url)
        {
            int indexOfFragment = url.Value.IndexOf("#");
            if (indexOfFragment != -1)
                return url.Value.Substring(0, indexOfFragment);
            return null;
        }
    }

    /// <summary>
    /// This resolver will take any non version specific question and then resolve with
    /// the designated version number
    /// </summary>
    internal class FixedVersionResolver : IResourceResolver
    {
        public FixedVersionResolver(string version, IResourceResolver source)
        {
            _fixedVersion = version;
            _source = source;
        }
        private string _fixedVersion;
        private IResourceResolver _source;

        public Resource ResolveByCanonicalUri(string uri)
        {
            Canonical cu = new Canonical(uri);
            if (string.IsNullOrWhiteSpace(cu.Version()))
                cu.Version(_fixedVersion);
            return _source.ResolveByCanonicalUri(cu);
        }

        public Resource ResolveByUri(string uri)
        {
            Canonical cu = new Canonical(uri);
            if (string.IsNullOrWhiteSpace(cu.Version()))
                cu.Version(_fixedVersion);
            return _source.ResolveByUri(cu);
        }
    }

    internal class VersionFilterResolver : IResourceResolver
    {
        public VersionFilterResolver(string version, IResourceResolver source)
        {
            _fixedVersion = version;
            _source = source;
        }
        private string _fixedVersion;
        private IResourceResolver _source;

        public Resource ResolveByCanonicalUri(string uri)
        {
            string convertedUrl = CrossVersionResolver.ConvertCanonical(uri);
            Canonical cu = new Canonical(convertedUrl);
            if (!string.IsNullOrWhiteSpace(cu.Version()))
            {
                if (cu.Version() != _fixedVersion)
                    return null;
            }
            var result = _source.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
            return result;
        }

        public Resource ResolveByUri(string uri)
        {
            string convertedUrl = CrossVersionResolver.ConvertCanonical(uri);
            Canonical cu = new Canonical(convertedUrl);
            if (!string.IsNullOrWhiteSpace(cu.Version()))
            {
                if (cu.Version() != _fixedVersion)
                    return null;
            }
            return _source.ResolveByUri(cu.BaseCanonicalUrl());
        }
    }

    internal class CrossVersionResolver : IResourceResolver
    {
        public CrossVersionResolver()
        {
            var settingsJson = new FhirJsonParsingSettings() { PermissiveParsing = true };
            var settingsXml = new FhirXmlParsingSettings() { PermissiveParsing = true };
            var settingsDir = new DirectorySourceSettings() { JsonParserSettings = settingsJson, XmlParserSettings = settingsXml, ParserSettings = { AcceptUnknownMembers = true, PermissiveParsing = true, AllowUnrecognizedEnums = true } };

            var cacheBaseDir = Path.Combine(Path.GetTempPath(), BuildDefaultCacheDirectoryNameWithoutSdkVersion());

            stu3 = new ZipSource(Path.Combine(CommonDirectorySource.SpecificationDirectory, "specification.stu3.custom.zip"), cacheBaseDir+ "Hl7.Fhir.STU3-inR4format", settingsDir);
            r4 = new ZipSource(Path.Combine(CommonDirectorySource.SpecificationDirectory, "specification.r4.zip"), cacheBaseDir+ "Hl7.Fhir.R4", settingsDir);
			r4b = new ZipSource(Path.Combine(CommonDirectorySource.SpecificationDirectory, "specification.r4b.zip"), cacheBaseDir + "Hl7.Fhir.R4B", settingsDir);
			r5 = new ZipSource(Path.Combine(CommonDirectorySource.SpecificationDirectory, "specification.r5.zip"), cacheBaseDir + "Hl7.Fhir.R5", settingsDir);
		}

		/// <summary>
		/// Builds a directory name to use as the target of the unzipped data. The name is based
		/// on the product and version information of the assembly given.
		/// </summary>
		/// <param name="satellite">The assembly from which to take product and version information.</param>
		private static string BuildDefaultCacheDirectoryNameWithoutSdkVersion()
		{
            Assembly satellite = typeof(ZipSource).Assembly;
			var versionInfo = satellite.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			var productInfo = satellite.GetCustomAttribute<AssemblyProductAttribute>();

			//  var cleanedInformationalVersion = new string(versionInfo!.InformationalVersion.TakeWhile(c => c != '+').ToArray());
			var cleanedInformationalVersion = versionInfo!.InformationalVersion;
			return $"FhirArtifactCache-{cleanedInformationalVersion}-";
		}

		public IResourceResolver OnlyStu3 { get { return new VersionFilterResolver("3.0", stu3); } }
        public IResourceResolver OnlyR4 { get { return new VersionFilterResolver("4.0", r4); } }
        public IResourceResolver OnlyR4B { get { return new VersionFilterResolver("4.3", r4b); } }
        public IResourceResolver OnlyR5 { get { return new VersionFilterResolver("5.0", r5); } }

        void CustomExceptionHandler(object source, ExceptionNotification args)
        {
            // System.Diagnostics.Trace.WriteLine(args.Message);
        }

        IResourceResolver stu3;
        IResourceResolver r4;
		IResourceResolver r4b;
		public IResourceResolver r5;
        public IResourceResolver fallbackResolver;
        const string fhirBaseCanonical = "http://hl7.org/fhir/";

        public static string ConvertCanonical(string uri)
        {
            if (uri.StartsWith(fhirBaseCanonical))
            {
				var remainder = uri.Substring(fhirBaseCanonical.Length);
				string resourceName;
				if (remainder.StartsWith("StructureDefinition/"))
					remainder = remainder.Substring("StructureDefinition/".Length);
				if (!remainder.Contains("/"))
					return uri;
				resourceName = remainder.Substring(remainder.IndexOf("/") + 1);
				if (resourceName.StartsWith("StructureDefinition/"))
					resourceName = resourceName.Substring("StructureDefinition/".Length);
				remainder = remainder.Substring(0, remainder.IndexOf("/"));

				// convert this from the old format into the versioned format
				// http://hl7.org/fhir/3.0/StructureDefinition/Account
				// =>
				// http://hl7.org/fhir/StructureDefinition/Account|3.0
				// http://hl7.org/fhir/StructureDefinition/Account|3.0.1
				// http://hl7.org/fhir/StructureDefinition/Account|4.0.1
				// i.e. https://github.com/microsoft/fhir-codegen/blob/dev/src/Microsoft.Health.Fhir.SpecManager/Manager/FhirPackageCommon.cs#L513

				string version = remainder;
                switch (version)
                {
                    case "DSTU2":
                        version = "3.0"; // stub these into the STU3 namespace
                        break;
                    case "3.0":
                    case "3.0.0":
                    case "3.0.1":
                    case "3.0.2":
                    case "STU3":
                        version = "3.0";
                        break;

					case "4.0":
					case "4.0.0":
                    case "4.0.1":
                    case "R4":
                        version = "4.0";
                        break;

					case "4.3":
					case "4.3.0":
                    case "R4B":
                        version = "4.3";
                        break;

					case "5.0":
					case "5.0.0":
                    case "R5":
                        version = "5.0";
                        break;
                    default:
                        return uri;
                }
                return $"{fhirBaseCanonical}StructureDefinition/{resourceName}|{version}";
            }
            return uri;
        }

        public Resource ResolveByCanonicalUri(string uri)
        {
            string convertedUrl = ConvertCanonical(uri);
            Canonical cu = new Canonical(convertedUrl);
            Resource result;
            if (cu.Version() == "3.0")
                result = stu3.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
			else if (cu.Version() == "4.0")
				result = r4.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
			else if (cu.Version() == "4.3")
                result = r4b.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
			else if (cu.Version() == "5.0")
				result = r5.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
			else
				result = fallbackResolver.ResolveByCanonicalUri(cu.BaseCanonicalUrl());
            if (result == null)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to resolve: {uri} at [{cu.Version()}] {cu.BaseCanonicalUrl()}");
            }
            return result;
        }

        public Resource ResolveByUri(string uri)
        {
            string convertedUrl = ConvertCanonical(uri);
            Canonical cu = new Canonical(convertedUrl);
            if (cu.Version() == "3.0")
                return stu3.ResolveByUri(cu.BaseCanonicalUrl());
			if (cu.Version() == "4.0")
				return r4.ResolveByUri(cu.BaseCanonicalUrl());
			if (cu.Version() == "4.3")
				return r4b.ResolveByUri(cu.BaseCanonicalUrl());
			if (cu.Version() == "5.0")
                return r5.ResolveByUri(cu.BaseCanonicalUrl());
            return fallbackResolver.ResolveByUri(cu.BaseCanonicalUrl());
        }
    }
}
