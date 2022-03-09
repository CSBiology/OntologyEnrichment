namespace OntologyEnrichment

open Argu

type Annotations =
    | MapMan
    | MapManDes
    | GO
    | GODes
    | TrivialName
    | Localization

type Species =
    | Arabidopsis
    | Chlamydomonas

type AnnotationArgs =
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-i")>] InputPath           of path:string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-o")>] OutputPath          of path:string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-c")>] ColumnHeader        of string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-t")>] ColumnSeparator     of string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-s")>] Species             of Species
    |            [<Mandatory>] [<AltCommandLine("-a")>] Annotation          of Annotations
    | [<Unique>]               [<AltCommandLine("-x")>] AnnotationSeparator of string
    | [<Unique>]               [<AltCommandLine("-y")>] IdentifierSeparator of string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | InputPath x       -> "input file path"
            | OutputPath x      -> "output file path"
            | ColumnHeader x    -> "columnheader of identifier column in annotation table"
            | ColumnSeparator x -> "column separator (tab , ; ...)"
            | Species x         -> "Organism"
            | Annotation x      -> "annotation"
            | AnnotationSeparator x -> "multiple annotation separator"
            | IdentifierSeparator x -> "multiple identifier separator"

type EnrichmentArgs =
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-i")>] InputPath           of path:string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-o")>] OutputPath          of path:string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-t")>] ColumnSeparator     of string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-c")>] IdColumnHeader      of colHeader:string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-a")>] AnnotationColumnHeader      of colHeader:string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-s")>] SignificanceColumnHeader    of colHeader:string
    | [<Unique>] [<Mandatory>] [<AltCommandLine("-p")>] SignificanceCriterion       of group:int
    |                          [<AltCommandLine("-z")>] AnnotationSeparator of string
    | [<Unique>]               [<AltCommandLine("-e")>] ExpandOntologyTree  of bool

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | InputPath x           -> "input file path"
            | OutputPath x          -> "output file path"
            | ColumnSeparator x     -> "column separator (tab , ; ...)"
            | IdColumnHeader x      -> "columnheader of identifier column in table"
            | AnnotationColumnHeader   x  -> "columnheader of annotation column in table"
            | SignificanceColumnHeader x  -> "columnheader of significance column in table"
            | SignificanceCriterion x  -> "group index of positive group (significant items)"
            | AnnotationSeparator x -> "multiple annotation separator"
            | ExpandOntologyTree x  -> "defines if annotation terms are expanded (25.4.3 -> 25; 25.4; 25.4.3)"


type OntologyEnrichmentCommand =

    ///Commands
    | [<CliPrefix(CliPrefix.None)>] Annotate    of annotation_args  : ParseResults<AnnotationArgs>
    | [<CliPrefix(CliPrefix.None)>] Enrich      of enrichment_args  : ParseResults<EnrichmentArgs>

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Annotate x    -> "Annotate"
            | Enrich x      -> "Enrich"
