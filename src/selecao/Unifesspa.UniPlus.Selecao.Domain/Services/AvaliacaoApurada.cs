namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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
    decimal? Nota)
{
    /// <summary>A primeira incoerência da avaliação, nomeando o campo; nula quando ela pode ser registrada.</summary>
    internal string? Violacao() => ViolacaoDaIdentificacao() ?? ViolacaoDaNota();

    private string? ViolacaoDaIdentificacao() =>
        InscricaoId == Guid.Empty ? "A avaliação precisa da inscrição."
        : OfertaCursoId == Guid.Empty ? "A avaliação precisa da oferta de curso."
        : string.IsNullOrWhiteSpace(GrupoAreaEnemCodigo) ? "A avaliação precisa do código do grupo de área."
        : GrupoAreaEnemCodigo.Length > GrupoAreaEnemSnapshot.CodigoMaxLength ? $"O código do grupo de área passa de {GrupoAreaEnemSnapshot.CodigoMaxLength} caracteres."
        : null;

    private string? ViolacaoDaNota()
    {
        if (!Enum.IsDefined(Situacao))
        {
            return "Situação da avaliação desconhecida.";
        }

        if ((Situacao == SituacaoDaAvaliacao.Apurada) != Nota.HasValue)
        {
            return "Só a avaliação apurada tem nota, e toda avaliação apurada tem.";
        }

        return Nota is { } nota && (nota < 0m || nota > AvaliacaoDaEtapa.NotaMaxima)
            ? $"A nota fica entre 0 e {AvaliacaoDaEtapa.NotaMaxima}."
            : null;
    }
}
