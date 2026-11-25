namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoDeVagas;

public sealed class ConfiguracaoDistribuicaoVagasQuadroTests
{
    [Fact(DisplayName = "Quadro institucional que soma acima do VO_base é recusado")]
    public void Criar_Institucional_QuadroAcimaDoVoBase_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 40),
            Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidadeDeclarada: 2, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(40, RegraInstitucional(), modalidades);

        resultado.IsFailure.Should().BeTrue(
            "as quantidades dividem o total da oferta; publicar 42 numa oferta de 40 contradiz o VO_base declarado");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroExcedeAsVagasOfertadasBase");
        resultado.Error.Message.Should().Contain("42").And.Contain("40");
    }

    [Fact(DisplayName = "Quadro institucional que soma abaixo do VO_base é recusado")]
    public void Criar_Institucional_QuadroAbaixoDoVoBase_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 4),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(40, RegraInstitucional(), modalidades);

        resultado.IsFailure.Should().BeTrue("a oferta tem 40 vagas e o quadro distribuiu 4");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroNaoCompletaAsVagasOfertadasBase");
    }

    [Fact(DisplayName = "Suplementar institucional entra na soma do quadro — não é dispensada do total")]
    public void Criar_Institucional_SuplementaresAcimaDoVoBase_Falha()
    {
        // Dispensar a suplementar da soma neste ramo aceitaria um certame inteiro de
        // suplementares como se distribuísse zero: aqui não há conjunto calculado ao lado
        // ao qual elas pudessem acrescer, e o total publicado é a soma do quadro.
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC_I", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
            Modalidade("AC_Q", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(40, RegraInstitucional(), modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroExcedeAsVagasOfertadasBase");
    }

    [Fact(DisplayName = "Suplementar na Lei 12.711 continua acrescendo ao total, sem cair na regra do quadro institucional")]
    public void Criar_Lei12711_ComSuplementar_AcresceAoTotal()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            .. AsOitoFederaisMaisAc(),
            Modalidade("AC_I", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 5),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsSuccess.Should().BeTrue("no ramo federal a suplementar acresce ao VO_base, e a igualdade do quadro já vem da calculadora");
        resultado.Value!.TotalDeVagasDoEdital.Should().Be(55);
    }

    [Fact(DisplayName = "Lei 12.711 pura recusa AC_PCD — a folga vive só na variação -COM-AC-PCD")]
    public void Criar_Lei12711_ComAcPcd_ForaDoRolDaRegraPura_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            .. AsOitoFederaisMaisAc(),
            Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidadeDeclarada: 2, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { ModalidadesAdmitidas = RolLei12711, OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue("AC_PCD não está no rol de nove da regra pura — só a variação -COM-AC-PCD o admite");
        resultado.Errors.Should().Contain(e =>
            e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeNaoAdmitidaPelaRegra"
            && e.Error.Message.Contains("AC_PCD", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Lei 12.711 com AC_PCD materializa o quadro pelo art. 10 sob a variação -COM-AC-PCD")]
    public void Criar_Lei12711ComAcPcd_ComRolCompleto_MaterializaQuadroPeloArt10()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            .. AsOitoFederaisMaisAc(),
            Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidadeDeclarada: 2, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711ComAcPcd(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { ModalidadesAdmitidas = RolLei12711ComAcPcd, OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsSuccess.Should().BeTrue(
            "sem EhRegraFederal cobrindo a variação, ela cairia no ramo que materializa quadro vazio sem erro");
        resultado.Value!.VagasOfertadas.Should().Contain(v => v.ModalidadeCodigo == "AC_PCD" && v.Quantidade == 2);
    }

    [Fact(DisplayName = "Com AC_PCD sem cotas recusa cota da Lei 12.711 fora do seu rol")]
    public void Criar_ComAcPcd_ComCotaDaLei_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 38),
            Modalidade(ModalidadesFederaisLei12711.LbPpi, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(40, RegraComAcPcd(), modalidades, ["AC", "AC_PCD"]);

        resultado.IsFailure.Should().BeTrue("o rol de COM-AC-PCD só admite AC e AC_PCD — nenhuma cota da Lei 12.711");
        resultado.Errors.Should().Contain(e =>
            e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeNaoAdmitidaPelaRegra"
            && e.Error.Message.Contains(ModalidadesFederaisLei12711.LbPpi, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "VO_base acima das vagas anuais autorizadas da oferta é recusado")]
    public void Criar_VoBaseAcimaDoTetoDaOferta_Falha()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 99999, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { VagasAnuaisAutorizadas = 40, OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue(
            "o certame que ultrapassa o teto publica mais vagas do que o e-MEC autoriza para a oferta");
        resultado.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseAcimaDasVagasAutorizadas");
    }

    [Fact(DisplayName = "VO_base igual às vagas anuais autorizadas é aceito")]
    public void Criar_VoBaseIgualAoTetoDaOferta_Sucesso()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { VagasAnuaisAutorizadas = 40, OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsSuccess.Should().BeTrue("o teto é o limite, e alcançá-lo é legítimo");
    }

    [Fact(DisplayName = "Oferta sem vagas anuais declaradas não impõe teto")]
    public void Criar_OfertaSemTetoDeclarado_NaoImpoeLimite()
    {
        // Ausência do dado não é permissão nem proibição: sem o teto declarado não há contra
        // o que confrontar, e recusar equivaleria a tratar a lacuna como zero.
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 99999, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Com regra de ajuste, o quadro que excede é reconciliado em vez de recusado")]
    public void Criar_QuadroExcedente_ComAjuste_Reconcilia()
    {
        ConfiguracaoDistribuicaoVagas configuracao = CriarFixadoNoEditalComAjuste();

        configuracao.TotalDeVagasDoEdital.Should().Be(40);
        configuracao.Quadro.Should().BeOfType<QuadroDeVagasFixadoNoEdital>()
            .Which.TotalDeVagasRetiradasNoAjuste.Should().Be(4, "o excesso absorvido precisa aparecer, não desaparecer");
        configuracao.VagasOfertadas.Single(v => v.ModalidadeCodigo == "AC").Quantidade.Should().Be(36);
        configuracao.VagasOfertadas.Single(v => v.ModalidadeCodigo == "AC_PCD").Quantidade.Should().Be(4);
    }

    [Fact(DisplayName = "Sem regra de ajuste, o quadro que excede continua sendo recusado")]
    public void Criar_QuadroExcedente_SemAjuste_Recusa()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 40),
            Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidadeDeclarada: 4, composicaoOrigemCodigo: "AC"),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(40, RegraComAcPcd(), modalidades);

        resultado.IsFailure.Should().BeTrue("sem regra de ajuste do quadro declarada não há de onde tirar");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroExcedeAsVagasOfertadasBase");
    }

    [Fact(DisplayName = "A sobra continua recusada mesmo com regra de ajuste — as regras de ajuste do quadro só reduzem")]
    public void Criar_QuadroComSobra_ComAjuste_ContinuaRecusado()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 4),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), RegraDeAjuste = RegraAjuste(), AjusteDoQuadro = new ParametrosReduzirDe("AC"), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue(
            "acrescer seria o sistema criar vaga que ninguém autorizou — quem configura fecha a conta");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroNaoCompletaAsVagasOfertadasBase");
    }
}
