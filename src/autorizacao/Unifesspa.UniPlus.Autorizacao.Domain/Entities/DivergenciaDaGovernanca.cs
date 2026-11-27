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

    /// <summary>A classificação antes e depois; só na divergência de classificação.</summary>
    public MudancaDaClassificacao? MudancaDaClassificacao { get; private init; }

    /// <summary>O escopo por unidade antes e depois; só na divergência de escopo.</summary>
    public MudancaDoEscopo? MudancaDoEscopo { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNoHistorico { get; private set; }

    // Construtor de materialização do EF Core.
    private DivergenciaDaGovernanca()
    {
    }

    public static DivergenciaDaGovernanca DaComposicao(
        Guid implantacaoId, Guid perfilId, string codigo, TipoDeDivergencia tipo, DateTimeOffset instante, int ordinal)
    {
        if (tipo is not (TipoDeDivergencia.ComposicaoFaltante or TipoDeDivergencia.ComposicaoExcedente))
        {
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "A divergência de composição é a faltante ou a excedente.");
        }

        return Nova(implantacaoId, perfilId, codigo, tipo, instante, ordinal);
    }

    public static DivergenciaDaGovernanca DaClassificacao(
        Guid implantacaoId, Guid perfilId, string codigo, MudancaDaClassificacao mudanca, DateTimeOffset instante, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(mudanca);

        return new()
        {
            ImplantacaoId = implantacaoId,
            PerfilId = perfilId,
            Codigo = codigo,
            Tipo = TipoDeDivergencia.Classificacao,
            MudancaDaClassificacao = mudanca,
            Instante = instante,
            OrdinalLocal = ordinal,
        };
    }

    public static DivergenciaDaGovernanca DoEscopo(
        Guid implantacaoId, Guid perfilId, string codigo, MudancaDoEscopo mudanca, DateTimeOffset instante, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(mudanca);

        return new()
        {
            ImplantacaoId = implantacaoId,
            PerfilId = perfilId,
            Codigo = codigo,
            Tipo = TipoDeDivergencia.Escopo,
            MudancaDoEscopo = mudanca,
            Instante = instante,
            OrdinalLocal = ordinal,
        };
    }

    private static DivergenciaDaGovernanca Nova(
        Guid implantacaoId, Guid perfilId, string codigo, TipoDeDivergencia tipo, DateTimeOffset instante, int ordinal) =>
        new() { ImplantacaoId = implantacaoId, PerfilId = perfilId, Codigo = codigo, Tipo = tipo, Instante = instante, OrdinalLocal = ordinal };
}
