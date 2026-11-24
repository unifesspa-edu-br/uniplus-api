namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// O procedimento que o edital declara para revisar o recurso de uma matéria: o tipo de decisão recorrida, o alvo
/// opcional dentro dela e a regra do catálogo que diz como revisar.
/// </summary>
/// <param name="TipoDeDecisao">Código do vocabulário de tipos de decisão, como a fase e a etapa o declaram.</param>
/// <param name="Alvo">A parte da matéria que o procedimento cobre; ausente quando cobre o tipo de decisão inteiro.</param>
/// <param name="RegraCodigo">Código da regra do tipo <c>REGRA_PROCEDIMENTO_RECURSO</c> no catálogo.</param>
/// <param name="RegraVersao">Versão da regra no catálogo.</param>
public sealed record ProcedimentoDeRecursoInput(
    string? TipoDeDecisao,
    AlvoDoProcedimentoDeRecursoInput? Alvo,
    string? RegraCodigo,
    string? RegraVersao);

/// <summary>O alvo de um procedimento: o item de avaliação da etapa pela identidade dele, ou a área do ENEM; só um dos dois.</summary>
public sealed record AlvoDoProcedimentoDeRecursoInput(Guid? ItemDeAvaliacaoId, string? AreaDoEnem);
