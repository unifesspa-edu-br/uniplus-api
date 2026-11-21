namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Representação pública de um tipo de conceito: a identidade (<c>Id</c> e <c>Codigo</c>, imutável), o estado
/// <c>Ativo</c> e a <c>Definicao</c>, que o <c>PUT</c> recebe de volta.
/// </summary>
public sealed record TipoConceitoDto(
    Guid Id,
    string Codigo,
    bool Ativo,
    DefinicaoDoTipoConceitoDto Definicao)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>
/// O que a gravação aceita de um tipo de conceito além do código: o <c>Numerico</c> só no numérico e os <c>Valores</c> só
/// no categórico. A natureza volta com código e rótulo, e o <c>PUT</c> a recebe só pelo código.
/// </summary>
public sealed record DefinicaoDoTipoConceitoDto(
    string Nome,
    string? Descricao,
    CodigoComRotulo<NaturezaConceito> Natureza,
    EscalaNumericaDto? Numerico,
    IReadOnlyList<ValorCategoricoDto>? Valores);

/// <summary>A escala de um tipo de conceito numérico.</summary>
public sealed record EscalaNumericaDto(decimal EscalaMinima, decimal EscalaMaxima, int CasasDecimais);

/// <summary>Um valor de um tipo de conceito categórico; a posição na lista é a ordem do valor.</summary>
public sealed record ValorCategoricoDto(string Codigo, string Rotulo, bool ContaComoAprovacao);
