namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>Representação pública de um tipo de conceito: o <c>Numerico</c> só no numérico e os <c>Valores</c> só no categórico.</summary>
public sealed record TipoConceitoDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    CodigoComRotulo<NaturezaConceito> Natureza,
    bool Ativo,
    EscalaNumericaDto? Numerico,
    IReadOnlyList<ValorCategoricoDto>? Valores)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>A escala de um tipo de conceito numérico.</summary>
public sealed record EscalaNumericaDto(decimal EscalaMinima, decimal EscalaMaxima, int CasasDecimais);

/// <summary>Um valor de um tipo de conceito categórico; a posição na lista é a ordem do valor.</summary>
public sealed record ValorCategoricoDto(string Codigo, string Rotulo, bool ContaComoAprovacao);
