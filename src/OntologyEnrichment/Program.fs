module Program

open OntologyEnrichment
open Argu

[<EntryPoint>]
let main args = 
    
    let argumentParser = ArgumentParser.Create<OntologyEnrichmentCommand>(programName = "OntologyEnrichment")
    
    let parseResult = argumentParser.ParseCommandLine(inputs = args, ignoreMissing = true, ignoreUnrecognized = true) 

    match parseResult.GetSubCommand() with
    | Enrich enrichArgs -> 
        Enrichment.run enrichArgs

    | Annotate annotateArgs -> 
        Annotate.run annotateArgs
        

    1
