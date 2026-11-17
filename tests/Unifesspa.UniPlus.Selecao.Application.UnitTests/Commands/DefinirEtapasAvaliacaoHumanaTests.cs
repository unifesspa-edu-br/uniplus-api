namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasAvaliacaoHumanaTests
{
    private static RegraCatalogo RegraDoCatalogo(string codigo, TipoRegra tipo) => RegraCatalogo.Criar(
        codigo, "v1", tipo, JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
        "Decisão institucional da Unifesspa");

    private static DefinirEtapasCommand RedacaoHumana(
        ProcessoSeletivo processo, string? forma, string? regraCodigo, string? regraVersao = "v1") => new(
        processo.Id,
        [
            new EtapaProcessoInput(
                "Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1,
                Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m, PassoDoLancamento: 0.5m,
                FormaDeAvaliacao: forma, RegraConsolidacaoCodigo: regraCodigo, RegraConsolidacaoVersao: regraVersao),
        ]);

    [Fact(DisplayName = "A etapa de avaliação humana congela a regra de consolidação com o hash do catálogo, nunca o do payload")]
    public async Task Handle_FormaHumanaComRegraDeConsolidacao_GravaAReferenciaDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        RegraCatalogo regra = RegraDoCatalogo(RegraConsolidacaoCodigo.CorrecaoIndependente, TipoRegra.RegraConsolidacao);
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", Arg.Any<CancellationToken>()).Returns(regra);

        Result resultado = await Executar(
            RedacaoHumana(processo, FormaDeAvaliacaoCodigo.Humana, RegraConsolidacaoCodigo.CorrecaoIndependente),
            repository, Substitute.For<IUnitOfWork>(), regraCatalogoReader: regraCatalogoReader);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.FormaDeAvaliacao.Should().Be(FormaDeAvaliacao.Humana);
        etapa.RegraConsolidacao.Should().Be(
            ReferenciaRegra.De(regra.Codigo, regra.Versao, regra.Hash));
    }

    [Fact(DisplayName = "A regra de consolidação só aceita regra do tipo consolidação")]
    public async Task Handle_RegraDeOutroTipoComoConsolidacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync("PRECISAO-TRUNCAR", "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDoCatalogo("PRECISAO-TRUNCAR", TipoRegra.RegraArredondamento));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await Executar(
            RedacaoHumana(processo, FormaDeAvaliacaoCodigo.Humana, "PRECISAO-TRUNCAR"),
            repository, unitOfWork, regraCatalogoReader: regraCatalogoReader);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].regraConsolidacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A coerência da avaliação acumula entre as etapas, sem ler o catálogo: cada etapa devolve o seu erro")]
    public async Task Handle_DuasEtapasObjetivasComPasso_AcumulaUmErroPorEtapa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();

        EtapaProcessoInput Objetiva(string nome, int ordem) => new(
            nome, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, ordem,
            Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m, PassoDoLancamento: 0.5m,
            FormaDeAvaliacao: FormaDeAvaliacaoCodigo.Objetiva,
            RegraConsolidacaoCodigo: RegraConsolidacaoCodigo.AvaliacaoUnica, RegraConsolidacaoVersao: "v1");

        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [Objetiva("Prova 1", 1), Objetiva("Prova 2", 2)]),
            repository, Substitute.For<IUnitOfWork>(), regraCatalogoReader: regraCatalogoReader);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("etapas[0].regraConsolidacao", EtapaProcessoErrorCodes.RegraDeConsolidacaoForaDaFormaHumana),
            ("etapas[0].passoDoLancamento", EtapaProcessoErrorCodes.PassoNaFormaObjetiva),
            ("etapas[1].regraConsolidacao", EtapaProcessoErrorCodes.RegraDeConsolidacaoForaDaFormaHumana),
            ("etapas[1].passoDoLancamento", EtapaProcessoErrorCodes.PassoNaFormaObjetiva),
        ]);
        await regraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Theory(DisplayName = "Código sem versão, ou versão sem código, é recusado dizendo qual metade falta")]
    [InlineData("CONSOLIDACAO-AVALIACAO-UNICA", null, "a versão")]
    [InlineData(null, "v1", "o código")]
    public async Task Handle_RegraDeConsolidacaoPelaMetade_Recusa(string? codigo, string? versao, string falta)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        Result resultado = await Executar(
            RedacaoHumana(processo, FormaDeAvaliacaoCodigo.Humana, codigo, versao),
            repository, Substitute.For<IUnitOfWork>());

        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain(falta);
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida);
    }

    [Fact(DisplayName = "Token de forma de avaliação desconhecido é recusado, não lido como ausente")]
    public async Task Handle_FormaDeAvaliacaoDesconhecida_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        Result resultado = await Executar(
            RedacaoHumana(processo, "HUMANO", regraCodigo: null, regraVersao: null),
            repository, Substitute.For<IUnitOfWork>());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].formaDeAvaliacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.FormaDeAvaliacaoDesconhecida);
    }

    private static EtapaProcessoInput EntrevistaComItens(
        Guid? id, IReadOnlyList<ItemDeAvaliacaoInput> itens, IReadOnlyList<NivelDeAtendimentoInput>? escala = null) => new(
        "Entrevista", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1,
        Id: id, Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m,
        FormaDeAvaliacao: FormaDeAvaliacaoCodigo.Humana, ItensDeAvaliacao: itens, EscalaDeAtendimento: escala);

    [Fact(DisplayName = "O item reenviado com o id é a mesma linha: o rótulo muda e o id continua; o item sem id é novo")]
    public async Task Handle_ItemComIdExistente_PreservaOIdQuandoORotuloMuda()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        (await Executar(
            new DefinirEtapasCommand(processo.Id, [EntrevistaComItens(null, [new(null, "Clareza", 4m), new(null, "Domínio", 6m)])]),
            repository, unitOfWork)).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = processo.Etapas.Single();
        Guid idDaClareza = etapa.ItensDeAvaliacao.Single(static i => i.Rotulo == "Clareza").Id;

        (await Executar(
            new DefinirEtapasCommand(
                processo.Id,
                [EntrevistaComItens(etapa.Id, [new(idDaClareza, "Objetividade", 4m), new(null, "Postura", 6m)])]),
            repository, unitOfWork)).IsSuccess.Should().BeTrue();

        processo.Etapas.Single().ItensDeAvaliacao.Should().HaveCount(2)
            .And.Contain(i => i.Id == idDaClareza && i.Rotulo == "Objetividade");
    }

    [Fact(DisplayName = "Itens e escala inválidos acumulam entre as etapas, antes de qualquer mutação")]
    public async Task Handle_ItensInvalidosEmDuasEtapas_AcumulaUmErroPorEtapa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await Executar(
            new DefinirEtapasCommand(
                processo.Id,
                [
                    EntrevistaComItens(null, [new(null, " ", 10m)]),
                    EntrevistaComItens(null, [new(null, "Clareza", 10m)], [new("Atendeu parcialmente", 0.5m)]),
                ]),
            repository, unitOfWork);

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
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoRedacaoOrigemId, "NOTA_ENEM", "Nota do ENEM", null, AdmitePontuacao: true, AdmiteEliminacao: true, NotaDeOrigemNoEnem: true, ExigeCorrecaoIndependente: false, ConceitosAdmitidos: []));
        EtapaProcessoInput etapa = EntrevistaComItens(null, [new(null, "Clareza", 10m)]) with { FormaDeAvaliacao = null };

        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [etapa]),
            repository, Substitute.For<IUnitOfWork>(), reader);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].itensDeAvaliacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.ItensNaEtapaDoEnem);
    }
}
