namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Uma diferença entre o perfil de governança e o que o código espera dele, achada na troca de catálogo: a composição
/// gravada que não bate com a esperada, ou a classificação e o escopo de uma permissão que mudaram. Os perfis de
/// governança nunca se suspendem; a divergência vira evento, aviso e tela. Só se acrescenta (ADR-0063).
/// </summary>
public sealed class DivergenciaDaGovernanca : IForensicEntity, IRegistroDaTrilhaDeContencao
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ImplantacaoId { get; private init; }

    public Guid PerfilId { get; private init; }

    public string Codigo { get; private init; } = string.Empty;

    public TipoDeDivergencia Tipo { get; private init; }

    /// <summary>O valor anterior, em texto estável; nulo na composição.</summary>
    public string? De { get; private set; }

    /// <summary>O valor novo, em texto estável; nulo na composição.</summary>
    public string? Para { get; private set; }

    public DateTimeOffset Instante { get; private init; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNaTrilha { get; private set; }

    // Construtor de materialização do EF Core.
    private DivergenciaDaGovernanca()
    {
    }

    public static DivergenciaDaGovernanca DaComposicao(
        Guid implantacaoId, Guid perfilId, string codigo, TipoDeDivergencia tipo, DateTimeOffset instante, int ordinal) =>
        new() { ImplantacaoId = implantacaoId, PerfilId = perfilId, Codigo = codigo, Tipo = tipo, Instante = instante, OrdinalLocal = ordinal };

    public static DivergenciaDaGovernanca DaMudanca(
        Guid implantacaoId, Guid perfilId, string codigo, TipoDeDivergencia tipo, ValorDivergente valor, DateTimeOffset instante, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(valor);

        DivergenciaDaGovernanca divergencia = DaComposicao(implantacaoId, perfilId, codigo, tipo, instante, ordinal);
        divergencia.De = valor.De;
        divergencia.Para = valor.Para;

        return divergencia;
    }
}
