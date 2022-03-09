namespace OntologyEnrichment

module Enrichment = 

    open BioFSharp.Stats
    open FSharp.Stats
    open System
    open Argu
    
    //read user input file
    let getDataFrame (columnSeparator:string) inputPath =
        System.IO.File.ReadAllLines(inputPath)
        |> Array.map (fun x -> x.Split([|columnSeparator|],StringSplitOptions.None))

    //get index of column header that contains the identifier
    let getIndexOfId idColumnHeader headerLine = 
        headerLine
        |> Array.tryFindIndex (fun c -> c = idColumnHeader)
        |> fun i -> 
            match i with
            | Some x -> x
            | None -> failwithf "idColumnHeader %A not present in data" idColumnHeader

    //get index of column header that contains ontology terms
    let getIndexOfAnnotation annotationType headerLine = 
        headerLine
        |> Array.tryFindIndex (fun c -> c = annotationType)
        |> fun i -> 
            match i with
            | Some x -> x
            | None -> failwithf "AnnotationType %A not present in data" annotationType
        
    //get index of column indicating a significance
    let getIndexOfSignificance sigColumnHeader headerLine = 
        headerLine
        |> Array.tryFindIndex (fun c -> c = sigColumnHeader)
        |> fun i -> 
            match i with
            | Some x -> x
            | None -> failwithf "sigColumnHeader %A not present in data" sigColumnHeader

    let getEnrichment (idColumnHeader:string) (annotationType) (annotationSeparators:string[]) sigColumnHeader (sigCriterion:int) expandOntologyTree (dataFrame:string[][]) =
        let indexOfId            = getIndexOfId           idColumnHeader  dataFrame.[0]
        let indexOfAnnotation    = getIndexOfAnnotation   annotationType  dataFrame.[0]
        let indexOfSignificance  = getIndexOfSignificance sigColumnHeader dataFrame.[0]
        dataFrame
        |> Array.tail
        |> Array.map (fun str -> 
            let id = str.[indexOfId]
            let annotation = str.[indexOfAnnotation].Replace("\"","").Split(annotationSeparators,StringSplitOptions.None)
            let sigIndex = int str.[indexOfSignificance]
            annotation
            |> Seq.map (fun a -> OntologyEnrichment.createOntologyItem id a sigIndex id)
            |> fun ontologyItems -> 
                if expandOntologyTree then 
                    OntologyEnrichment.expandOntologyTree ontologyItems
                else ontologyItems
            )
        |> Seq.concat
        |> fun x -> 
            sigCriterion,OntologyEnrichment.CalcOverEnrichment sigCriterion None None x

    let writeEnrichment (sigCriterion:int) outputPath (enrichments:seq<OntologyEnrichment.GseaResult<string>>) = 
        let header = "Term\tTotalUniverse\tTotalNumberOfDE\tNumberInBin\tNumberOFDEsInBin\tpValue\tFDR\tItems"
        let pValAdj = 
            enrichments 
            |> Seq.map (fun x -> x.PValue)
            |> Testing.MultipleTesting.benjaminiHochbergFDR
            |> Array.ofSeq
        
        enrichments
        |> Seq.mapi (fun i x -> 
            let identifier = (x.ItemsInBin |> Seq.filter (fun x -> x.GroupIndex=sigCriterion)|> Seq.map (fun x -> x.Id) |> String.concat ";")
            sprintf "%s\t%i\t%i\t%i\t%i\t%f\t%f\t%s" x.OntologyTerm x.TotalUniverse x.TotalNumberOfDE x.NumberInBin x.NumberOfDEsInBin x.PValue pValAdj.[i] identifier 
            )
        |> Seq.append [|header|]
        |> fun dtw -> 
            System.IO.File.WriteAllLines(outputPath,dtw)
            printfn "Enrichment output written to: %s" outputPath
    
    /// \t or "\t" doesnt work as argument
    let getSeparator str = 
        match str with
        | "tab"             -> "\t"
        | "tabulator"       -> "\t"
        | _ -> str
   
    let run (enrichmentArgs : ParseResults<EnrichmentArgs>) =
    
        // arguments are translated
        let inputPath           = enrichmentArgs.GetResult(InputPath)
        let outputFilePath      = enrichmentArgs.GetResult(OutputPath)
        let colSeparator        = getSeparator (enrichmentArgs.GetResult(ColumnSeparator))
        let idColumnHeader      = enrichmentArgs.GetResult(IdColumnHeader)
        let annotationHeader    = enrichmentArgs.GetResult(AnnotationColumnHeader)
        let significanceHeader  = enrichmentArgs.GetResult(SignificanceColumnHeader)
        let annotationSeparator = (enrichmentArgs.GetResults(AnnotationSeparator)) |> Seq.map getSeparator |> Array.ofSeq
        let sigCriterion        = enrichmentArgs.GetResult(SignificanceCriterion)
        let expandOntologyTree  = enrichmentArgs.GetResult(ExpandOntologyTree,false)
    
        /// create Output folder if not already existing
        let outputFolderPath = 
            let relativeDir = 
                let chunks = enrichmentArgs.GetResult(OutputPath).Split([|'/';'\\'|])
                chunks.[..chunks.Length-2]
                |> String.concat "/"
            relativeDir + "/"
        System.IO.Directory.CreateDirectory(outputFolderPath) |> ignore

        let (groupIndex,enrichment) = 
            getDataFrame colSeparator inputPath
            |> getEnrichment idColumnHeader annotationHeader annotationSeparator significanceHeader sigCriterion expandOntologyTree
    
        writeEnrichment groupIndex outputFilePath enrichment
