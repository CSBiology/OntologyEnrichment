namespace OntologyEnrichment

module Annotate =

    open Argu
    open System
    open System.IO
    open System.IO.Compression

    /// translates annotation types to strings (required for new column header)
    let annotationTypeToString str = 
        match str with
        | MapMan        -> "MapMan"
        | MapManDes     -> "MapManDescription"
        | GO            -> "GO"
        | GODes         -> "GODescription"
        | Localization  -> "Localization"
        | TrivialName   -> "TrivialName"

    /// Snapshots of MapMan and Gene Ontology were generated with FATool and genome releases 5.5 (Chlamy) and Araport11 (Arabidopsis)
    module Mapping =
    
        type Annotation =
            {
            Identifier          :string
            MapMan              :string
            MapManDescription   :string
            SubLocalization     :string
            GO                  :string
            GODescription       :string
            Synonym             :string
            } with 
                static member Create id mm mmdes loc go godes syn =
                    {
                    Identifier          = id 
                    MapMan              = mm
                    MapManDescription   = mmdes
                    SubLocalization     = loc
                    GO                  = go
                    GODescription       = godes
                    Synonym             = syn
                    }

        /// reads FA tool snapshot for araport11 or chlamyJGI_v5.5
        let readMapping species = 

            let mappingFilePath = 
                match species with 
                | Chlamydomonas -> "chlamy_jgi55.txt.gz"
                | Arabidopsis -> "arabidopsis_araport11.txt.gz"

            let assembly = System.Reflection.Assembly.GetExecutingAssembly()
            let lns =
                seq{
                    let outStream = new MemoryStream()
                    use stream = assembly.GetManifestResourceStream("OntologyEnrichment.external." + mappingFilePath)            
                    use unzip = new GZipStream(stream, mode=CompressionMode.Decompress, leaveOpen=true)
                    unzip.CopyTo(outStream)
                    outStream.Seek(0L,SeekOrigin.Begin) |> ignore
                    use textReader = new StreamReader(outStream, encoding=System.Text.Encoding.Default)
                    while not textReader.EndOfStream do
                       yield textReader.ReadLine()}

            lns
            |> Seq.skip 2
            |> Seq.map (fun x ->
                let tmp = x.Split '\t'
                tmp.[0],Annotation.Create tmp.[0] tmp.[1] tmp.[2] tmp.[3] tmp.[4] tmp.[5] tmp.[6]
                )
            |> Map.ofSeq

        let creMapping = readMapping Chlamydomonas
        let araMapping = readMapping Arabidopsis

        let reportBlank i id =
            if id = "" then 
                printfn "Warning: identifier in line %i is empty" i
            else
                printfn "Warning: Could not find identifier %s (line %i)" id i
            ""

        // annotations are separated by ';'. The user can specify an alternative separator.
        let getMapMan (mappingFile:Map<string,Annotation>) multipleAnnotationSeparator i id = 
            if mappingFile.ContainsKey id then 
                mappingFile.[id].MapMan.Split ';' 
                |> String.concat multipleAnnotationSeparator
            else reportBlank i id

        let getMapManDescription (mappingFile:Map<string,Annotation>) multipleAnnotationSeparator i id = 
            if mappingFile.ContainsKey id then 
                mappingFile.[id].MapManDescription.Split ';' 
                |> String.concat multipleAnnotationSeparator 
            else 
                reportBlank i id

        let getGO (mappingFile:Map<string,Annotation>) multipleAnnotationSeparator i id = 
            if mappingFile.ContainsKey id then 
                mappingFile.[id].GO.Split ';' 
                |> String.concat multipleAnnotationSeparator
            else reportBlank i id

        let getGODescription (mappingFile:Map<string,Annotation>) multipleAnnotationSeparator i id = 
            if mappingFile.ContainsKey id then 
                mappingFile.[id].GODescription.Split ';' 
                |> String.concat multipleAnnotationSeparator 
            else reportBlank i id

        let getLoc (mappingFile:Map<string,Annotation>) multipleAnnotationSeparator i id = 
            if mappingFile.ContainsKey id then 
                mappingFile.[id].SubLocalization 
            else reportBlank i id
    
        let getSyn (mappingFile:Map<string,Annotation>) multipleAnnotationSeparator i id = 
            if mappingFile.ContainsKey id then 
                mappingFile.[id].Synonym         
            else reportBlank i id

        let getAnnotation (mappingFile:Map<string,Annotation>) annotation multipleAnnotationSeparator i id =
            match annotation with
            | MapMan        -> getMapMan mappingFile multipleAnnotationSeparator i id
            | MapManDes     -> getMapManDescription mappingFile multipleAnnotationSeparator i id
            | GO            -> getGO mappingFile multipleAnnotationSeparator i id
            | GODes         -> getGODescription mappingFile multipleAnnotationSeparator i id
            | TrivialName   -> getSyn mappingFile multipleAnnotationSeparator i id
            | Localization  -> getLoc mappingFile multipleAnnotationSeparator i id


    /// User data is read, annotated and written to a new file
    module Data = 
    
        let getDataFrame (columnSeparator:string) inputPath = 
            System.IO.File.ReadAllLines(inputPath)
            |> Array.map (fun x ->
                x.Split([|columnSeparator|],System.StringSplitOptions.None)
                )

        /// index of column that contains the identifier to annotate
        let getColIndex (dataFrame:string[][]) columnHeader= 
            Array.tryFindIndex (fun x -> x = columnHeader) dataFrame.[0]
            |> fun o -> 
                match o with
                | Some i -> i
                | _ -> failwithf "ColumnHeader %s not found." columnHeader
    
        /// based on given mapping arguments the file is extended with given mapping columns
        let getHeader (dataFrame:string[][]) (columnSeparator:string) (annotations:Annotations[]) = 
            let annotationColumnHeader = 
                //Arguments.mappings 
                annotations
                |> Array.map annotationTypeToString 
            Array.append dataFrame.[0] annotationColumnHeader
            |> String.concat columnSeparator
    
        /// Cre identifier have to be truncated prior to annotating
        let truncatedCreIdentifier species (id:string) = 
            match species with
            | Arabidopsis -> id
            | Chlamydomonas -> (id.Split '.').[..1] |> String.concat "."

        /// every row of the file is processed and converted to a new string with additional information attached at the end of the line
        let getRows inputPath (columnSeparator:string) columnHeader species (annotations:Annotations[]) (multipleIdentifierSeparator:string) (multipleAnnotationSeparator:string) = 
        
            let dataFrame =     getDataFrame columnSeparator inputPath
        
            let mappingFile =
                match species with 
                | Arabidopsis -> Mapping.araMapping
                | Chlamydomonas -> Mapping.creMapping
        
            let colIndex =      getColIndex dataFrame columnHeader
        
            let header =        getHeader dataFrame columnSeparator annotations

            dataFrame
            |> Array.tail
            |> Array.map (fun x -> 
                // if several identifiers are written in one cell, they have to be split up, annotated separately, and then be merged again afterwards
                let identifiers = 
                    x.[colIndex].Split([|multipleIdentifierSeparator|],System.StringSplitOptions.None)
                    |> Array.map (truncatedCreIdentifier species)
                // every annotation defined by the user is generadet using the separated identifiers
                let annotations = 
                    annotations
                    |> Array.mapi (fun i mapping -> 
                        identifiers
                        |> Array.map (Mapping.getAnnotation mappingFile mapping multipleAnnotationSeparator i)
                        |> Array.filter (fun x -> x <> "")
                        |> String.concat multipleIdentifierSeparator
                    )
                Array.append x annotations
                |> String.concat columnSeparator
                )
            |> Array.append [|header|]

        let annotateAndWriteData inputPath (columnSeparator:string) columnHeader species (annotations:Annotations[]) (multipleIdentifierSeparator:string) (multipleAnnotationSeparator:string) outputPath =
            /// warns user if separators are identical
            if columnSeparator = multipleIdentifierSeparator then printfn "WARNING: Column separator is equal to identifier separator"
            if columnSeparator = multipleAnnotationSeparator then printfn "WARNING: Column separator is equal to annotation separator"
            let annotatedRows = 
                getRows inputPath columnSeparator columnHeader species annotations multipleIdentifierSeparator multipleAnnotationSeparator
            System.IO.File.WriteAllLines(outputPath,annotatedRows)
            printfn "Output written to: %s" outputPath


    let run (annotationArgs : ParseResults<AnnotationArgs>) =

        /// \t or "\t" doesnt work as argument
        let getSeparator str = 
            match str with
            | "tab"             -> "\t"
            | "tabulator"       -> "\t"
            | _ -> str
   
        // arguments are translated
        let inputPath        = annotationArgs.GetResult(AnnotationArgs.InputPath)//@"/arc/assays/" + vennR.GetResult(InputPath)
        let outputFilePath   = annotationArgs.GetResult(AnnotationArgs.OutputPath)
        let columnHeader     = annotationArgs.GetResult(ColumnHeader)
        let species          = annotationArgs.GetResult(Species)
        let separator        = getSeparator (annotationArgs.GetResult(AnnotationArgs.ColumnSeparator))
        let mappings         = annotationArgs.GetResults(Annotation) |> Array.ofList
        let multipleIdentifierSeparator  = getSeparator (annotationArgs.GetResult(AnnotationArgs.IdentifierSeparator,";"))
        let multipleAnnotationSeparator  = getSeparator (annotationArgs.GetResult(AnnotationArgs.AnnotationSeparator,"|"))
    
        /// create Output folder if not already existing
        let outputFolderPath = 
            let relativeDir = 
                let chunks = annotationArgs.GetResult(AnnotationArgs.OutputPath).Split([|'/';'\\'|])
                chunks.[..chunks.Length-2]
                |> String.concat "/"
            relativeDir + "/"

        System.IO.Directory.CreateDirectory(outputFolderPath) |> ignore

        Data.annotateAndWriteData inputPath separator columnHeader species mappings multipleIdentifierSeparator multipleAnnotationSeparator outputFilePath
