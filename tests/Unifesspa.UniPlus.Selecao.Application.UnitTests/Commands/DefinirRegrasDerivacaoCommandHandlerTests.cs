namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ApoioDaDefinicaoDasRegrasDeDerivacao;

/// <summary>
/// Cobertura do <see cref="DefinirRegrasDerivacaoCommandHandler"/> (Story #985): o alvo derivado
/// com binding de regra, a semântica das condições <c>quando</c> contra o vocabulário e a oferta do
/// processo, a recusa das derivações que a oferta define, e a delegação estrutural ao agregado.
/// </summary>
public sealed class DefinirRegrasDerivacaoCommandHandlerTests
{

    [Fact(DisplayName = "Processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo: null, processoId);

        Result resultado = await HandleAsync(mocks, new DefinirRegrasDerivacaoCommand(processoId, []));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Derivação válida (âncora + condicional) é aceita e persistida")]
    public async Task Handle_DerivacaoValida_DefineEPersiste()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            ConfigDerivado(
                new RegraDerivacaoInput(0, "AC", null),
                new RegraDerivacaoInput(1, "AC", [[Condicao("COR_RACA", "IGUAL", "PRETA")]])),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.RegrasDerivacao.Should().ContainSingle(c => c.CodigoFato == Derivado);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fato alvo fora do vocabulário é recusado como desconhecido")]
    public async Task Handle_AlvoDesconhecido_Retorna422()
    {
        ProcessoSeletivo processo = ProcessoBase();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [new ConfiguracaoDerivacaoInput("BOGUS", [new RegraDerivacaoInput(0, "AC", null)])]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDerivacaoFato.FatoDesconhecido");
    }

    [Fact(DisplayName = "Fato alvo declarado (não derivável) é recusado")]
    public async Task Handle_AlvoDeclarado_RetornaNaoDerivavel()
    {
        ProcessoSeletivo processo = ProcessoBase();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [new ConfiguracaoDerivacaoInput("COR_RACA", [new RegraDerivacaoInput(0, "PRETA", null)])]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDerivacaoFato.FatoNaoDerivavel");
    }

    [Theory(DisplayName = "O domínio do fato no catálogo decide a forma das regras: o booleano não contribui código e o categórico, mesmo de fonte não enumerável, contribui")]
    [InlineData("BOOLEANO", null, null)]
    [InlineData("BOOLEANO", "INDIGENA", "ConfiguracaoDerivacaoFato.ContribuicaoIncoerenteComODominio")]
    [InlineData("CATEGORICO", null, "ConfiguracaoDerivacaoFato.ContribuicaoIncoerenteComODominio")]
    public async Task Handle_ContribuicaoConformeODominioDoDerivado_AceitaOuRecusa(string dominio, string? contribui, string? recusa)
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
        [
            .. VocabularioSeed(),
            new FatoCandidatoView(Guid.CreateVersion7(), "INDIGENA_DECLARADO", "Indígena declarado", "Descrição do fato.", dominio,
                "DERIVADO", "ESCALAR", null, "INSCRICAO", "REGRA_DERIVACAO:INDIGENA_DECLARADO", [], dominio == "BOOLEANO" ? null : "GEO_UF", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        ]));
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [new ConfiguracaoDerivacaoInput("INDIGENA_DECLARADO",
                [new RegraDerivacaoInput(0, contribui, [[Condicao("COR_RACA", "IGUAL", "INDIGENA")]])])]);

        Result resultado = await HandleAsync(mocks, command);

