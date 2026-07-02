namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

public static class OfertaCursoErrorCodes
{
    // Referências obrigatórias (existência viva checada pelo handler).
    public const string CursoInexistenteOuRemovido = "OfertaCurso.CursoInexistenteOuRemovido";
    public const string LocalOfertaInexistenteOuRemovido = "OfertaCurso.LocalOfertaInexistenteOuRemovido";
    public const string UnidadeOfertanteInexistente = "OfertaCurso.UnidadeOfertanteInexistente";

    // Value object UnidadeOfertante (snapshot-copy, ADR-0061).
    public const string UnidadeOfertanteOrigemObrigatoria = "OfertaCurso.UnidadeOfertanteOrigemObrigatoria";
    public const string UnidadeOfertanteSiglaObrigatoria = "OfertaCurso.UnidadeOfertanteSiglaObrigatoria";
    public const string UnidadeOfertanteSiglaTamanho = "OfertaCurso.UnidadeOfertanteSiglaTamanho";
    public const string UnidadeOfertanteNomeObrigatorio = "OfertaCurso.UnidadeOfertanteNomeObrigatorio";
    public const string UnidadeOfertanteNomeTamanho = "OfertaCurso.UnidadeOfertanteNomeTamanho";
    public const string UnidadeOfertanteTipoObrigatorio = "OfertaCurso.UnidadeOfertanteTipoObrigatorio";
    public const string UnidadeOfertanteTipoTamanho = "OfertaCurso.UnidadeOfertanteTipoTamanho";

    // Domínios fechados dos enums (tokens UPPER_SNAKE).
    public const string ProgramaDeOfertaInvalido = "OfertaCurso.ProgramaDeOfertaInvalido";
    public const string FormatoPedagogicoInvalido = "OfertaCurso.FormatoPedagogicoInvalido";
    public const string TurnoInvalido = "OfertaCurso.TurnoInvalido";

    // Invariantes de coerência.
    public const string BaseLegalObrigatoriaParaProgramaNaoRegular = "OfertaCurso.BaseLegalObrigatoriaParaProgramaNaoRegular";
    public const string VagasAnuaisNegativas = "OfertaCurso.VagasAnuaisNegativas";

    // Tamanhos dos campos textuais opcionais.
    public const string EMecCodigoTamanho = "OfertaCurso.EMecCodigoTamanho";
    public const string CodigoSgaTamanho = "OfertaCurso.CodigoSgaTamanho";
    public const string BaseLegalTamanho = "OfertaCurso.BaseLegalTamanho";
    public const string AtoAutorizacaoMecTamanho = "OfertaCurso.AtoAutorizacaoMecTamanho";

    public const string NaoEncontrada = "OfertaCurso.NaoEncontrada";
}
