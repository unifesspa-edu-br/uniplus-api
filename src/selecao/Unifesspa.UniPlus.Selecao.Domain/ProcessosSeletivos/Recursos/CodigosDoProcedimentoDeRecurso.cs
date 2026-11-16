namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

/// <summary>
/// Os códigos de erro das recusas da declaração de procedimentos, que são os mesmos para a janela de etapa e a de
/// fase e diferem só no prefixo da entidade. A conferência é uma só e recebe o conjunto da janela que confere.
/// </summary>
/// <remarks>
/// Constantes literais por entidade, e não montadas por concatenação: o registro de erros da API só enxerga literal.
/// </remarks>
public sealed record CodigosDoProcedimentoDeRecurso(
    string Desconhecido,
    string Duplicado,
    string Conflitante,
    string AlvoInexistente,
    string ParaDecisaoNaoDeclarada)
{
    public static CodigosDoProcedimentoDeRecurso DaEtapa { get; } = new(
        "RecursoDaEtapa.ProcedimentoDeRecursoDesconhecido",
        "RecursoDaEtapa.ProcedimentoDeRecursoDuplicado",
        "RecursoDaEtapa.ProcedimentoDeRecursoConflitante",
        "RecursoDaEtapa.AlvoDoProcedimentoDeRecursoInexistente",
        "RecursoDaEtapa.ProcedimentoParaDecisaoNaoDeclarada");

    public static CodigosDoProcedimentoDeRecurso DaFase { get; } = new(
        "RegraRecursoFase.ProcedimentoDeRecursoDesconhecido",
        "RegraRecursoFase.ProcedimentoDeRecursoDuplicado",
        "RegraRecursoFase.ProcedimentoDeRecursoConflitante",
        "RegraRecursoFase.AlvoDoProcedimentoDeRecursoInexistente",
        "RegraRecursoFase.ProcedimentoParaDecisaoNaoDeclarada");
}