        (resultado.Error?.Code).Should().Be(recusa);
    }

    [Theory(DisplayName = "Derivado de fonte global contribui valor do catálogo; o desativado é recusado como desativado e o desconhecido como fora do domínio")]
    [InlineData("EJA", null)]
    [InlineData("PROFICIENCIA", VinculoCatalogoErrorCodes.ValorDesativado)]
    [InlineData("XYZ", "RegrasDerivacaoFato.ContribuiForaDoDominio")]
    public async Task Handle_DerivadoDeFonteGlobal_ContribuiSoValorVigente(string contribui, string? recusa)
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
        [
            .. VocabularioSeed(),
            new FatoCandidatoView(Guid.CreateVersion7(), "FORMA_CONCLUSAO", "Forma de conclusão", "Descrição do fato.", "CATEGORICO", "DERIVADO",
                "MULTIVALORADO", ["EJA", "PROFICIENCIA"], "INSCRICAO", "REGRA_DERIVACAO:FORMA_CONCLUSAO",
                [new FatoValorDominioViewItem("EJA", "EJA", 0, true), new FatoValorDominioViewItem("PROFICIENCIA", "Proficiência", 1, false)],
                "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        ]));
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [new ConfiguracaoDerivacaoInput("FORMA_CONCLUSAO", [new RegraDerivacaoInput(0, contribui, null)])]);

        Result resultado = await HandleAsync(mocks, command);

        if (recusa is null)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Error!.Code.Should().Be(recusa);
        }
    }

    [Theory(DisplayName = "A modalidade do candidato e a opção pelas cotas da lei são recusadas na forma do código do fato")]
    [InlineData("MODALIDADE")]
    [InlineData("OPTOU_PELAS_COTAS_DA_LEI")]
    public async Task Handle_DerivacaoDaOferta_Recusa(string codigoFato)
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await HandleAsync(mocks, new DefinirRegrasDerivacaoCommand(processo.Id,
            [new ConfiguracaoDerivacaoInput(codigoFato, [new RegraDerivacaoInput(0, "AC", null)])]));

        resultado.IsFailure.Should().BeTrue("as duas saem das modalidades ofertadas");
        resultado.Errors.Should().ContainSingle(e => e.Field == "configuracoes[0].codigoFato")
            .Which.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.DerivacaoDaOferta);
        resultado.Errors.Should().ContainSingle("o fato da oferta não é conferido contra o catálogo como alvo");
    }

    [Fact(DisplayName = "Com cota da lei ofertada, a derivação do operador pode citar o egresso que a oferta supre pela regra do sistema")]
    public async Task Handle_CitaOEgressoSuprido_Aceita()
    {
        ProcessoSeletivo processo = ProcessoBase();
        processo.DefinirItens([FatoColetadoDeTeste.Coletando("COR_RACA", 0).ComRotulo("Cor ou raça").DoTipo(TipoRenderizacao.SelecaoUnica).Construir()])
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
                new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo), Modalidades = [ModalidadeDe("AC", NaturezaLegalModalidade.Ampla), ModalidadeDe("LI_EP", NaturezaLegalModalidade.CotaReservada)] },
                new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!],
            FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.RegrasDerivacao.Should().BeEmpty("o processo não configura o egresso");
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await HandleAsync(mocks, new DefinirRegrasDerivacaoCommand(processo.Id,
            [ConfigDerivado(
                new RegraDerivacaoInput(0, "AC", null),
                new RegraDerivacaoInput(1, "AC_PCD", [[Condicao(OrigemEscolar.FatoEgresso, "IGUAL", true)]]))]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Condição 'quando' que cita fato não disponível na configuração é recusada")]
    public async Task Handle_QuandoCitaFatoIndisponivel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // BAIXA_RENDA existe no vocabulário mas não é coletado nem derivado neste processo.
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [ConfigDerivado(new RegraDerivacaoInput(0, "AC", [[Condicao("BAIXA_RENDA", "IGUAL", true)]]))]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
    }

    [Fact(DisplayName = "Condição 'quando' com operador incompatível com o domínio do fato citado é recusada")]
    public async Task Handle_QuandoOperadorIncompativel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // COR_RACA é categórico: MAIOR_IGUAL não se aplica.
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [ConfigDerivado(new RegraDerivacaoInput(0, "AC", [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]]))]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }
}
