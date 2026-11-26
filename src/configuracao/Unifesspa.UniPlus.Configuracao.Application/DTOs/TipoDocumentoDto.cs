namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// DTO de resposta HTTP para <c>TipoDocumento</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). A categoria é exposta como <c>string</c> (token
/// canônico UPPER_SNAKE); o tamanho máximo como número (MB). A classificação de proteção e a
/// hipótese legal trazem código e rótulo. A leitura é pública: o tipo só
/// informa se a proteção está aprovada; a situação detalhada fica na fila da proteção de dados.
/// </summary>
public sealed record TipoDocumentoDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Categoria,
    string? FormatosAceitos,
    int? TamanhoMaximoMb,
    string? TipoEquivalente,
    CodigoComRotulo<ClassificacaoDeProtecao> ClassificacaoProtecao,
    string FinalidadeTratamento,
    CodigoComRotulo<HipoteseLegalTratamento> HipoteseLegal,
    DateTimeOffset CriadoEm,
    bool ProtecaoAprovada)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
