namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>O peso de uma área do ENEM no grupo de área de um curso.</summary>
public sealed record PesoDaArea(string AreaCodigo, decimal Peso);

/// <summary>
/// A nota da etapa de uma inscrição numa oferta concorrida. A nota só existe quando a situação é
/// <see cref="SituacaoDaAvaliacao.Apurada"/>; a falta dela nunca vira zero.
/// </summary>
public sealed record AvaliacaoApurada(
    Guid InscricaoId,
    Guid OfertaCursoId,
    string GrupoAreaEnemCodigo,
    SituacaoDaAvaliacao Situacao,
    decimal? Nota);
