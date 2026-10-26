namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Uma nota individual de uma inscrição numa etapa: uma área do ENEM com a nota e a situação do
/// candidato, vinda de um lote de importação (ADR-0149). Evidência append-only: corrigir uma nota é
/// importar outro lote, nunca alterar este.
/// </summary>
public sealed class LancamentoDeNota : IForensicEntity
{
    public const decimal NotaMaxima = 1000m;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid LoteId { get; private init; }

    public Guid InscricaoId { get; private init; }

    /// <summary>A etapa do lote, repetida aqui para o lançamento ser lido sem o lote.</summary>
    public Guid EtapaProcessoId { get; private init; }

    /// <summary>O código da área do ENEM.</summary>
    public string ItemCodigo { get; private init; } = null!;

    public decimal Nota { get; private init; }

    public SituacaoDaProvaNoEnem Situacao { get; private init; }

    // Construtor de materialização do EF Core.
    private LancamentoDeNota()
    {
    }

    public static LancamentoDeNota Registrar(
        Guid loteId,
        Guid inscricaoId,
        Guid etapaProcessoId,
        string itemCodigo,
        decimal nota,
        SituacaoDaProvaNoEnem situacao)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(loteId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(inscricaoId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(etapaProcessoId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCodigo);

        if (!AreaDoEnem.Todas.Contains(itemCodigo))
        {
            throw new ArgumentException("O item do lançamento deve ser uma das cinco áreas do ENEM.", nameof(itemCodigo));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(nota, 0m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nota, NotaMaxima);

        if (!Enum.IsDefined(situacao))
        {
            throw new ArgumentOutOfRangeException(nameof(situacao), "Situação da prova desconhecida.");
        }

        return new LancamentoDeNota
        {
            LoteId = loteId,
            InscricaoId = inscricaoId,
            EtapaProcessoId = etapaProcessoId,
            ItemCodigo = itemCodigo,
            Nota = nota,
            Situacao = situacao,
        };
    }
}
