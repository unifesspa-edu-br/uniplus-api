namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Application.Commands.ProcessosSeletivos;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirClassificacao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>BaseadoEmEnem</c> é <c>[JsonRequired]</c>: o host não habilita
/// <c>RespectRequiredConstructorParameters</c> (só configura naming policy e
/// enum converter), então por padrão do <c>System.Text.Json</c> um parâmetro
/// de construtor de record ausente no JSON recebe o valor default
/// (<see langword="false"/>) — indistinguível de um <see langword="false"/>
/// explícito. Omitir o campo não pode equivaler silenciosamente a "não é
/// baseado em ENEM".
/// </remarks>
public sealed record DefinirClassificacaoRequest(
    string RegraCalculoCodigo,
    string RegraCalculoVersao,
    string? RegraArredondamentoCodigo,
    string? RegraArredondamentoVersao,
    int? CasasArredondamento,
    string RegraOrdemAlocacaoCodigo,
    string RegraOrdemAlocacaoVersao,
    int NOpcoesAlocacao,
    IReadOnlyList<RegraEliminacaoInput> RegrasEliminacao,
    [property: JsonRequired] bool BaseadoEmEnem);
