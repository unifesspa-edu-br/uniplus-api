namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão da liberação segue a ordem das recusas que orienta, é atômica e só grava a base nova quando todas as etapas
/// exigidas da concessão estão liberadas. A classe que sobe exige a proteção de dados; o escopo que muda, a gestão de acessos.
/// </summary>
public sealed class ReclassificacaoDaPermissaoTests
{
    private const string Codigo = "selecao:processos:consultar";

    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;
    private static readonly Identidade Paulo = new("https://idp.test/realms/unifesspa", "paulo.ramos", "Paulo Ramos");
    private static readonly Identidade Isabella = new("https://idp.test/realms/unifesspa", "isabella.figueiredo", "Isabella Figueiredo");
    private static readonly BaseDaConcessao BaseInterna = new(ClassificacaoDeProtecao.Interno, false);

    [Fact(DisplayName = "Subir a classe exige só a proteção de dados e mudar o escopo, só a gestão de acessos")]
    public void EtapasExigidas_CadaEixoExigeUmaEtapa()
    {
        Subida(deUnidade: false).EtapasExigidas(BaseInterna).Should().Equal(EtapaDaVersao.ProtecaoDeDados);
        Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Interno, false, true).EtapasExigidas(BaseInterna)
            .Should().Equal(EtapaDaVersao.GestaoDeAcessos);
        Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal, false, true).EtapasExigidas(BaseInterna)
            .Should().Equal(EtapaDaVersao.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos);
    }

    [Fact(DisplayName = "A descida de classe sozinha não suspende nem exige etapa")]
    public void Descida_NaoSuspendeNemExigeEtapa()
    {
        ReclassificacaoDaPermissao descida = Mudanca(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Interno, false, false);

        descida.Desceu.Should().BeTrue();
        descida.SuspendeA(new BaseDaConcessao(ClassificacaoDeProtecao.Pessoal, false)).Should().BeFalse();
        descida.EtapasExigidas(new BaseDaConcessao(ClassificacaoDeProtecao.Pessoal, false)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A descida com mudança de escopo registra a descida e suspende só pelo escopo")]
    public void DescidaComMudancaDeEscopo_SuspendeSoPeloEscopo()
    {
        ReclassificacaoDaPermissao descida = Mudanca(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Interno, false, true);
        BaseDaConcessao baseAntiga = new(ClassificacaoDeProtecao.Pessoal, false);

        descida.Desceu.Should().BeTrue();
        descida.MudouEscopo.Should().BeTrue();
        descida.SuspendeA(baseAntiga).Should().BeTrue();
        descida.EtapasExigidas(baseAntiga).Should().Equal(EtapaDaVersao.GestaoDeAcessos);
    }

    [Fact(DisplayName = "A proteção de dados libera a concessão de Paulo e mantém a de Isabella")]
    public void Decidir_LiberaUmaEMantemOutra()
    {
        ReclassificacaoDaPermissao reclassificacao = Subida(deUnidade: false);
        ConcessaoNaReclassificacao dePaulo = Concessao(Paulo);
        ConcessaoNaReclassificacao deIsabella = Concessao(Isabella);

        Result<DesfechoDaDecisaoDaReclassificacao> liberada =
            reclassificacao.Decidir(Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [dePaulo]));
        Result<DesfechoDaDecisaoDaReclassificacao> mantida =
            reclassificacao.Decidir(Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Mantida, [deIsabella], motivo: "Sem justificativa."));

        liberada.Value!.Bases.Should().ContainSingle().Which.Should().Match<BaseDaPermissaoNaConcessao>(b =>
            b.ConcessaoId == dePaulo.Id && b.Origem == OrigemDaBase.Liberacao && b.Classificacao == ClassificacaoDeProtecao.Pessoal && b.ReferenciaId == reclassificacao.Id);
        mantida.Value!.Bases.Should().BeEmpty();
        mantida.Value.Liberacoes.Single().Motivo.Should().Be("Sem justificativa.");
    }

    [Fact(DisplayName = "Com classe e escopo mudando juntos, uma etapa só não grava a base nova")]
    public void Decidir_UmaEtapaDeDuas_NaoGravaABase()
    {
        ReclassificacaoDaPermissao reclassificacao = Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal, false, true);
        ConcessaoNaReclassificacao concessao = Concessao(Paulo, escopo: TipoDeEscopo.Unidade);

        DesfechoDaDecisaoDaReclassificacao desfecho =
            reclassificacao.Decidir(Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [concessao])).Value!;

        desfecho.Liberacoes.Should().ContainSingle();
        desfecho.Bases.Should().BeEmpty();
    }

    [Fact(DisplayName = "As duas etapas liberadas por pessoas diferentes gravam a base, em qualquer ordem")]
    public void Decidir_DuasEtapasPorPessoasDiferentes_GravamABase()
    {
        ReclassificacaoDaPermissao reclassificacao = Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal, false, true);
        ConcessaoNaReclassificacao ja = Concessao(Paulo, escopo: TipoDeEscopo.Unidade, decisoes: [new(EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.Gestora, ResultadoDaLiberacao.Liberada)]);

        DesfechoDaDecisaoDaReclassificacao desfecho = reclassificacao
            .Decidir(Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [ja], decisor: PerfisDeTeste.ProtecaoDeDados)).Value!;

        desfecho.Bases.Should().ContainSingle().Which.DeUnidade.Should().BeTrue();
    }

    [Fact(DisplayName = "A reclassificação superada recusa a decisão")]
    public void Decidir_Superada_Recusa() =>
        Recusa(Subida(false), Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [Concessao(Paulo)], modo: new ModoDoPedido(Vigente: false)))
            .Should().Be(ReclassificacaoErrorCodes.Superada);

    [Fact(DisplayName = "A concessão que não reúne a permissão ou não está suspensa por ela fica fora da reclassificação")]
    public void Decidir_ConcessaoForaDaReclassificacao_Recusa()
    {
        ReclassificacaoDaPermissao reclassificacao = Subida(false);

        Recusa(reclassificacao, Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [Concessao(Paulo, daPermissao: false)]))
            .Should().Be(ReclassificacaoErrorCodes.ConcessaoForaDaReclassificacao);
        Recusa(reclassificacao, Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [Concessao(Paulo, ClassificacaoDeProtecao.Pessoal)]))
            .Should().Be(ReclassificacaoErrorCodes.ConcessaoForaDaReclassificacao);
    }

    [Fact(DisplayName = "A etapa que a base não exige e a etapa já decidida são recusadas")]
    public void Decidir_EtapaNaoExigidaOuJaDecidida_Recusa()
    {
        ReclassificacaoDaPermissao reclassificacao = Subida(false);

        Recusa(reclassificacao, Pedido(EtapaDaVersao.GestaoDeAcessos, ResultadoDaLiberacao.Liberada, [Concessao(Paulo)]))
            .Should().Be(ReclassificacaoErrorCodes.EtapaNaoExigida);
        Recusa(reclassificacao, Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada,
                [Concessao(Paulo, decisoes: [new(EtapaDaVersao.ProtecaoDeDados, Isabella, ResultadoDaLiberacao.Mantida)])]))
            .Should().Be(ReclassificacaoErrorCodes.EtapaJaDecidida);
    }

    [Fact(DisplayName = "Ninguém decide a própria concessão nem as duas etapas da mesma concessão")]
    public void Decidir_Segregacao_Recusa()
    {
        ReclassificacaoDaPermissao reclassificacao = Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal, false, true);

        Recusa(reclassificacao, Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [Concessao(Paulo, escopo: TipoDeEscopo.Unidade)], decisor: Paulo))
            .Should().Be(ReclassificacaoErrorCodes.DecisorEBeneficiario);
        Recusa(reclassificacao, Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada,
                [Concessao(Paulo, escopo: TipoDeEscopo.Unidade, decisoes: [new(EtapaDaVersao.GestaoDeAcessos, Isabella, ResultadoDaLiberacao.Liberada)])], decisor: Isabella))
            .Should().Be(ReclassificacaoErrorCodes.MesmoDecisorNasDuasEtapas);
    }

    [Fact(DisplayName = "Decidir sem ter lido o documento de cada versão é recusado")]
    public void Decidir_DocumentoNaoLido_Recusa() =>
        Recusa(Subida(false), Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [Concessao(Paulo, documentoLido: false)]))
            .Should().Be(ReclassificacaoErrorCodes.DocumentoNaoLido);

    [Fact(DisplayName = "A gestão de acessos não libera a concessão cujo escopo não comporta o novo alcance, mas pode mantê-la suspensa")]
    public void Decidir_EscopoNaoComporta_NaoLibera()
    {
        ReclassificacaoDaPermissao paraUnidade = Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Interno, false, true);
        ConcessaoNaReclassificacao institucional = Concessao(Paulo, escopo: TipoDeEscopo.Instituicao);

        Recusa(paraUnidade, Pedido(EtapaDaVersao.GestaoDeAcessos, ResultadoDaLiberacao.Liberada, [institucional]))
            .Should().Be(ReclassificacaoErrorCodes.EscopoNaoComporta);
        paraUnidade.Decidir(Pedido(EtapaDaVersao.GestaoDeAcessos, ResultadoDaLiberacao.Mantida, [institucional], motivo: "Pede outro perfil."))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O motivo da suspensão mantida é gravado sem os espaços das pontas")]
    public void Decidir_MantidaComEspacos_GravaOMotivoAparado()
    {
        Result<DesfechoDaDecisaoDaReclassificacao> mantida = Subida(deUnidade: false)
            .Decidir(Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Mantida, [Concessao(Paulo)], motivo: $" {new string('a', 500)} "));

        mantida.Value!.Liberacoes.Single().Motivo.Should().Be(new string('a', 500));
    }

    [Fact(DisplayName = "Manter a suspensão exige motivo")]
    public void Decidir_MantidaSemMotivo_Recusa() =>
        Recusa(Subida(false), Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Mantida, [Concessao(Paulo)], motivo: "  "))
            .Should().Be(ReclassificacaoErrorCodes.MotivoInvalido);

    [Fact(DisplayName = "Na decisão em lote, a concessão do decisor e a que o escopo não comporta ficam de fora")]
    public void Decidir_Todas_SeparaAsQueFicamParaOutraPessoa()
    {
        ReclassificacaoDaPermissao reclassificacao = Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Interno, false, true);
        ConcessaoNaReclassificacao propria = Concessao(PerfisDeTeste.Gestora, escopo: TipoDeEscopo.Unidade);
        ConcessaoNaReclassificacao semEscopo = Concessao(Isabella, escopo: TipoDeEscopo.Instituicao);
        ConcessaoNaReclassificacao decidivel = Concessao(Paulo, escopo: TipoDeEscopo.Unidade);

        DesfechoDaDecisaoDaReclassificacao desfecho = reclassificacao
            .Decidir(Pedido(EtapaDaVersao.GestaoDeAcessos, ResultadoDaLiberacao.Liberada, [propria, semEscopo, decidivel], decisor: PerfisDeTeste.Gestora, modo: new ModoDoPedido(Todas: true))).Value!;

        desfecho.Liberacoes.Select(l => l.ConcessaoId).Should().Equal(decidivel.Id);
        desfecho.ParaOutraPessoa.Should().Equal(propria.Id);
        desfecho.PuladasPorEscopo.Should().Equal(semEscopo.Id);
    }

    [Fact(DisplayName = "Na decisão em lote, a concessão cuja outra etapa o decisor decidiu recusa o lote inteiro")]
    public void Decidir_Todas_ConcessaoDecididaEmOutraEtapaPeloDecisor_RecusaOLote()
    {
        ReclassificacaoDaPermissao reclassificacao = Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal, false, true);
        ConcessaoNaReclassificacao decididaAntes = Concessao(
            Paulo, escopo: TipoDeEscopo.Unidade, decisoes: [new DecisaoNaConcessao(EtapaDaVersao.ProtecaoDeDados, PerfisDeTeste.Gestora, ResultadoDaLiberacao.Liberada)]);
        ConcessaoNaReclassificacao livre = Concessao(Isabella, escopo: TipoDeEscopo.Unidade);

        Result<DesfechoDaDecisaoDaReclassificacao> resultado = reclassificacao.Decidir(Pedido(
            EtapaDaVersao.GestaoDeAcessos, ResultadoDaLiberacao.Liberada, [decididaAntes, livre], decisor: PerfisDeTeste.Gestora, modo: new ModoDoPedido(Todas: true)));

        resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.MesmoDecisorNasDuasEtapas);
    }

    [Fact(DisplayName = "Toda liberação do lote leva o mesmo lote e registra se o decisor tem concessão do perfil")]
    public void Decidir_Lote_LevaOMesmoLoteEAConcessaoDoDecisor()
    {
        ReclassificacaoDaPermissao reclassificacao = Subida(false);

        DesfechoDaDecisaoDaReclassificacao desfecho = reclassificacao
            .Decidir(Pedido(EtapaDaVersao.ProtecaoDeDados, ResultadoDaLiberacao.Liberada, [Concessao(Paulo), Concessao(Isabella)], modo: new ModoDoPedido(TemConcessao: true))).Value!;

        desfecho.Liberacoes.Select(l => l.LoteId).Distinct().Should().ContainSingle();
        desfecho.Liberacoes.Should().OnlyContain(l => l.DecisorTemConcessao && l.Instante == Agora);
    }

    private static string Recusa(ReclassificacaoDaPermissao reclassificacao, PedidoDeDecisaoDaReclassificacao pedido) =>
        reclassificacao.Decidir(pedido).Error!.Code;

    private static ReclassificacaoDaPermissao Subida(bool deUnidade) =>
        Mudanca(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal, deUnidade, deUnidade);

    private static ReclassificacaoDaPermissao Mudanca(ClassificacaoDeProtecao de, ClassificacaoDeProtecao para, bool unidadeAntes, bool unidadeDepois) =>
        ReclassificacaoDaPermissao.Registrar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Codigo, new MudancaDaPermissao(de, para, unidadeAntes, unidadeDepois), Agora, 1);

    private static ConcessaoNaReclassificacao Concessao(
        Identidade beneficiario,
        ClassificacaoDeProtecao classeDaBase = ClassificacaoDeProtecao.Interno,
        TipoDeEscopo escopo = TipoDeEscopo.Instituicao,
        bool daPermissao = true,
        bool documentoLido = true,
        IReadOnlyList<DecisaoNaConcessao>? decisoes = null) =>
        new(Guid.CreateVersion7(), beneficiario, daPermissao, new BaseDaConcessao(classeDaBase, false), escopo, decisoes ?? [], documentoLido);

    private static PedidoDeDecisaoDaReclassificacao Pedido(
        EtapaDaVersao etapa,
        ResultadoDaLiberacao resultado,
        IReadOnlyList<ConcessaoNaReclassificacao> alvos,
        string? motivo = null,
        Identidade? decisor = null,
        ModoDoPedido? modo = null)
    {
        ModoDoPedido o = modo ?? new ModoDoPedido();

        return new(etapa, resultado, motivo, decisor ?? PerfisDeTeste.ProtecaoDeDados, o.TemConcessao, o.Todas, Guid.CreateVersion7(), Agora, o.Vigente, alvos);
    }

    private sealed record ModoDoPedido(bool Todas = false, bool Vigente = true, bool TemConcessao = false);
}
