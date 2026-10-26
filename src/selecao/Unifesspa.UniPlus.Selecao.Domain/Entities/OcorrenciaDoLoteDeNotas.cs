namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que não virou nota ao importar um lote. O inscrito é identificado pela inscrição. A linha de quem
/// não é inscrito guarda o número da linha, o CPF só mascarado, o nome e o sexo que o arquivo traz,
/// para achá-la e reconhecer a pessoa; o CPF completo nunca é gravado.
/// </summary>
public sealed class OcorrenciaDoLoteDeNotas : IForensicEntity
{
    /// <summary>O tamanho máximo do nome gravado na linha sem inscrito.</summary>
    public const int TamanhoMaximoDoNome = 200;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid LoteId { get; private init; }

    public TipoDeOcorrenciaDoLote Tipo { get; private init; }

    public Guid? InscricaoId { get; private init; }

    public int[]? NumerosDasLinhas { get; private init; }

    /// <summary>O CPF já mascarado da linha sem inscrito; nulo nos demais tipos.</summary>
    public string? CpfMascarado { get; private init; }

    /// <summary>O nome que o arquivo traz para a linha sem inscrito; nulo nos demais tipos e quando a célula veio vazia.</summary>
    public string? Nome { get; private init; }

    /// <summary>O sexo (<c>F</c> ou <c>M</c>) que o arquivo traz para a linha sem inscrito; nulo nos demais tipos e quando a célula veio vazia.</summary>
    public string? Sexo { get; private init; }

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

    public static OcorrenciaDoLoteDeNotas LinhaSemInscrito(Guid loteId, int numeroDaLinha, string cpfMascarado, string? nome, string? sexo)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(loteId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(numeroDaLinha, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(cpfMascarado);
        if (!EstaMascarado(cpfMascarado))
        {
            throw new ArgumentException("O CPF da linha deve chegar mascarado (***.999.999-**).", nameof(cpfMascarado));
        }

        return new OcorrenciaDoLoteDeNotas
        {
            LoteId = loteId,
            Tipo = TipoDeOcorrenciaDoLote.LinhaSemInscrito,
            NumerosDasLinhas = [numeroDaLinha],
            CpfMascarado = cpfMascarado,
            Nome = nome,
            Sexo = sexo,
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

    private static bool EstaMascarado(string cpf) =>
        cpf.Length == Cpf.TamanhoMascarado && cpf.StartsWith("***.", StringComparison.Ordinal) && cpf.EndsWith("-**", StringComparison.Ordinal);
}
