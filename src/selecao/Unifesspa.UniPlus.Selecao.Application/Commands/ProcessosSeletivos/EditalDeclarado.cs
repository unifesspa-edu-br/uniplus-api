namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// O que quem publica ou retifica declara para o edital da versão: o número, o período de inscrição quando a
/// configuração não o ancora numa fase, o documento confirmado e os anexos que pede. A conferência o resolve contra
/// a configuração, e o que não foi declarado a versão herda.
/// </summary>
internal sealed record EditalDeclarado(
    string? Numero,
    DateTimeOffset? PeriodoInscricaoInicio,
    DateTimeOffset? PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    IReadOnlyList<AnexoSolicitadoDoEdital>? Anexos);
