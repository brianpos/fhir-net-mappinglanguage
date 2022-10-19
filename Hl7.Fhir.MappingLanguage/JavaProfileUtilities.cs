/*
  Copyright (c) 2011+, HL7, Inc.
  All rights reserved.

  Redistribution and use in source and binary forms, with or without modification,
  are permitted provided that the following conditions are met:

   * Redistributions of source code must retain the above copyright notice, this
     list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above copyright notice,
     this list of conditions and the following disclaimer in the documentation
     and/or other materials provided with the distribution.
   * Neither the name of HL7 nor the names of its contributors may be used to
     endorse or promote products derived from this software without specific
     prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
  ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
  NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
  WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.

*/

// Ported from https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4b/src/main/java/org/hl7/fhir/r4b/conformance/ProfileUtilities.java

using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using static Hl7.Fhir.Model.ElementDefinition;
using System.Linq;

namespace Hl7.Fhir.MappingLanguage
{
    public class ProfileUtilities
    {
        private static Dictionary<ElementDefinition, List<ElementDefinition>> childMapCache = new Dictionary<ElementDefinition, List<ElementDefinition>>();

        public static List<ElementDefinition> getChildMap(StructureDefinition profile, ElementDefinition element)
        {
            if (childMapCache.ContainsKey(element))
            {
                return childMapCache[element];
            }
            if (element.ContentReference != null)
            {
                List<ElementDefinition> list = null;
                String id = null;
                if (element.ContentReference.StartsWith("#"))
                {
                    // internal reference
                    id = element.ContentReference.Substring(1);
                    list = profile.Snapshot.Element;
                }
                else if (element.ContentReference.Contains("#"))
                {
                    // external reference
                    string refVal = element.ContentReference;
                    StructureDefinition sd = context.fetchResource<StructureDefinition>(refVal.Substring(0, refVal.IndexOf("#")));
                    if (sd == null)
                    {
                        throw new DefinitionException("unable to process contentReference '" + element.ContentReference + "' on element '" + element.ElementId + "'");
                    }
                    list = sd.Snapshot.Element;
                    id = refVal.Substring(refVal.IndexOf("#") + 1);
                }
                else
                {
                    throw new DefinitionException("unable to process contentReference '" + element.ContentReference + "' on element '" + element.ElementId + "'");
                }

                foreach (ElementDefinition e in list)
                {
                    if (id.Equals(e.ElementId))
                        return getChildMap(profile, e);
                }
                throw new DefinitionException(context.formatMessage(I18nConstants.UNABLE_TO_RESOLVE_NAME_REFERENCE__AT_PATH_, element.ContentReference, element.Path));

            }
            else
            {
                List<ElementDefinition> res = new List<ElementDefinition>();
                List<ElementDefinition> elements = profile.Snapshot.Element;
                String path = element.Path;
                for (int index = elements.IndexOf(element) + 1; index < elements.Count(); index++)
                {
                    ElementDefinition e = elements[index];
                    if (e.Path.StartsWith(path + "."))
                    {
                        // We only want direct children, not all descendants
                        if (!e.Path.Substring(path.Length + 1).Contains("."))
                            res.Add(e);
                    }
                    else
                        break;
                }
                childMapCache.Add(element, res);
                return res;
            }
        }

        public void cleanUpDifferential(StructureDefinition sd)
        {
            if (sd.Differential.Element.Count() > 1)
                cleanUpDifferential(sd, 1);
        }

        private void cleanUpDifferential(StructureDefinition sd, int start)
        {
            int level = Utilities.charCount(sd.Differential.Element[start].Path, '.');
            int c = start;
            int len = sd.Differential.Element.Count();
            HashSet<String> paths = new HashSet<String>();
            while (c < len && Utilities.charCount(sd.Differential.Element[c].Path, '.') == level)
            {
                ElementDefinition ed = sd.Differential.Element[c];
                if (!paths.Contains(ed.Path))
                {
                    paths.Add(ed.Path);
                    int ic = c + 1;
                    while (ic < len && Utilities.charCount(sd.Differential.Element[ic].Path, '.') > level)
                        ic++;
                    ElementDefinition slicer = null;
                    List<ElementDefinition> slices = new List<ElementDefinition>();
                    slices.Add(ed);
                    while (ic < len && Utilities.charCount(sd.Differential.Element[ic].Path, '.') == level)
                    {
                        ElementDefinition edi = sd.Differential.Element[ic];
                        if (ed.Path.Equals(edi.Path))
                        {
                            if (slicer == null)
                            {
                                slicer = new ElementDefinition();
                                slicer.Path = edi.Path;
                                slicer.Slicing.Rules = SlicingRules.Open;
                                sd.Differential.Element.Insert(c, slicer);
                                c++;
                                ic++;
                            }
                            slices.Add(edi);
                        }
                        ic++;
                        while (ic < len && Utilities.charCount(sd.Differential.Element[ic].Path, '.') > level)
                            ic++;
                    }
                    // now we're at the end, we're going to figure out the slicing discriminator
                    if (slicer != null)
                        determineSlicing(slicer, slices);
                }
                c++;
                if (c < len && Utilities.charCount(sd.Differential.Element[c].Path, '.') > level)
                {
                    cleanUpDifferential(sd, c);
                    c++;
                    while (c < len && Utilities.charCount(sd.Differential.Element[c].Path, '.') > level)
                        c++;
                }
            }
        }

        private void determineSlicing(ElementDefinition slicer, List<ElementDefinition> slices)
        {
            // first, name them
            int i = 0;
            foreach (ElementDefinition ed in slices)
            {
                if (ed.hasUserData("slice-name"))
                {
                    ed.SliceName = ed.getUserData("slice-name") as string;
                }
                else
                {
                    i++;
                    ed.SliceName = $"slice-{i}";
                }
            }
            // now, the hard bit, how are they differentiated?
            // right now, we hard code this...
            if (slicer.Path.EndsWith(".extension") || slicer.Path.EndsWith(".modifierExtension"))
                slicer.getSlicing().Discriminator.Add(new DiscriminatorComponent()
                {
                    Type = DiscriminatorType.Value,
                    Path = "url"
                });
            else if (slicer.Path.Equals("DiagnosticReport.result"))
                slicer.getSlicing().Discriminator.Add(new DiscriminatorComponent()
                {
                    Type = DiscriminatorType.Value,
                    Path = "reference.code"
                });
            else if (slicer.Path.Equals("Observation.related"))
                slicer.getSlicing().Discriminator.Add(new DiscriminatorComponent()
                {
                    Type = DiscriminatorType.Value,
                    Path = "target.reference.code"
                });
            else if (slicer.Path.Equals("Bundle.entry"))
                slicer.getSlicing().Discriminator.Add(new DiscriminatorComponent()
                {
                    Type = DiscriminatorType.Value,
                    Path = "resource.@profile"
                });
            else
                throw new FHIRException("No slicing for " + slicer.Path);
        }

        private static String sdNs(String type)
        {
            return sdNs(type, null);
        }

        public static String sdNs(String type, String overrideVersionNs)
        {
            if (Utilities.isAbsoluteUrl(type))
                return type;
            else if (overrideVersionNs != null)
                return Utilities.pathURL(overrideVersionNs, type);
            else
                return "http://hl7.org/fhir/StructureDefinition/" + type;
        }
    }
}
