namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

/// <summary>
/// O que se exige da banca que julga o recurso por ter avaliado o item, sem banca dedicada no lugar: a votação do recurso e o
/// mínimo de integrantes. Onde não se sabe quem julga, nada se exige e o item do painel fica pendente.
/// </summary>
public sealed class DeliberacaoEMinimoDeQuemJulgaTests
{
    private const string Fase = "AVALIACAO";
    private const string ItemDaDeliberacao = "cronograma_banca_com_regra_de_deliberacao";
    private const string ItemDoMinimo = "cronograma_banca_com_o_minimo_de_integrantes";

    private static readonly ProcedimentoDeRecursoDeclarado[] DaNota = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa)];

    private static readonly TipoDeBancaCopiado Entrevista = Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa);

    private static readonly ReferenciaRegra RegraLida = ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('a', 64));

    private static readonly ReferenciaRegra RegraForaDoCatalogo = ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('b', 64));

    private static readonly CorretoresDasEtapas TresCorretores =
        new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [RegraLida] = CorretoresDaRegra.SemNovoCorretor(3) });

    private static RegraDeDeliberacao AtoIndividualSemVotacao() =>
        RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(ModoDeDeliberacao.AtoIndividual.Codigo(), 1, 0, new(null, null, null, null, null))).Value!;

    private static BancaDaEtapa AvaliadoraSemVotacao() => BancaDaEtapa.Criar(Entrevista, AtoIndividualSemVotacao());

    private static ModeloDeEtapa DaRegraLida(ModeloDeEtapa etapa, ReferenciaRegra? regra = null) =>
        etapa with { Forma = FormaDeAvaliacao.Humana, RegraConsolidacao = regra ?? RegraLida, Passo = 0.5m };

    private static IReadOnlyList<FieldError> Deliberacao(ModeloDeFase fase, params ModeloDeEtapa[] etapas) =>
        ConferenciaDasBancas.ConferirRegrasDeDeliberacao(Fases(fase), Etapas(etapas));

    private static IReadOnlyList<FieldError> Minimo(ModeloDeFase fase, CorretoresDasEtapas corretores, params ModeloDeEtapa[] etapas) =>
        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(Fases(fase), Etapas(etapas), corretores);

    private static ItemConformidade ItemDoPainel(string codigo, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, cronograma, etapas, TresCorretores).Single(item => item.Codigo == codigo);

    private static (CronogramaDoProcesso Cronograma, EtapasDoProcesso Etapas) ComRecursosRecusados(ModeloDeEtapa etapa) =>
        (CronogramaDoProcesso.Do([new ModeloDeFase(Fase, 1).Adotada()]), EtapasDoProcesso.Das([etapa.Adotada()], ComPartesRecusadas("etapas[0].recursos")));

    [Fact(DisplayName = "A banca que julga o recurso por ter avaliado o item e decide por ato individual sem votação é recusada, dizendo por que julga")]
    public void Avaliadora_QueJulga_AtoIndividualSemVotacao_Recusa()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, AvaliadoraSemVotacao());

        FieldError recusa = Deliberacao(new ModeloDeFase(Fase, 1), etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas[0]");
        recusa.Error.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
        recusa.Error.Message.Should().Contain(
            "da etapa na posição 1 decide por ato individual e julga recurso por ter avaliado o item recorrido, sem banca dedicada no lugar, mas não declara a votação do recurso.");
    }

    [Fact(DisplayName = "A avaliadora não é recusada quando há banca dedicada a julgar o recurso no lugar")]
    public void Avaliadora_ComBancaDedicadaNoLugar_NaoRecusa()
    {
        ModeloDeFase fase = new ModeloDeFase(Fase, 1) with { Bancas = [BancasDeTeste.DeRecursosDaFase()] };
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, AvaliadoraSemVotacao());

        Deliberacao(fase, etapa).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de 2ª instância da fase, sem etapa que ela julgue, tem como mínimo o quórum e não é cobrada dos corretores")]
    public void BancaDeSegundaInstanciaDaFase_SemEtapaJulgada_MinimoEOQuorum()
    {
        BancaRequerida pequena = BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), [], BancasDeTeste.Regra(integrantes: 2, quorum: 2));
        ModeloDeFase fase = FaseComRecurso(Fase, DaNota, true, pequena);
        ModeloDeEtapa etapa = DaRegraLida(EtapaNaFase(Fase));

        Minimo(fase, TresCorretores, etapa).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de 2ª instância da etapa tem como mínimo o quórum, e não os corretores da etapa")]
    public void BancaDeSegundaInstanciaDaEtapa_MinimoEOQuorum_NaoRecusa()
    {
        BancaDaEtapa pequena = BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), BancasDeTeste.Regra(integrantes: 2, quorum: 2));
        ModeloDeEtapa etapa = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, true, pequena));

        Minimo(new ModeloDeFase(Fase, 1), TresCorretores, etapa).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de 2ª instância da etapa, cuja regra de consolidação o catálogo não confirmou, não é recusada por regra não resolvida")]
    public void BancaDeSegundaInstanciaDaEtapa_RegraNaoResolvida_NaoRecusa()
    {
        BancaDaEtapa banca = BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), BancasDeTeste.Regra());
        ModeloDeEtapa etapa = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, true, banca), RegraForaDoCatalogo);

        Minimo(new ModeloDeFase(Fase, 1), TresCorretores, etapa).Should().BeEmpty();
    }

    [Fact(DisplayName = "Na fase com a banca dedicada à 1ª instância e a de 2ª instância, só a de 1ª instância é cobrada dos corretores da etapa que ela julga")]
    public void FaseComAsDuasBancas_SoADePrimeiraInstanciaEJulgaAEtapa()
    {
        BancaRequerida primeira = BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeRecursos(Guid.CreateVersion7()), [], BancasDeTeste.Regra(integrantes: 2, quorum: 2));
        BancaRequerida segunda = BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(Guid.CreateVersion7()), [], BancasDeTeste.Regra(integrantes: 2, quorum: 2));
        ModeloDeFase fase = new ModeloDeFase(Fase, 1) with { Bancas = [primeira, segunda] };
        ModeloDeEtapa etapa = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, true));

        Causas(Minimo(fase, TresCorretores, etapa)).Should().Equal(("fases[0].bancasRequeridas[0]", "Banca.IntegrantesAbaixoDoMinimo"));
    }

    [Fact(DisplayName = "A avaliadora de etapa sem recurso, ou cujo item recorrido é de outro tipo de decisão, não é recusada")]
    public void Avaliadora_SemRecursoSobreOSeuItem_NaoRecusa()
    {
        ModeloDeEtapa semRecurso = EtapaNaFase(Fase) with { Bancas = [AvaliadoraSemVotacao()] };
        ModeloDeEtapa deOutroTipo = EtapaComRecurso("Redação", Fase, [Procedimento(TipoMotivoDeDecisao.AnaliseDocumental)], false, AvaliadoraSemVotacao());

        Deliberacao(new ModeloDeFase(Fase, 1), semRecurso).Should().BeEmpty();
        Deliberacao(new ModeloDeFase(Fase, 1), deOutroTipo).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de recursos da fase, com papel que julga recurso, decide por ato individual sem votação e é recusada citando o papel")]
    public void BancaDeRecursosDaFase_AtoIndividualSemVotacao_RecusaCitandoOPapel()
    {
        BancaRequerida banca = BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeRecursos(Guid.CreateVersion7()), [], AtoIndividualSemVotacao());
        ModeloDeFase fase = FaseComRecurso(Fase, DaNota, false, banca);

        FieldError recusa = Deliberacao(fase).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].bancasRequeridas[0]");
        recusa.Error.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
        recusa.Error.Message.Should().Contain("julga recurso com o papel 'Julga o recurso em 1ª instância', mas não declara a votação do recurso.");
    }

    [Fact(DisplayName = "A banca de 2ª instância da fase, com papel que julga recurso, decide por ato individual sem votação e é recusada citando o papel")]
    public void BancaDeSegundaInstanciaDaFase_AtoIndividualSemVotacao_RecusaCitandoOPapel()
    {
        BancaRequerida banca = BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), [], AtoIndividualSemVotacao());
        ModeloDeFase fase = FaseComRecurso(Fase, DaNota, true, banca);

        FieldError recusa = Deliberacao(fase).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].bancasRequeridas[0]");
        recusa.Error.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
        recusa.Error.Message.Should().Be(
            "A banca 'Banca de análise de recursos de 2ª instância 1' da fase 'AVALIACAO' decide por ato individual e julga recurso com o papel 'Julga o recurso em 2ª instância', mas não declara a votação do recurso. Declare o quórum, o critério e o encerramento usados no julgamento do recurso.");
        ItemDoPainel(ItemDaDeliberacao, Fases(fase), Etapas()).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A banca de 2ª instância da etapa, com papel que julga recurso, decide por ato individual sem votação e é recusada citando a posição da etapa")]
    public void BancaDeSegundaInstanciaDaEtapa_AtoIndividualSemVotacao_RecusaCitandoOPapel()
    {
        BancaDaEtapa banca = BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), AtoIndividualSemVotacao());
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, true, banca);

        FieldError recusa = Deliberacao(new ModeloDeFase(Fase, 1), etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas[0]");
        recusa.Error.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
        recusa.Error.Message.Should().Contain(
            "da etapa na posição 1 decide por ato individual e julga recurso com o papel 'Julga o recurso em 2ª instância', mas não declara a votação do recurso.");
    }

    [Fact(DisplayName = "Com duas avaliadoras do item, as duas são cobradas da votação do recurso")]
    public void DuasAvaliadoras_AsDuasSaoCobradasDaVotacao()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, AvaliadoraSemVotacao(), AvaliadoraSemVotacao());

        Causas(Deliberacao(new ModeloDeFase(Fase, 1), etapa)).Should().Equal(
            ("etapas[0].bancas[0]", "Banca.RegraDeDeliberacaoObrigatoria"),
            ("etapas[0].bancas[1]", "Banca.RegraDeDeliberacaoObrigatoria"));
    }

    [Fact(DisplayName = "Com duas avaliadoras do item, as duas são cobradas do mínimo de integrantes")]
    public void DuasAvaliadoras_AsDuasSaoCobradasDoMinimo()
    {
        BancaDaEtapa pequena = BancaDaEtapa.Criar(Entrevista, BancasDeTeste.Regra(integrantes: 2, quorum: 2));
        ModeloDeEtapa etapa = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, false, pequena, pequena));

        Causas(Minimo(new ModeloDeFase(Fase, 1), TresCorretores, etapa)).Should().Equal(
            ("etapas[0].bancas[0]", "Banca.IntegrantesAbaixoDoMinimo"),
            ("etapas[0].bancas[1]", "Banca.IntegrantesAbaixoDoMinimo"));
    }

    [Theory(DisplayName = "Os papéis de 1ª e de 2ª instância julgam recurso para a votação e os candidatos atendidos; só o de 1ª instância ordena quem julga essa instância e traz os corretores para o mínimo")]
    [InlineData(PapelDaBanca.AvaliaOMerito, false, false)]
    [InlineData(PapelDaBanca.ImportaAsNotasDoInep, false, false)]
    [InlineData(PapelDaBanca.ImportaCandidatos, false, false)]
    [InlineData(PapelDaBanca.JulgaAPrimeiraInstancia, true, true)]
    [InlineData(PapelDaBanca.JulgaASegundaInstancia, false, true)]
    public void PapelDaCopia_JulgaRecursoEPrimeiraInstancia(PapelDaBanca papel, bool julgaAPrimeiraInstancia, bool julgaRecurso)
    {
        TipoDeBancaCopiado tipo = TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.AnaliseDeRecursos, papel);

        tipo.JulgaAPrimeiraInstancia.Should().Be(julgaAPrimeiraInstancia);
        tipo.JulgaRecurso.Should().Be(julgaRecurso);
    }

    [Fact(DisplayName = "A avaliadora com a lista dos recursos da etapa recusada na leitura não é cobrada da votação, e o item do painel fica pendente")]
    public void Avaliadora_ComRecursosRecusadosNaLeitura_NaoCobraAVotacaoEDeixaPendente()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, AvaliadoraSemVotacao());
        (CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) = ComRecursosRecusados(etapa);

        ConferenciaDasBancas.ConferirRegrasDeDeliberacao(cronograma, etapas).Should().BeEmpty();
        ItemDoPainel(ItemDaDeliberacao, cronograma, etapas).Ok.Should().BeFalse();
        Deliberacao(new ModeloDeFase(Fase, 1), etapa).Should().ContainSingle();
    }

    [Fact(DisplayName = "A avaliadora que julga o recurso e tem menos integrantes que os corretores da etapa é recusada na posição dela")]
    public void Avaliadora_QueJulga_AbaixoDoMinimoDosCorretores_Recusa()
    {
        ModeloDeEtapa etapa = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, false, BancaDaEtapa.Criar(Entrevista, BancasDeTeste.Regra(integrantes: 2, quorum: 2))));

        FieldError recusa = Minimo(new ModeloDeFase(Fase, 1), TresCorretores, etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas[0]");
        recusa.Error.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        recusa.Error.Message.Should().Contain("declara 2 integrantes, e o mínimo exigido é 3");
    }

    [Fact(DisplayName = "A avaliadora dispensada pela banca de recursos da fase, ou a de recurso da fase, não entra no mínimo de integrantes")]
    public void Avaliadora_QueNaoJulgaEtapaNenhuma_NaoEntraNoMinimo()
    {
        BancaDaEtapa pequena = BancaDaEtapa.Criar(Entrevista, BancasDeTeste.Regra(integrantes: 2, quorum: 2));
        ModeloDeFase comDedicada = new ModeloDeFase(Fase, 1) with { Bancas = [BancasDeTeste.DeRecursosDaFase()] };
        ModeloDeEtapa dispensada = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, false, pequena));
        ModeloDeEtapa semRecursoProprio = DaRegraLida(EtapaNaFase(Fase) with { Bancas = [pequena] });

        Minimo(comDedicada, TresCorretores, dispensada).Should().BeEmpty();
        Minimo(FaseComRecurso(Fase, DaNota), TresCorretores, semRecursoProprio).Should().BeEmpty();
    }

    [Fact(DisplayName = "A avaliadora que julga etapa de regra de consolidação não confirmada no catálogo é recusada por regra não resolvida")]
    public void Avaliadora_QueJulga_RegraNaoResolvida_Recusa()
    {
        ModeloDeEtapa etapa = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, false, NaEtapa(Entrevista)), RegraForaDoCatalogo);

        FieldError recusa = Minimo(new ModeloDeFase(Fase, 1), TresCorretores, etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas[0]");
        recusa.Error.Code.Should().Be("EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
        recusa.Error.Message.Should().Contain("julga a etapa na posição 1, cuja regra de consolidação não pôde ser confirmada no catálogo");
    }

    [Fact(DisplayName = "Com a lista dos recursos da etapa recusada na leitura, o mínimo da avaliadora não é conferido, e o item do painel fica pendente")]
    public void Avaliadora_ComRecursosRecusadosNaLeitura_NaoConfereOMinimo()
    {
        ModeloDeEtapa etapa = DaRegraLida(EtapaComRecurso("Redação", Fase, DaNota, false, BancaDaEtapa.Criar(Entrevista, BancasDeTeste.Regra(integrantes: 2, quorum: 2))));
        (CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) = ComRecursosRecusados(etapa);

        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, TresCorretores).Should().BeEmpty();
        ItemDoPainel(ItemDoMinimo, cronograma, etapas).Ok.Should().BeFalse();
    }

    // Uma fase com dois recursos (um sem banca, outro sobre item que se encerra na 1ª instância) e duas etapas: a primeira tem a
    // avaliadora de tipo que exige duas bancas, por ato individual e abaixo dos corretores; a segunda tem regra de consolidação
    // que o catálogo não confirmou.
    private static ContextoDaConferencia ContextoComUmaCausaDeCadaAssunto()
    {
        TipoDeBancaCopiado queEncerra = AvaliadoraComMarcas(TipoDeBanca.Heteroidentificacao, true, true, TipoMotivoDeDecisao.Heteroidentificacao);
        ModeloDeFase fase = FaseComRecurso(
            Fase, [Procedimento(TipoMotivoDeDecisao.Heteroidentificacao), Procedimento(TipoMotivoDeDecisao.HomologacaoDasInscricoes)], true);
        BancaDaEtapa avaliadoraQueEncerra = BancaDaEtapa.Criar(queEncerra, AtoIndividualSemVotacao());
        ModeloDeEtapa daHeteroidentificacao = EtapaComRecurso("Heteroidentificação", Fase, [Procedimento(TipoMotivoDeDecisao.Heteroidentificacao)], false, avaliadoraQueEncerra);
        ModeloDeEtapa daEntrevista = EtapaComRecurso("Entrevista", Fase, DaNota, false, NaEtapa(Entrevista));
        ModeloDeEtapa primeira = DaRegraLida(daHeteroidentificacao with { Ordem = 1 });
        ModeloDeEtapa segunda = DaRegraLida(daEntrevista with { Ordem = 2 }, RegraForaDoCatalogo);
        ConfiguracaoDeclarada configuracao = Lida([fase.Adotada()], [primeira.Adotada(), segunda.Adotada()], OrigemNoDocumento.Identidade);

        return ContextoDaConferencia.De(configuracao, CadastrosDeTeste.Com(corretores: TresCorretores));
    }

    [Fact(DisplayName = "As causas das bancas saem na ordem única: sem banca, instância superior, mínimo por tipo, deliberação, mínimo de integrantes e consolidação")]
    public void CausasDasBancas_SaemNaOrdemUnica()
    {
        IEnumerable<FieldError> causas = CausasQueImpedemCongelar.Assuntos
            .Single(static assunto => assunto.Nome == NomeDoAssunto.Bancas)
            .Causas(ContextoComUmaCausaDeCadaAssunto());

        Causas(causas).Should().Equal(
            ("fases[0].bancasRequeridas", "Cronograma.RecursoSemBanca"),
            ("fases[0].regraRecurso.admiteInstanciaSuperior", "Banca.TipoNaoAdmiteInstanciaSuperior"),
            ("etapas[0].bancas[0]", "Banca.TipoExigeMaisDeUmaBanca"),
            ("etapas[0].bancas[0]", "Banca.RegraDeDeliberacaoObrigatoria"),
            ("etapas[0].bancas[0]", "Banca.IntegrantesAbaixoDoMinimo"),
            ("etapas[1].bancas[0]", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida"));
    }
}
