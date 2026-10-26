namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que não virou nota ao importar um lote. Nunca guarda CPF: o inscrito é identificado pela
/// inscrição, e a linha de quem não é inscrito só pelo número da linha.
/// </summary>
public sealed class OcorrenciaDoLoteDeNotas : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid LoteId { get; private init; }

    public TipoDeOcorrenciaDoLote Tipo { get; private init; }

    public Guid? InscricaoId { get; private init; }

    public int[]? NumerosDasLinhas { get; private init; }

    // Construtor de materialização do EF Core.
    private OcorrenciaDoLoteDeNotas()
    {
    }

    public static OcorrenciaDoLoteDeNotas InscritoSemLinha(Guid loteId, Guid inscricaoId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(loteId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(inscricaoId, Guid.Empty);

        return new OcorrenciaDoLoteDeNotas
        {
            LoteId = loteId,
            Tipo = TipoDeOcorrenciaDoLote.InscritoSemLinha,
            InscricaoId = inscricaoId,
        };
    }

    public static OcorrenciaDoLoteDeNotas LinhaSemInscrito(Guid loteId, int numeroDaLinha)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(loteId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(numeroDaLinha, 1);

        return new OcorrenciaDoLoteDeNotas
        {
            LoteId = loteId,
            Tipo = TipoDeOcorrenciaDoLote.LinhaSemInscrito,
            NumerosDasLinhas = [numeroDaLinha],
        };
    }

    public static OcorrenciaDoLoteDeNotas Conflito(Guid loteId, Guid inscricaoId, IReadOnlyList<int> numerosDasLinhas)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(loteId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(inscricaoId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(numerosDasLinhas);

        if (numerosDasLinhas.Count < 2)
        {
            throw new ArgumentException("Um conflito envolve ao menos duas linhas.", nameof(numerosDasLinhas));
        }

        return new OcorrenciaDoLoteDeNotas
        {
            LoteId = loteId,
            Tipo = TipoDeOcorrenciaDoLote.Conflito,
            InscricaoId = inscricaoId,
            NumerosDasLinhas = [.. numerosDasLinhas],
        };
    }
}
