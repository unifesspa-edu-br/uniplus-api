namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.ApoioDaSegundaInstancia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;

/// <summary>
/// A banca de 2ª instância só é prevista onde um recurso a faria julgar: na etapa, um recurso da própria etapa que admite
/// instância superior; na fase, o da fase, ou o de uma etapa dela que admite e não tem banca de 2ª instância própria. A banca que
/// sobra é recusada, uma recusa por banca; a parte que a leitura recusou deixa a pergunta sem resposta, e o item fica pendente.
/// </summary>
public sealed class SobraDaBancaDaSegundaInstanciaTests
{
    private const string CodigoDaSobraNaFase = "Banca.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar";
    private const string CodigoDaSobraNaEtapa = "Banca.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar";

    private const string DetalheDaSobraNaFaseSemNenhumRecurso =
        "A fase 'Fase de avaliação' prevê a banca 'Banca de análise de recursos de 2ª instância 1', com o papel 'Julga o recurso em 2ª instância', e nenhum recurso da fase ou das etapas dela admite instância superior. A banca de 2ª instância da fase julga o recurso da fase e os das etapas dela que não têm banca de 2ª instância própria. Retire essa banca, ou declare que o recurso da fase, ou o de uma etapa dela sem banca de 2ª instância própria, admite instância superior.";

    private const string DetalheDaSobraNaFaseComEtapaQueTemBancaPropria =
        "A fase 'Fase de avaliação' prevê a banca 'Banca de análise de recursos de 2ª instância 1', com o papel 'Julga o recurso em 2ª instância', e não há recurso para ela julgar: o recurso da fase não admite instância superior, e cada etapa da fase com recurso que admite instância superior já tem a sua banca de 2ª instância, que o julga. A banca de 2ª instância da fase julga o recurso da fase e os das etapas dela que não têm banca de 2ª instância própria. Retire essa banca; ou retire a banca de 2ª instância de uma dessas etapas, para que a da fase julgue o recurso dela; ou declare que o recurso da fase, ou o de uma etapa dela sem banca de 2ª instância própria, admite instância superior.";

    private const string DetalheDaSobraNaEtapa =
        "A etapa na posição 1 prevê a banca 'Banca de análise de recursos de 2ª instância 1', com o papel 'Julga o recurso em 2ª instância', e nenhum recurso da etapa admite instância superior. A banca de 2ª instância da etapa julga só os recursos dela; a do recurso da fase fica na própria fase. Retire essa banca, ou declare que um recurso da etapa admite instância superior.";

    private static void DeveEstarAtendido(Cenario cenario)
    {
        cenario.Causas.Should().BeEmpty();
        cenario.Sobras.Indeterminado.Should().BeFalse();
        cenario.ItemEstaVerde(ItemDaSobra).Should().BeTrue();
    }

    private static void DeveFicarPendenteSemCausa(Cenario cenario)
    {
        cenario.Sobras.Causas.Should().BeEmpty();
        cenario.Sobras.Indeterminado.Should().BeTrue();
        cenario.Causas.Should().BeEmpty();
        cenario.ItemEstaVerde(ItemDaSobra).Should().BeFalse();
    }

    private static FieldError DeveRecusarABancaDaFase(Cenario cenario, string detalhe)
    {
        FieldError recusa = cenario.Causas.Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].bancasRequeridas[0]");
        recusa.Error.Code.Should().Be(CodigoDaSobraNaFase);
        recusa.Error.Message.Should().Be(detalhe);
        recusa.Identificadores.Should().Equal(
            new IdentificadorDoItem("fases", Fase),
            new IdentificadorDoItem("bancasRequeridas", cenario.IdentificadorDaBancaDaFase(0, 0)));
        cenario.ItemEstaVerde(ItemDaSobra).Should().BeFalse();

