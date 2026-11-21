namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>Representação pública de um tipo de etapa ativo.</summary>
public sealed record TipoEtapaDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    bool Ativo,
    bool AdmitePontuacao,
    bool AdmiteEliminacao,
    bool NotaDeOrigemNoEnem,
    bool ExigeCorrecaoIndependente,
    IReadOnlyList<ConceitoAdmitidoDto> ConceitosAdmitidos,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>Um tipo de conceito admitido pelo tipo de etapa; a natureza sai com código e rótulo.</summary>
public sealed record ConceitoAdmitidoDto(string Codigo, string Nome, CodigoComRotulo<NaturezaConceito> Natureza);
