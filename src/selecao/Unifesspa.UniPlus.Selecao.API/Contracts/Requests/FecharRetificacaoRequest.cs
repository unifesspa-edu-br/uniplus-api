namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.FecharRetificacao"/> — o do atalho atômico
/// <b>menos o motivo</b>, que já foi declarado na abertura e vive no rascunho.
/// </summary>
public sealed record FecharRetificacaoRequest(
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato);
