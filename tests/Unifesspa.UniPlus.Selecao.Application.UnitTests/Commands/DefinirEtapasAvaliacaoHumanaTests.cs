namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasAvaliacaoHumanaTests
{
    private static RegraCatalogo RegraDoCatalogo(string codigo, TipoRegra tipo) => RegraCatalogo.Criar(
        codigo, "v1", tipo, JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
        "Decisão institucional da Unifesspa");

    private static (ProcessoSeletivo Processo, IProcessoSeletivoRepository Repositorio) ProcessoEmRascunho()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return (processo, repositorio);
    }

    private static DefinirEtapasCommand RedacaoHumana(
        ProcessoSeletivo processo, string? forma, string? regraCodigo, string? regraVersao = "v1") => new(
        processo.Id,
        [
            new EtapaProcessoInput(
                "Redação", CaraterEtapa.Classificatoria.Codigo(), TipoRedacaoOrigemId, 1m, null, 1,
                Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m, PassoDoLancamento: 0.5m,
                FormaDeAvaliacao: forma, RegraConsolidacaoCodigo: regraCodigo, RegraConsolidacaoVersao: regraVersao),
        ]);

    [Fact(DisplayName = "A etapa de avaliação humana congela a regra de consolidação com o hash do catálogo, nunca o do payload")]
    public async Task Handle_FormaHumanaComRegraDeConsolidacao_GravaAReferenciaDoCatalogo()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();
        RegraCatalogo regra = RegraDoCatalogo(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), TipoRegra.RegraApuracaoAvaliacao);
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), "v1", Arg.Any<CancellationToken>()).Returns(regra);

        Result resultado = await Executar(
            RedacaoHumana(processo, "HUMANA", RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo()),
            repository, Substitute.For<IUnitOfWork>(), cadastros: new(Regras: regraCatalogoReader));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.FormaDeAvaliacao.Should().Be(FormaDeAvaliacao.Humana);
        etapa.RegraConsolidacao.Should().Be(
            ReferenciaRegra.De(regra.Codigo, regra.Versao, regra.Hash));
    }

    [Fact(DisplayName = "A regra de consolidação só aceita regra do tipo consolidação")]
    public async Task Handle_RegraDeOutroTipoComoConsolidacao_Recusa()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync("PRECISAO_TRUNCAR", "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDoCatalogo("PRECISAO_TRUNCAR", TipoRegra.RegraArredondamento));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await Executar(
            RedacaoHumana(processo, "HUMANA", "PRECISAO_TRUNCAR"),
            repository, unitOfWork, cadastros: new(Regras: regraCatalogoReader));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].regraConsolidacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static EtapaProcessoInput ObjetivaComPasso(string nome, int ordem) => new(
        nome, CaraterEtapa.Classificatoria.Codigo(), TipoProvaObjetivaOrigemId, 1m, null, ordem,
        Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m, PassoDoLancamento: 0.5m,
        FormaDeAvaliacao: "OBJETIVA",
        RegraConsolidacaoCodigo: RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), RegraConsolidacaoVersao: "v1");

    private static (string Campo, string Codigo)[] RecusasDaObjetivaComPasso(int indice) =>
    [
        ($"etapas[{indice}].regraConsolidacao", EtapaProcessoErrorCodes.RegraDeConsolidacaoForaDaFormaHumana),
        ($"etapas[{indice}].passoDoLancamento", EtapaProcessoErrorCodes.PassoNaFormaObjetiva),
    ];

    [Fact(DisplayName = "A coerência da avaliação acumula entre as etapas, sem ler o catálogo: cada etapa devolve o seu erro")]
    public async Task Handle_DuasEtapasObjetivasComPasso_AcumulaUmErroPorEtapa()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();

        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [ObjetivaComPasso("Prova 1", 1), ObjetivaComPasso("Prova 2", 2)]),
            repository, Substitute.For<IUnitOfWork>(), cadastros: new(Regras: regraCatalogoReader));

        resultado.Errors.Select(e => (e.Field!, e.Error.Code)).Should().BeEquivalentTo([.. RecusasDaObjetivaComPasso(0), .. RecusasDaObjetivaComPasso(1)]);
        await regraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Theory(DisplayName = "Código sem versão, ou versão sem código, é recusado dizendo qual metade falta")]
    [InlineData("APURACAO_AVALIACAO_UNICA", null, "a versão")]
    [InlineData(null, "v1", "o código")]
    [InlineData("APURACAO_AVALIACAO_UNICA", "  ", "a versão")]
    [InlineData("  ", "v1", "o código")]
    public async Task Handle_RegraDeConsolidacaoPelaMetade_Recusa(string? codigo, string? versao, string falta)
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();

        Result resultado = await Executar(
            RedacaoHumana(processo, "HUMANA", codigo, versao),
            repository, Substitute.For<IUnitOfWork>());

        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain(falta);
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida);
    }

    [Theory]
    [InlineData(" APURACAO_AVALIACAO_UNICA", "v1")]
    [InlineData("APURACAO_AVALIACAO_UNICA ", "v1")]
    [InlineData("apuracao_avaliacao_unica", "v1")]
    [InlineData("APURACAO_AVALIACAO_UNICA", " v1")]
    [InlineData("APURACAO_AVALIACAO_UNICA", "v1 ")]
    [InlineData("APURACAO_AVALIACAO_UNICA", "V1")]
    public async Task Handle_RegraDeConsolidacaoComCodigoOuVersaoDiferente_Recusa(string codigo, string versao)
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repositorio) = ProcessoEmRascunho();
        IRegraCatalogoReader catalogo = Substitute.For<IRegraCatalogoReader>();
        catalogo.ObterAsync("APURACAO_AVALIACAO_UNICA", "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDoCatalogo("APURACAO_AVALIACAO_UNICA", TipoRegra.RegraApuracaoAvaliacao));
        Result resultado = await Executar(RedacaoHumana(processo, "HUMANA", codigo, versao),
            repositorio, Substitute.For<IUnitOfWork>(), cadastros: new(Regras: catalogo));
        DomainError erro = resultado.Errors.Should().ContainSingle().Subject.Error;
        erro.Code.Should().Be(EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida);
        erro.Message.Should().NotContain(codigo).And.NotContain(versao);
    }

    [Fact(DisplayName = "Token de forma de avaliação desconhecido é recusado, não lido como ausente")]
    public async Task Handle_FormaDeAvaliacaoDesconhecida_Recusa()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();

        Result resultado = await Executar(
            RedacaoHumana(processo, "HUMANO", regraCodigo: null, regraVersao: null),
            repository, Substitute.For<IUnitOfWork>());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].formaDeAvaliacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.FormaDeAvaliacaoDesconhecida);
    }

    private static EtapaProcessoInput EntrevistaComItens(
        Guid? id, IReadOnlyList<ItemDeAvaliacaoInput> itens, IReadOnlyList<NivelDeAtendimentoInput>? escala = null) => new(
        "Entrevista", CaraterEtapa.Classificatoria.Codigo(), TipoRedacaoOrigemId, 1m, null, 1,
        Id: id, Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m,
        FormaDeAvaliacao: "HUMANA", ItensDeAvaliacao: itens, EscalaDeAtendimento: escala);

    private static async Task<Result> GravarEntrevistaAsync(
        ProcessoSeletivo processo, IProcessoSeletivoRepository repository, Guid? etapaId, IReadOnlyList<ItemDeAvaliacaoInput> itens) =>
        await Executar(
            new DefinirEtapasCommand(processo.Id, [EntrevistaComItens(etapaId, itens)]), repository, Substitute.For<IUnitOfWork>());

    [Fact(DisplayName = "O item reenviado com o id é a mesma linha: o rótulo muda e o id continua; o item sem id é novo")]
    public async Task Handle_ItemComIdExistente_PreservaOIdQuandoORotuloMuda()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();
        (await GravarEntrevistaAsync(processo, repository, null, [new(null, "Clareza", 4m), new(null, "Domínio", 6m)])).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = processo.Etapas.Single();
        Guid idDaClareza = etapa.ItensDeAvaliacao.Single(static i => i.Rotulo == "Clareza").Id;

        (await GravarEntrevistaAsync(processo, repository, etapa.Id, [new(idDaClareza, "Objetividade", 4m), new(null, "Postura", 6m)])).IsSuccess.Should().BeTrue();

        processo.Etapas.Single().ItensDeAvaliacao.Should().HaveCount(2)
            .And.Contain(i => i.Id == idDaClareza && i.Rotulo == "Objetividade");
    }

    [Fact(DisplayName = "Itens e escala inválidos acumulam entre as etapas, antes de qualquer mutação")]
    public async Task Handle_ItensInvalidosEmDuasEtapas_AcumulaUmErroPorEtapa()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();

        EtapaProcessoInput semRotulo = EntrevistaComItens(null, [new(null, " ", 10m)]);
        EtapaProcessoInput escalaIncompleta = EntrevistaComItens(null, [new(null, "Clareza", 10m)], [new("Atendeu parcialmente", 0.5m)]);

        Result resultado = await Executar(new DefinirEtapasCommand(processo.Id, [semRotulo, escalaIncompleta]), repository, Substitute.For<IUnitOfWork>());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("etapas[0].itensDeAvaliacao[0].rotulo", ItemDeAvaliacao.RotuloInvalido),
            ("etapas[1].escalaDeAtendimento", NiveisDeAtendimento.SemNivelCompleto),
        ]);
        processo.Etapas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa itens e escala na gravação, lidos do tipo congelado")]
    public async Task Handle_ItensNaEtapaDoEnem_Recusa()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository) = ProcessoEmRascunho();
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(TipoEtapaViewDeTeste.Com(TipoRedacaoOrigemId, "NOTA_ENEM").Chamado("Nota do ENEM").ComNotaDoEnem().Construir());
        EtapaProcessoInput etapa = EntrevistaComItens(null, [new(null, "Clareza", 10m)]) with { FormaDeAvaliacao = null };

        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [etapa]),
            repository, Substitute.For<IUnitOfWork>(), reader);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].itensDeAvaliacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.ItensNaEtapaDoEnem);
    }
}
