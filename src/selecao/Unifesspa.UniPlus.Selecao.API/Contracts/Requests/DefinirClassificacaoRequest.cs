namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Application.Commands.ProcessosSeletivos;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirClassificacao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirClassificacaoRequest(
    string RegraCalculoCodigo,
    string RegraCalculoVersao,
    string? RegraArredondamentoCodigo,
    string? RegraArredondamentoVersao,
    int? CasasArredondamento,
    string RegraOrdemAlocacaoCodigo,
    string RegraOrdemAlocacaoVersao,
    int NOpcoesAlocacao,
    IReadOnlyList<RegraEliminacaoInput> RegrasEliminacao);
