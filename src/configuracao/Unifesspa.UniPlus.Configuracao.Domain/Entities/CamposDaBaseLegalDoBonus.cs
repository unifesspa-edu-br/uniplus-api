namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>Os campos de uma base legal do bônus regional depois de conferidos.</summary>
internal sealed record CamposDaBaseLegalDoBonus(
    TipoInstrumentoNormativo TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<BaseLegalBonusRegionalMunicipio> Municipios);
