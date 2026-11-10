namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// O fato "o catálogo X mudou a permissão C para o perfil P": a classe sobe, desce ou fica, e o escopo muda ou fica, cada
/// eixo por conta própria. Serve à fila de revisão, aos avisos, à trilha e à retomada do perfil suspenso. A subida de
/// classe ou a mudança de escopo suspende a permissão nas concessões do perfil até a liberação das etapas exigidas; a
/// descida sozinha só se registra. Só se acrescenta: é evidência forense (ADR-0063). A decisão das etapas vive em
/// <see cref="Decidir"/>.
/// </summary>
public sealed partial class ReclassificacaoDaPermissao : IForensicEntity, IRegistroDaTrilhaDeContencao
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ImplantacaoId { get; private init; }

    public Guid PerfilId { get; private init; }

    public string Codigo { get; private init; } = string.Empty;

    public ClassificacaoDeProtecao ClassificacaoAnterior { get; private init; }

    public ClassificacaoDeProtecao ClassificacaoNova { get; private init; }

    public bool DeUnidadeAnterior { get; private init; }

    public bool DeUnidadeNovo { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNaTrilha { get; private set; }

    /// <summary>A classificação da permissão subiu.</summary>
    public bool Subiu => ClassificacaoNova > ClassificacaoAnterior;

    /// <summary>A classificação da permissão desceu.</summary>
    public bool Desceu => ClassificacaoNova < ClassificacaoAnterior;

    /// <summary>O escopo da permissão mudou entre institucional e de unidade.</summary>
    public bool MudouEscopo => DeUnidadeAnterior != DeUnidadeNovo;

    // Construtor de materialização do EF Core.
    private ReclassificacaoDaPermissao()
    {
    }

    public static ReclassificacaoDaPermissao Registrar(
        Guid implantacaoId, Guid perfilId, string codigo, MudancaDaPermissao mudanca, DateTimeOffset instante, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(mudanca);

        return new ReclassificacaoDaPermissao
        {
            ImplantacaoId = implantacaoId,
            PerfilId = perfilId,
            Codigo = codigo,
            ClassificacaoAnterior = mudanca.ClassificacaoAnterior,
            ClassificacaoNova = mudanca.ClassificacaoNova,
            DeUnidadeAnterior = mudanca.DeUnidadeAnterior,
            DeUnidadeNovo = mudanca.DeUnidadeNovo,
            Instante = instante,
            OrdinalLocal = ordinal,
        };
    }

    /// <summary>A base está aquém do que o catálogo desta reclassificação pede: a permissão fica suspensa na concessão.</summary>
    public bool SuspendeA(BaseDaConcessao baseDaConcessao)
    {
        ArgumentNullException.ThrowIfNull(baseDaConcessao);

        return baseDaConcessao.SuspendeFrenteA(ClassificacaoNova, DeUnidadeNovo);
    }

    /// <summary>
    /// As etapas que a liberação exige para a concessão, pela base dela: a classe maior exige a proteção de dados, e o
    /// escopo diferente, a gestão de acessos.
    /// </summary>
    public IReadOnlyList<EtapaDaVersao> EtapasExigidas(BaseDaConcessao baseDaConcessao)
    {
        ArgumentNullException.ThrowIfNull(baseDaConcessao);

        List<EtapaDaVersao> etapas = [];
        if (ClassificacaoNova > baseDaConcessao.Classificacao)
        {
            etapas.Add(EtapaDaVersao.ProtecaoDeDados);
        }

        if (DeUnidadeNovo != baseDaConcessao.DeUnidade)
        {
            etapas.Add(EtapaDaVersao.GestaoDeAcessos);
        }

        return etapas;
    }

    /// <summary>A concessão, pelo escopo em que foi dada, comporta o alcance que a permissão passou a ter.</summary>
    public bool EscopoComporta(TipoDeEscopo escopo) => DeUnidadeNovo ? escopo == TipoDeEscopo.Unidade : escopo == TipoDeEscopo.Instituicao;
}
