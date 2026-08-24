namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirConfiguracaoDivulgacao"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota). Não aceita a regra de abreviação: ela nunca é
/// escolha do cliente, é derivada da regra vigente no instante do congelamento.
/// </summary>
public sealed record DefinirConfiguracaoDivulgacaoRequest(
    [property: VocabularioFechado(
        ConfiguracaoDivulgacao.NumeroInscricao,
        ConfiguracaoDivulgacao.NomeAbreviado,
        ConfiguracaoDivulgacao.Nome)]
    IReadOnlyList<string>? CamposPublicos,
    string? Justificativa);
