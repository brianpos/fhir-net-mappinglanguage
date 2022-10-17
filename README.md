## Introduction ##
This is a direct port of the Java implementation of the [HL7 FHIR Mapping Language][fhir-spec] on the Microsoft .NET (dotnet) platform.
https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/utils/StructureMapUtilities.java

The file/code structure is planned to remain inline with the java implementation to ease maintenance.

## Release notes ##
Not released yet!

## What's in the box?
This library provides:
* FHIR Mapping Language (FML) Parser - returning a Firely SDK StructureMap POCO object
* Serializer to output a FML representation of a StructureMap POCO object
* Evaluation of FML transforms


## Getting Started ##
Coming soon

## Support 
Exploratory project at this stage, but may support this via issues coming in through the GitHub repository at [https://github.com/brianpos/fhir-net-mappinglanguage/issues](https://github.com/brianpos/fhir-net-mappinglanguage/issues). 
You are welcome to register your bugs and feature suggestions there. For questions and broader discussions, we use the .NET FHIR Implementers chat on [Zulip][netsdk-zulip].

## Contributing ##
We are welcoming contributors!

If you want to participate in this project, we're using [Git Flow][nvie] for our branch management, so please submit your commits using pull requests on the correct `develop-r4`/`develop-r4B` branches as mentioned above! 


### GIT branching strategy 
- [NVIE](http://nvie.com/posts/a-successful-git-branching-model/)
- Or see: [Git workflow](https://www.atlassian.com/git/workflows#!workflow-gitflow)

### FML language Issues
https://jira.hl7.org/browse/FHIR-39282

[netsdk-zulip]: https://chat.fhir.org/#narrow/stream/dotnet
[nvie]: http://nvie.com/posts/a-successful-git-branching-model/
[fhir-spec]: http://www.hl7.org/fhir/mapping-language.html
