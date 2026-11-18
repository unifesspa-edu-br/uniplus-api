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

public sealed class ConfiguracaoDistribuicaoVagasTests
{
    [Fact(DisplayName = "Criar Lei 12.711 com as 8 federais + AC e referência demográfica tem sucesso")]
    public void Criar_Lei12711Completa_Sucesso()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Modalidades.Should().HaveCount(9);
    }

    [Fact(DisplayName = "Criar institucional sem referência demográfica tem sucesso (quadro fixo, sem Censo)")]
    public void Criar_Institucional_SemDemografica_Sucesso()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
            Modalidade("QUIL", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 30),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(60, RegraInstitucional(), modalidades);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.VagasOfertadas.Should().HaveCount(2);
        resultado.Value!.TotalDeVagasDoEdital.Should().Be(60);
    }

    [Theory(DisplayName = "Criar guarda código e rótulo do grupo de área do ENEM, inclusive a ausência dele")]
    [InlineData("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas")]
    [InlineData(null, null)]
    public void Criar_GrupoAreaEnem_Guarda(string? codigo, string? rotulo)
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 60),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 60, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = codigo is null ? null : (codigo, rotulo), OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.GrupoAreaEnem?.Codigo.Should().Be(codigo);
        resultado.Value.GrupoAreaEnem?.Rotulo.Should().Be(rotulo);
        (resultado.Value.GrupoAreaEnem is null).Should().Be(codigo is null);
    }

    [Theory(DisplayName = "Criar recusa grupo de área do ENEM inválido no campo grupoAreaEnem, acumulado com as demais checagens de forma")]
    [InlineData("", "Tecnológica", "GrupoAreaEnemSnapshot.CodigoObrigatorio")]
    [InlineData("TECNOLOGICA", "   ", "GrupoAreaEnemSnapshot.RotuloObrigatorio")]
    [InlineData("CODIGO_LONGO_DEMAIS_PARA_A_COLUNA", "Tecnológica", "GrupoAreaEnemSnapshot.TamanhoInvalido")]
    public void Criar_GrupoAreaEnemInvalido_RecusaNoCampoAcumulado(string codigo, string rotulo, string codigoDeErro)
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 60),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 0, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = (codigo, rotulo), OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e => e.Field == "grupoAreaEnem" && e.Error.Code == codigoDeErro);
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseInvalido",
            "a recusa do grupo se acumula com as demais checagens de forma (ADR-0125)");
    }

    [Theory(DisplayName = "Criar com PR fora de [0,5; 1] falha")]
    [InlineData(0.49)]
    [InlineData(1.01)]
    public void Criar_PercentualDaReservaLegalForaDoLimite_Falha(double pr)
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = (decimal)pr, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.PercentualDaReservaLegalForaDoLimite");
    }

    [Fact(DisplayName = "Criar sem modalidades falha")]
    public void Criar_ModalidadesVazias_Falha()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = [] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadesVazias");
    }

    [Fact(DisplayName = "Criar com modalidade duplicada falha")]
    public void Criar_ModalidadeDuplicada_Falha()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas),
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas),
        ];

        Result<ConfiguracaoDistribuicaoVagas> resultado = CriarConfiguracao(50, RegraInstitucional(), modalidades);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada");
    }

    [Fact(DisplayName = "Criar com RETIRADA_DE_OUTRA_MODALIDADE apontando para código fora do conjunto selecionado falha")]
    public void Criar_ComposicaoOrigemForaDoConjunto_Falha()
    {
        ModalidadeSelecionada retiradaDeOutraModalidadeSemOrigemSelecionada = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
            composicaoOrigemCodigo: "AC", RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), null, "base legal").Value!;

        // "AC" não está selecionado nesta oferta — a origem do RETIRADA_DE_OUTRA_MODALIDADE
        // aponta para uma modalidade ausente do conjunto.
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), Modalidades = [retiradaDeOutraModalidadeSemOrigemSelecionada] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("ConfiguracaoDistribuicaoVagas.ComposicaoOrigemNaoSelecionada");
    }

    [Fact(DisplayName = "Criar com remanejamento CRUZADO apontando para par fora do conjunto falha")]
    public void Criar_RemanejamentoParForaDoConjunto_Falha()
    {
        ModalidadeSelecionada indSemParSelecionado = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "IND", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.Cruzado, null, remanejamentoPar: "QUIL", remanejamentoFallback: "AC",
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), acaoQuandoIndeferido: null, baseLegal: "base legal").Value!;

        ModalidadeSelecionada ac = Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas);

        // "QUIL" (o par) não está selecionado — só "IND" e "AC" (o fallback).
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), Modalidades = [indSemParSelecionado, ac] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("ConfiguracaoDistribuicaoVagas.RemanejamentoParNaoSelecionado");
    }

    [Fact(DisplayName = "Criar Lei 12.711 sem referência demográfica falha")]
    public void Criar_Lei12711SemDemografica_Falha()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaObrigatoria");
    }

    [Fact(DisplayName = "Criar Lei 12.711 faltando uma modalidade federal falha")]
    public void Criar_Lei12711FaltandoFederal_Falha()
    {
        List<ModalidadeSelecionada> modalidades = [.. AsOitoFederaisMaisAc().Where(m => m.Codigo != ModalidadesFederaisLei12711.LiEp)];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadesFederaisIncompletas");
    }

    [Fact(DisplayName = "Criar Lei 12.711 com retirada de origem diferente de AC falha")]
    public void Criar_Lei12711RetiradaForaDeAc_Falha()
    {
        ModalidadeSelecionada retiradaDeSubReserva = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
            composicaoOrigemCodigo: ModalidadesFederaisLei12711.LbPpi, RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), null, "base legal", quantidadeDeclarada: 1).Value!;

        List<ModalidadeSelecionada> modalidades = [.. AsOitoFederaisMaisAc(), retiradaDeSubReserva];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RetiradaFederalDeveSerDeAmplaConcorrencia");
    }

    [Fact(DisplayName = "Criar institucional com referência demográfica indevida falha")]
    public void Criar_InstitucionalComDemografica_Falha()
    {
        List<ModalidadeSelecionada> modalidades = [Modalidade("IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal)];

        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 60, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), ReferenciaDemografica = Demografica(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaIndevida");
    }

    [Fact(DisplayName = "ADR-0125: VO_base inválido e PR fora do limite acumulam no mesmo lote")]
    public void Criar_VagasOfertadasBaseInvalidoEPercentualDaReservaLegalForaDoLimite_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 0, PercentualDaReservaLegal = 1.5m, RegraDeDistribuicao = RegraInstitucional(), Modalidades = [] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseInvalido",
            "ConfiguracaoDistribuicaoVagas.PercentualDaReservaLegalForaDoLimite",
            "ConfiguracaoDistribuicaoVagas.ModalidadesVazias",
        ]);
    }

    [Fact(DisplayName = "ConferirForma sem violação retorna lote vazio")]
    public void ConferirForma_SemViolacao_Vazio()
    {
        IReadOnlyList<FieldError> erros = ConferenciaDaDistribuicaoDeVagas.ConferirForma(50, 0.75m, 1, []);

        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "ConferirForma com zero modalidades reporta ModalidadesVazias sem precisar de nenhuma resolvida")]
    public void ConferirForma_ZeroModalidades_ReportaModalidadesVazias()
    {
        IReadOnlyList<FieldError> erros = ConferenciaDaDistribuicaoDeVagas.ConferirForma(50, 0.75m, 0, []);

        erros.Select(e => e.Error.Code).Should().BeEquivalentTo(["ConfiguracaoDistribuicaoVagas.ModalidadesVazias"]);
    }
}
