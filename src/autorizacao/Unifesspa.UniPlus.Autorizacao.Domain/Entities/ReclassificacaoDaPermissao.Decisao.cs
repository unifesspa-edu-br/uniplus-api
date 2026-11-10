namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A decisão das etapas da liberação, uma conferência por método, na ordem em que as recusas orientam.</summary>
public sealed partial class ReclassificacaoDaPermissao
{
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
        DomainError? recusa = !pedido.MaisRecente ? ReclassificacaoErros.Superada() : RecusaDosAlvos(pedido, partilha.Decididas) ?? RecusaDoMotivo(pedido);

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
        concessao.DaPermissao && SuspendeA(concessao.Base) ? null : ReclassificacaoErros.ConcessaoForaDaReclassificacao();

    private DomainError? EtapaNaoExigida(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao) =>
        EtapasExigidas(concessao.Base).Contains(pedido.Etapa) ? null : ReclassificacaoErros.EtapaNaoExigida();

    private static DomainError? EtapaJaDecidida(PedidoDeDecisaoDaReclassificacao pedido, ConcessaoNaReclassificacao concessao) =>
        concessao.Decisoes.Any(d => d.Etapa == pedido.Etapa) ? ReclassificacaoErros.EtapaJaDecidida() : null;

    private static DomainError? RecusaDoMotivo(PedidoDeDecisaoDaReclassificacao pedido) =>
        pedido.Resultado == ResultadoDaLiberacao.Mantida && MotivosDaVersao.Normalizar(pedido.Motivo) is null
            ? ReclassificacaoErros.MotivoInvalido(MotivosDaVersao.MaxLength)
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

        return EtapasExigidas(concessao.Base)
            .All(etapa => decisoes.Exists(d => d.Etapa == etapa && d.Resultado == ResultadoDaLiberacao.Liberada));
    }

    private BaseDaPermissaoNaConcessao BaseDaLiberacao(ConcessaoNaReclassificacao concessao, int ordinal) =>
        BaseDaPermissaoNaConcessao.Registrar(
            PerfilId, concessao.Id, Codigo, new BaseDaConcessao(ClassificacaoNova, DeUnidadeNovo), OrigemDaBase.Liberacao, Id, ordinal);

    private sealed record PartilhaDosAlvos(
        IReadOnlyList<ConcessaoNaReclassificacao> Decididas, IReadOnlyList<Guid> ParaOutraPessoa, IReadOnlyList<Guid> PuladasPorEscopo);
}
