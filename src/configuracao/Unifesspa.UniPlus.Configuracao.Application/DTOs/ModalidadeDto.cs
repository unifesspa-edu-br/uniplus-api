namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// DTO de resposta HTTP para <c>Modalidade</c> de concorrência. Suporta HATEOAS
/// Level 1 via <c>_links</c> (ADR-0029). Os enums saem como código e rótulo;
/// o remanejamento das vagas sai como um bloco, nulo quando a modalidade não remaneja. A regra de
/// aplicabilidade sai como OU de cláusulas, cada uma o E de condições; vazia na incondicional.
/// <c>LegalFixa</c> diz se a modalidade é do catálogo legal fixo, cuja estrutura e regra vêm da
/// norma e só aceitam editar a descrição e a base legal.
/// </summary>
public sealed record ModalidadeDto(
    Guid Id,
    string Codigo,
    bool LegalFixa,
    string? Descricao,
    CodigoComRotulo<NaturezaLegal> NaturezaLegal,
    CodigoComRotulo<ComposicaoVagas> ComposicaoVagas,
    string? ComposicaoOrigem,
    RemanejamentoDasVagasDto? Remanejamento,
    IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoDto>> RegraDeAplicabilidade,
    CodigoComRotulo<AcaoQuandoIndeferido>? AcaoQuandoIndeferido,
    string? BaseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>
/// Para onde vão as vagas não preenchidas da modalidade: a regra e, conforme ela, o destino único ou o par de modalidades
/// com o fallback.
/// </summary>
public sealed record RemanejamentoDasVagasDto(
    CodigoComRotulo<RegraRemanejamento> Regra,
    string? Destino,
    string? Par,
    string? Fallback);
