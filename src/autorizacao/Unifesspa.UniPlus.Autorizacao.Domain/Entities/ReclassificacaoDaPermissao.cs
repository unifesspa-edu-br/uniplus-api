namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O fato "o catálogo X mudou a permissão C para o perfil P": a classe sobe, desce ou fica, e o escopo muda ou fica, cada
/// eixo por conta própria. Serve à fila de revisão, aos avisos, à trilha e à retomada do perfil suspenso. A subida de
/// classe ou a mudança de escopo suspende a permissão nas concessões do perfil até a liberação das etapas exigidas; a
/// descida sozinha só se registra. Só se acrescenta: é evidência forense (ADR-0063). A decisão das etapas vive em
/// <see cref="Decidir"/>.
/// </summary>
public sealed class ReclassificacaoDaPermissao : IForensicEntity, IRegistroDaTrilhaDeContencao
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ImplantacaoId { get; private init; }

    public Guid PerfilId { get; private init; }

    public CodigoDePermissao Codigo { get; private init; } = null!;

    public ClassificacaoDeProtecao ClassificacaoAnterior { get; private init; }

    public ClassificacaoDeProtecao ClassificacaoNova { get; private init; }

    public bool DeUnidadeAnterior { get; private init; }

    public bool DeUnidadeNovo { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNoHistorico { get; private set; }

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
        Guid implantacaoId, Guid perfilId, CodigoDePermissao codigo, MudancaDaPermissao mudanca, DateTimeOffset instante, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(codigo);

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

    /// <summary>
    /// Decide uma etapa para as concessões alvo, de forma atômica: ou todas são decididas ou nenhuma. Quando as etapas
    /// exigidas de uma concessão estão todas liberadas, ela ganha a base nova. Na decisão em lote, a concessão do próprio
    /// decisor e a que o escopo não comporta ficam de fora e voltam na resposta; a que ele já decidiu em outra etapa recusa
    /// o lote inteiro.
    /// </summary>
    public Result<DesfechoDaDecisaoDaReclassificacao> Decidir(PedidoDeDecisaoDaReclassificacao pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        PartilhaDosAlvos partilha = Partilhar(pedido);
        DomainError? recusa = !pedido.Vigente ? ReclassificacaoErros.Superada() : RecusaDosAlvos(pedido, partilha.Decididas) ?? RecusaDoMotivo(pedido);

        return recusa is not null
            ? Result<DesfechoDaDecisaoDaReclassificacao>.Failure(recusa)
            : Result<DesfechoDaDecisaoDaReclassificacao>.Success(Registrar(pedido, partilha));
    }

    private PartilhaDosAlvos Partilhar(PedidoDeDecisaoDaReclassificacao pedido)
    {
        if (!pedido.Todas)
        {
            return new PartilhaDosAlvos(pedido.Alvos, [], []);
        }

        ILookup<bool, ConcessaoNaReclassificacao> paraOutra = pedido.Alvos.ToLookup(c => ParaOutraPessoa(pedido, c));
        ILookup<bool, ConcessaoNaReclassificacao> semEscopo = paraOutra[false].ToLookup(c => NaoComportaALiberacao(pedido, c));

        return new PartilhaDosAlvos([.. semEscopo[false]], [.. paraOutra[true].Select(static c => c.Id)], [.. semEscopo[true].Select(static c => c.Id)]);
    }

    private DomainError? RecusaDosAlvos(PedidoDeDecisaoDaReclassificacao pedido, IReadOnlyList<ConcessaoNaReclassificacao> alvos)
    {
        Func<ConcessaoNaReclassificacao, DomainError?>[] conferencias =
        [
            c => ForaDaReclassificacao(c),
            c => EtapaNaoExigida(pedido, c),
            c => EtapaJaDecidida(pedido, c),
            c => c.Beneficiario.Equals(pedido.Decisor) ? ReclassificacaoErros.DecisorEBeneficiario() : null,
            c => DecididaEmOutraEtapaPeloDecisor(pedido, c) ? ReclassificacaoErros.MesmoDecisorNasDuasEtapas() : null,
            c => c.DocumentoLido ? null : ReclassificacaoErros.DocumentoNaoLido(),
            c => NaoComportaALiberacao(pedido, c) ? ReclassificacaoErros.EscopoNaoComporta() : null,
        ];

        return conferencias
            .Select(conferir => alvos.Select(conferir).FirstOrDefault(static erro => erro is not null))
            .FirstOrDefault(static erro => erro is not null);
    }

    private DomainError? ForaDaReclassificacao(ConcessaoNaReclassificacao concessao) =>
        concessao.DaPermissao && concessao.Base is { } baseDaConcessao && SuspendeA(baseDaConcessao) ? null : ReclassificacaoErros.ConcessaoForaDaReclassificacao();

    private DomainError? EtapaNaoExigida(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao) =>
        EtapasExigidas(concessao.Base!).Contains(pedido.Etapa) ? null : ReclassificacaoErros.EtapaNaoExigida();

    private static DomainError? EtapaJaDecidida(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao) =>
        concessao.Decisoes.Any(d => d.Etapa == pedido.Etapa) ? ReclassificacaoErros.EtapaJaDecidida() : null;

    private static DomainError? RecusaDoMotivo(PedidoDeDecisaoDaReclassificacao pedido) =>
        pedido.Resultado == ResultadoDaLiberacao.Mantida && MotivoInformado.Normalizar(pedido.Motivo) is null
            ? ReclassificacaoErros.MotivoInvalido(MotivoInformado.MaximoDeCaracteres)
            : null;

    private static bool DecididaEmOutraEtapaPeloDecisor(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao) =>
        concessao.Decisoes.Any(d => d.Etapa != pedido.Etapa && d.Decisor.Equals(pedido.Decisor));

    private static bool ParaOutraPessoa(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao) =>
        concessao.Beneficiario.Equals(pedido.Decisor);

    // Só a gestão de acessos decide o escopo, e liberar o que o escopo da concessão não comporta é o que se recusa; manter
    // a suspensão continua permitido.
    private bool NaoComportaALiberacao(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao) =>
        pedido is { Etapa: EtapaDaVersao.GestaoDeAcessos, Resultado: ResultadoDaLiberacao.Liberada } && !EscopoComporta(concessao.EscopoTipo);

    private DesfechoDaDecisaoDaReclassificacao Registrar(PedidoDeDecisaoDaReclassificacao pedido, PartilhaDosAlvos partilha)
    {
        int ordinal = 0;
        List<LiberacaoDaReclassificacao> liberacoes =
            [.. partilha.Decididas.Select(c => LiberacaoDaReclassificacao.Registrar(this, c.Id, pedido, ++ordinal))];
        List<BaseDaPermissaoNaConcessao> bases =
            [.. partilha.Decididas.Where(c => TodasLiberadas(pedido, c)).Select(c => BaseDaLiberacao(c, ++ordinal))];

        return new DesfechoDaDecisaoDaReclassificacao(liberacoes, bases, partilha.ParaOutraPessoa, partilha.PuladasPorEscopo);
    }

    // As etapas exigidas da concessão estão todas liberadas, contando as decisões anteriores e a desta requisição.
    private bool TodasLiberadas(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao)
    {
        List<DecisaoNaConcessao> decisoes = [.. concessao.Decisoes, new(pedido.Etapa, pedido.Decisor, pedido.Resultado)];

        return EtapasExigidas(concessao.Base!)
            .All(etapa => decisoes.Exists(d => d.Etapa == etapa && d.Resultado == ResultadoDaLiberacao.Liberada));
    }

    private BaseDaPermissaoNaConcessao BaseDaLiberacao(ConcessaoNaReclassificacao concessao, int ordinal) =>
        BaseDaPermissaoNaConcessao.Registrar(
            PerfilId, concessao.Id, Codigo, new BaseDaConcessao(ClassificacaoNova, DeUnidadeNovo), OrigemDaBase.Liberacao, Id, ordinal);

    private sealed record PartilhaDosAlvos(
        IReadOnlyList<ConcessaoNaReclassificacao> Decididas, IReadOnlyList<Guid> ParaOutraPessoa, IReadOnlyList<Guid> PuladasPorEscopo);
}
