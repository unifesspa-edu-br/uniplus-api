namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A nota da etapa de uma inscrição numa oferta de curso que ela concorre (1ª ou 2ª opção), apurada
/// pelos pesos do grupo de área do curso quando o lote foi importado. Sem nota quando a inscrição não
/// tem linha no arquivo ou tem linhas em conflito: a situação diz o motivo, e a falta nunca vira zero.
/// </summary>
/// <remarks>
/// Evidência append-only, filha do lote: vale com ele, pela vigência do lote mais recente do escopo.
/// A nota e a situação de cada área ficam nos lançamentos do mesmo lote, não aqui.
/// </remarks>
public sealed class AvaliacaoDaEtapa : IForensicEntity
{
    public const decimal NotaMaxima = 1000m;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid LoteId { get; private init; }

    /// <summary>A etapa do lote, repetida para ler a avaliação sem o lote.</summary>
    public Guid EtapaProcessoId { get; private init; }

    public Guid InscricaoId { get; private init; }

    /// <summary>A oferta de curso concorrida, guardada por valor: pertence à configuração do processo.</summary>
    public Guid OfertaCursoId { get; private init; }

    /// <summary>O código do grupo de área cujos pesos apuraram a nota.</summary>
    public string GrupoAreaEnemCodigo { get; private init; } = null!;

    public SituacaoDaAvaliacao Situacao { get; private init; }

    /// <summary>Sem arredondamento; nula quando a situação não é <see cref="SituacaoDaAvaliacao.Apurada"/>.</summary>
    public decimal? Nota { get; private init; }

    // Construtor de materialização do EF Core.
    private AvaliacaoDaEtapa()
    {
    }

    public static AvaliacaoDaEtapa Registrar(Guid loteId, Guid etapaProcessoId, AvaliacaoApurada apurada)
    {
        ArgumentNullException.ThrowIfNull(apurada);
        ArgumentOutOfRangeException.ThrowIfEqual(loteId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(etapaProcessoId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(apurada.InscricaoId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(apurada.OfertaCursoId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(apurada.GrupoAreaEnemCodigo);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(apurada.GrupoAreaEnemCodigo.Length, GrupoAreaEnemSnapshot.CodigoMaxLength);

        if (!Enum.IsDefined(apurada.Situacao))
        {
            throw new ArgumentOutOfRangeException(nameof(apurada), "Situação da avaliação desconhecida.");
        }

        if ((apurada.Situacao == SituacaoDaAvaliacao.Apurada) != apurada.Nota.HasValue)
        {
            throw new ArgumentException("Só a avaliação apurada tem nota, e toda avaliação apurada tem.", nameof(apurada));
        }

        if (apurada.Nota is { } nota)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(nota, 0m);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(nota, NotaMaxima);
        }

        return new AvaliacaoDaEtapa
        {
            LoteId = loteId,
            EtapaProcessoId = etapaProcessoId,
            InscricaoId = apurada.InscricaoId,
            OfertaCursoId = apurada.OfertaCursoId,
            GrupoAreaEnemCodigo = apurada.GrupoAreaEnemCodigo,
            Situacao = apurada.Situacao,
            Nota = apurada.Nota,
        };
    }
}