        return recusa;
    }

    private static void DeveRecusarABancaDaEtapa(Cenario cenario)
    {
        FieldError recusa = cenario.Causas.Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas[0]");
        recusa.Error.Code.Should().Be(CodigoDaSobraNaEtapa);
        recusa.Error.Message.Should().Be(DetalheDaSobraNaEtapa);
        recusa.Identificadores.Should().Equal(
            new IdentificadorDoItem("etapas", cenario.IdentificadorDaEtapa(0)),
            new IdentificadorDoItem("bancas", cenario.IdentificadorDaBancaDaEtapa(0, 0)));
        cenario.ItemEstaVerde(ItemDaSobra).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase sem recurso e com banca de 2ª instância é recusada na posição da banca, com o detalhe de nenhum recurso")]
    public void Fase_SemRecursoComBanca_Recusa() =>
        DeveRecusarABancaDaFase(Montar([FaseSemRecurso(DaSegundaInstanciaNaFase())], []), DetalheDaSobraNaFaseSemNenhumRecurso);

    [Fact(DisplayName = "A fase cujo recurso não admite instância superior, sem etapa que admita, é recusada pela banca de 2ª instância")]
    public void Fase_ComRecursoQueNaoAdmiteENenhumaEtapaAdmite_Recusa() =>
        DeveRecusarABancaDaFase(
            Montar([FaseQueNaoAdmite(DaSegundaInstanciaNaFase())], [EtapaQueNaoAdmite()]), DetalheDaSobraNaFaseSemNenhumRecurso);

    [Fact(DisplayName = "Duas bancas de 2ª instância na fase sem instância superior são duas recusas, na ordem da posição")]
    public void Fase_ComDuasBancasSemRecurso_DuasRecusasNaOrdemDaPosicao()
    {
        Cenario cenario = Montar([FaseSemRecurso(DaSegundaInstanciaNaFase(), DaSegundaInstanciaNaFase())], []);

        cenario.Causas.Select(static causa => causa.Field).Should().Equal("fases[0].bancasRequeridas[0]", "fases[0].bancasRequeridas[1]");
        cenario.Causas.Select(static causa => causa.Error.Message).Should().SatisfyRespectively(
            static primeira => primeira.Should().Contain("prevê a banca 'Banca de análise de recursos de 2ª instância 1'"),
            static segunda => segunda.Should().Contain("prevê a banca 'Banca de análise de recursos de 2ª instância 2'"));
        cenario.Causas.Select(static causa => causa.Identificadores![1].Identificador).Should().Equal(
            cenario.IdentificadorDaBancaDaFase(0, 0), cenario.IdentificadorDaBancaDaFase(0, 1));
    }

    [Fact(DisplayName = "A fase cujo recurso não admite, com etapa que admite e não tem banca própria, mantém a banca de 2ª instância")]
    public void Fase_ComEtapaQueAdmiteSemBancaPropria_Aceita() =>
        DeveEstarAtendido(Montar([FaseQueNaoAdmite(DaSegundaInstanciaNaFase())], [EtapaQueAdmite()]));

    [Fact(DisplayName = "A fase cujo recurso admite instância superior mantém a banca de 2ª instância, haja ou não etapa que admita")]
    public void Fase_ComRecursoQueAdmite_Aceita() =>
        DeveEstarAtendido(Montar([FaseQueAdmite(DaSegundaInstanciaNaFase())], [EtapaQueNaoAdmite()]));

    [Fact(DisplayName = "A fase cuja única etapa que admite já tem banca própria perde a da fase: ela não tem recurso para julgar")]
    public void Fase_ComAUnicaEtapaQueAdmiteComBancaPropria_RecusaComODetalheDaEtapaQueTemBanca() =>
        DeveRecusarABancaDaFase(
            Montar([FaseSemRecurso(DaSegundaInstanciaNaFase())], [EtapaQueAdmite(DaSegundaInstanciaNaEtapa())]),
            DetalheDaSobraNaFaseComEtapaQueTemBancaPropria);

    [Fact(DisplayName = "A etapa sem recurso que admita instância superior, com banca de 2ª instância, é recusada na posição da banca")]
    public void Etapa_SemRecursoComBanca_Recusa() =>
        DeveRecusarABancaDaEtapa(Montar([FaseSemRecurso()], [EtapaSemRecurso(DaSegundaInstanciaNaEtapa())]));

    [Fact(DisplayName = "A etapa cujo recurso não admite instância superior perde a banca de 2ª instância, mesmo que a fase admita e tenha a sua")]
    public void Etapa_QueNaoAdmiteEFaseQueAdmiteComBanca_SoABancaDaEtapaERecusada()
    {
        Cenario cenario = Montar(
            [FaseQueAdmite(DaSegundaInstanciaNaFase())], [EtapaQueNaoAdmite(DaSegundaInstanciaNaEtapa())]);

        DeveRecusarABancaDaEtapa(cenario);
        cenario.Causas.Should().NotContain(static causa => causa.Error.Code == CodigoDaSobraNaFase);
    }

    [Fact(DisplayName = "A etapa com dois recursos, só o segundo com instância superior, mantém a banca de 2ª instância")]
    public void Etapa_ComUmRecursoSoQueAdmite_Aceita() =>
        DeveEstarAtendido(Montar([FaseSemRecurso()], [EtapaComUmRecursoSoQueAdmite(DaSegundaInstanciaNaEtapa())]));

    [Fact(DisplayName = "O recurso já recusado pela instância superior não admitida não produz a recusa da banca que sobra")]
    public void RecursoRecusadoPelaInstanciaSuperiorNaoAdmitida_SoARecusaDoTipo()
    {
        Cenario cenario = Montar(
            [FaseSemRecurso()], [EtapaQueAdmiteSobreItemQueSeEncerra(DaSegundaInstanciaNaEtapa())]);

        DeveEstarAtendido(cenario);
        QuemJulgaORecurso.InstanciaSuperiorNaoAdmitida(cenario.Cronograma, cenario.Etapas).Should().ContainSingle()
            .Which.Error.Code.Should().Be("Banca.TipoNaoAdmiteInstanciaSuperior");
    }

    [Fact(DisplayName = "A regra de recurso da fase recusada na leitura deixa a banca da fase sem recusa, e o item pendente")]
    public void Fase_ComARegraDeRecursoRecusada_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(
            Montar([FaseSemRecurso(DaSegundaInstanciaNaFase())], [], $"fases[0].{FaseDeclarada.CampoDaRegraDeRecurso}"));

    [Fact(DisplayName = "Os recursos recusados de uma etapa da fase sem banca própria deixam a banca da fase sem recusa, e o item pendente")]
    public void Fase_ComRecursosDeEtapaRecusados_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(
            Montar([FaseQueNaoAdmite(DaSegundaInstanciaNaFase())], [EtapaSemRecurso()], "etapas[0].recursos"));

    [Fact(DisplayName = "As bancas recusadas de uma etapa da fase sem banca própria de 2ª instância deixam a banca da fase sem recusa, e o item pendente")]
    public void Fase_ComBancasRecusadasEmEtapaDaFase_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(
            Montar([FaseQueNaoAdmite(DaSegundaInstanciaNaFase())], [EtapaQueAdmite()], "etapas[0].bancas"));

    [Fact(DisplayName = "A etapa recusada inteira na leitura deixa a banca da fase sem recusa, e o item pendente")]
    public void Fase_ComEtapaRecusadaInteira_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(Montar([FaseQueNaoAdmite(DaSegundaInstanciaNaFase())], [], "etapas"));

    [Fact(DisplayName = "Os recursos recusados da etapa deixam a banca da etapa sem recusa, e o item pendente")]
    public void Etapa_ComRecursosRecusados_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(
            Montar([FaseSemRecurso()], [EtapaSemRecurso(DaSegundaInstanciaNaEtapa())], "etapas[0].recursos"));

    [Fact(DisplayName = "A lista de bancas recusada na fase sem instância superior não esconde a falta de recurso, mas deixa o item pendente")]
    public void Fase_ComBancasRecusadasESemInstanciaSuperior_PendenteComOuSemBancaAceita()
    {
        DeveFicarPendenteSemCausa(Montar([FaseSemRecurso()], [], "fases[0].bancasRequeridas"));

        Cenario comBancaAceita = Montar([FaseSemRecurso(DaSegundaInstanciaNaFase())], [], "fases[0].bancasRequeridas");

        comBancaAceita.Causas.Should().ContainSingle().Which.Error.Code.Should().Be(CodigoDaSobraNaFase);
        comBancaAceita.Sobras.Indeterminado.Should().BeTrue();
        comBancaAceita.ItemEstaVerde(ItemDaSobra).Should().BeFalse();
    }

    [Fact(DisplayName = "A lista de bancas recusada na etapa sem instância superior não esconde a falta de recurso, mas deixa o item pendente")]
    public void Etapa_ComBancasRecusadasESemInstanciaSuperior_PendenteComOuSemBancaAceita()
    {
        DeveFicarPendenteSemCausa(Montar([FaseSemRecurso()], [EtapaSemRecurso()], "etapas[0].bancas"));

        Cenario comBancaAceita = Montar([FaseSemRecurso()], [EtapaSemRecurso(DaSegundaInstanciaNaEtapa())], "etapas[0].bancas");

        comBancaAceita.Causas.Should().ContainSingle().Which.Error.Code.Should().Be(CodigoDaSobraNaEtapa);
        comBancaAceita.Sobras.Indeterminado.Should().BeTrue();
    }

    [Fact(DisplayName = "A fase recusada inteira na leitura deixa o item pendente, e as causas das outras fases saem")]
    public void FaseRecusadaInteira_ItemPendenteEAsOutrasFasesSaem()
    {
        Cenario cenario = Montar([FaseSemRecurso(DaSegundaInstanciaNaFase())], [], "fases");

        cenario.Causas.Should().ContainSingle().Which.Error.Code.Should().Be(CodigoDaSobraNaFase);
        cenario.Sobras.Indeterminado.Should().BeTrue();
        cenario.ItemEstaVerde(ItemDaSobra).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa da fase com a lista de bancas recusada pode ter a sua banca de 2ª instância, e por isso a banca da fase não é recusada")]
    public void Fase_ComEtapaQueTemBancasRecusadas_NaoRecusaABancaDaFase() =>
        DeveFicarPendenteSemCausa(
            Montar([FaseQueNaoAdmite(DaSegundaInstanciaNaFase())], [EtapaQueAdmite(), EtapaSemRecurso()], "etapas[1].bancas"));
}
