namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// Os tipos de decisão de cada etapa são conferidos pela etapa, ao declará-los, e a recusa deles se acumula com a dos
/// demais filhos da mesma etapa e com a das outras etapas. Só o procedimento de recurso, que lê os tipos declarados,
/// deixa de ser avaliado quando os tipos são recusados.
/// </summary>
public sealed class DefinirEtapasTiposDeDecisaoTests
{
    private const string Classificacao = "CLASSIFICACAO";
    private const string Isencao = "ISENCAO_DA_TAXA";
    private const string Heteroidentificacao = "HETEROIDENTIFICACAO";
    private const string TipoForaDoVocabulario = "RECURSO_DA_BANCA";
    private static readonly DateTimeOffset Inicio = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FimAntesDoInicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TipoBancaId = Guid.CreateVersion7();

    private static EtapaProcessoInput Etapa(int ordem, string nome = "Prova Objetiva", Guid? id = null, params string?[] tipos) => new(
        nome, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, ordem, id,
        Produtos: [], Bancas: [], Recursos: [], TiposDeDecisao: tipos);

    private static DefinirEtapasCommand Comando(ProcessoSeletivo processo, params EtapaProcessoInput[] etapas) =>
        new(processo.Id, etapas);

    private static IProcessoSeletivoRepository RepositorioDe(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repository;
    }

    private static (string? Campo, string Codigo)[] Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    private static ProdutoDaEtapaInput Definitivo() => new(AtoDaClassificacao, PapelProdutoFaseCodigo.Definitivo);

    private static EtapaProcessoInput ComProdutosDoRecurso(EtapaProcessoInput etapa, params ProcedimentoDeRecursoInput[] procedimentos) =>
        etapa with
        {
            Produtos = [new ProdutoDaEtapaInput(AtoDaClassificacao, PapelProdutoFaseCodigo.Preliminar), Definitivo()],
            Recursos =
            [
                new RecursoDaEtapaInput(
                    AncoraDoRecurso.AtoPublicado, RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", 2m, "diasUteis",
                    AtoDaClassificacao, false, null, null, "semEfeitoSuspensivo", null, procedimentos),
            ],
        };

    private static ProcedimentoDeRecursoInput ProcedimentoDaClassificacao(string regra = RegraProcedimentoRecursoCodigo.Recalculo) =>
        new(Classificacao, null, regra, "v1");

    private static EtapaProcessoInput EtapaComJanelaInvertidaETipoInvalido() =>
        Etapa(1, tipos: [TipoForaDoVocabulario]) with
        {
            Inicio = Inicio,
            Fim = FimAntesDoInicio,
        };

    private static ITipoAtoPublicadoReader CatalogoComOAtoDaClassificacao()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(AtoDaClassificacao, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(TipoAtoDaClassificacao());
        return catalogo;
    }

    private static IRegraCatalogoReader CatalogoDasRegrasDoRecurso()
    {
        RegraCatalogo prazo = RegraDePrazoDeRecurso();
        RegraCatalogo procedimento = RegraDoProcedimentoDeRecalculo();
        IRegraCatalogoReader catalogo = Substitute.For<IRegraCatalogoReader>();
        catalogo.ObterAsync(prazo.Codigo, prazo.Versao, Arg.Any<CancellationToken>()).Returns(prazo);
        catalogo.ObterAsync(procedimento.Codigo, procedimento.Versao, Arg.Any<CancellationToken>()).Returns(procedimento);
        return catalogo;
    }

    private static ITipoBancaReader CadastroComOTipoDeBanca()
    {
        ITipoBancaReader cadastro = Substitute.For<ITipoBancaReader>();
        cadastro.ObterPorIdAsync(TipoBancaId, Arg.Any<CancellationToken>())
            .Returns(new TipoBancaView(TipoBancaId, TipoDeBanca.Entrevista, "Banca de entrevista", null, null, PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.NotaDaEtapa]));
        return cadastro;
    }

    private static Task<Result> ExecutarComOsCadastros(ProcessoSeletivo processo, params EtapaProcessoInput[] etapas) =>
        Executar(
            Comando(processo, etapas), RepositorioDe(processo), Substitute.For<IUnitOfWork>(),
            cadastros: new(TiposDeBanca: CadastroComOTipoDeBanca(), Regras: CatalogoDasRegrasDoRecurso(), Atos: CatalogoComOAtoDaClassificacao()));

    [Fact(DisplayName = "A etapa declara os tipos de decisão e a leitura os devolve em ordem canônica, do comando ao DTO")]
    public async Task Handle_EtapaComTipos_GravaEDevolveEmOrdemCanonica()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [Isencao, Classificacao, Heteroidentificacao])), RepositorioDe(processo), unitOfWork);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        EtapaProcessoDto dto = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(processo.Etapas.Single());
        dto.Publicacoes.TiposDeDecisao.Select(static tipo => tipo.Codigo).Should().Equal(Classificacao, Heteroidentificacao, Isencao);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A gravação seguinte sem tipos limpa a declaração da mesma etapa")]
    public async Task Handle_EtapaExistenteSemTipos_LimpaADeclaracao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = RepositorioDe(processo);
        (await Executar(Comando(processo, Etapa(1, tipos: [Classificacao])), repository, Substitute.For<IUnitOfWork>())).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = processo.Etapas.Single();

        Result resultado = await Executar(
            Comando(processo, Etapa(1, id: etapa.Id)), repository, Substitute.For<IUnitOfWork>());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().ContainSingle().Which.Should().BeSameAs(etapa).And.Match<EtapaProcesso>(e => e.TiposDeDecisao.Count == 0);
    }

    [Fact(DisplayName = "A forma inválida de uma etapa corta o que depende de cadastro: o tipo inválido de outra não é julgado")]
    public async Task Handle_TipoInvalidoNumaEtapaEFormaInvalidaEmOutra_RecusaSoAForma()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();

        Result resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [null]), Etapa(2, nome: "")),
            RepositorioDe(processo), Substitute.For<IUnitOfWork>(), tipoEtapaReader);

        Recusas(resultado).Should().Equal(("etapas[1].nome", "EtapaProcesso.NomeObrigatorio"));
        tipoEtapaReader.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipos inválidos em várias etapas saem juntos, por posição de etapa e de tipo")]
    public async Task Handle_TiposInvalidosEmVariasEtapas_RecusaPorPosicao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result resultado = await Executar(
            Comando(
                processo,
                Etapa(1, tipos: [TipoForaDoVocabulario]),
                Etapa(2, tipos: [Classificacao]),
                Etapa(3, tipos: [Isencao, Classificacao, Isencao])),
            RepositorioDe(processo), Substitute.For<IUnitOfWork>());

        Recusas(resultado).Should().Equal(
            ("etapas[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("etapas[2].tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado));
    }

    [Fact(DisplayName = "Etapa com tipo inválido e janela invertida devolve as duas recusas, na ordem da declaração")]
    public async Task Handle_TipoInvalidoEJanelaInvertida_RecusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result resultado = await ExecutarComOsCadastros(processo, EtapaComJanelaInvertidaETipoInvalido());

        Recusas(resultado).Should().Equal(
            ("etapas[0].fim", "EtapaProcesso.JanelaInvertida"),
            ("etapas[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
        resultado.Error!.Code.Should().Be("EtapaProcesso.JanelaInvertida");
    }

    [Fact(DisplayName = "Tipo inválido na etapa 0 não impede o julgamento da etapa 1, que recusa o ato repetido")]
    public async Task Handle_TipoInvalidoNaEtapaZeroEAtoRepetidoNaEtapaUm_RecusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcessoInput comTipoInvalido = Etapa(1, tipos: [TipoForaDoVocabulario]);
        EtapaProcessoInput comAtoRepetido = Etapa(2, nome: "Redação") with { Produtos = [Definitivo(), Definitivo()] };

        Result resultado = await ExecutarComOsCadastros(processo, comTipoInvalido, comAtoRepetido);

        Recusas(resultado).Should().Equal(
            ("etapas[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("etapas[1].produtos[1]", "EtapaProcesso.AtoDuplicadoNaEtapa"));
    }

    [Fact(DisplayName = "Tipos válidos sem o tipo do procedimento continuam recusando o procedimento da etapa")]
    public async Task Handle_TiposValidosSemOTipoDoProcedimento_RecusaOProcedimento()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcessoInput etapa = ComProdutosDoRecurso(Etapa(1, tipos: [Isencao]), ProcedimentoDaClassificacao());

        Result resultado = await ExecutarComOsCadastros(processo, etapa);

        Recusas(resultado).Should().Equal((
            "etapas[0].recursos[0].procedimentos[0].tipoDeDecisao", CodigosDoProcedimentoDeRecurso.DaEtapa.ParaDecisaoNaoDeclarada));
    }

    [Fact(DisplayName = "Tipo inválido com procedimento do mesmo tipo e regra desconhecida recusa o tipo e a regra, sem dizer que o tipo não é declarado")]
    public async Task Handle_TipoInvalidoEProcedimentoComRegraDesconhecida_RecusaOTipoEARegra()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcessoInput etapa = ComProdutosDoRecurso(
            Etapa(1, tipos: ["CLASIFICACAO"]), ProcedimentoDaClassificacao(regra: "PROCEDIMENTO-INEXISTENTE"));

        Result resultado = await ExecutarComOsCadastros(processo, etapa);

        Recusas(resultado).Should().Equal(
            ("etapas[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("etapas[0].recursos[0].procedimentos[0].regra", CodigosDoProcedimentoDeRecurso.DaEtapa.Desconhecido));
    }

    [Fact(DisplayName = "Produto recusado com recurso ancorado nele recusa o produto, a janela e os tipos, sem recusa de recurso")]
    public async Task Handle_ProdutoRecusadoComRecursoAncorado_NaoJulgaORecurso()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcessoInput etapa = ComProdutosDoRecurso(Etapa(1, tipos: [TipoForaDoVocabulario]), ProcedimentoDaClassificacao()) with
        {
            Produtos = [new ProdutoDaEtapaInput(AtoDaClassificacao, "PAPEL_INEXISTENTE")],
            Inicio = Inicio,
            Fim = FimAntesDoInicio,
        };

        Result resultado = await ExecutarComOsCadastros(processo, etapa);

        Recusas(resultado).Select(static r => r.Campo).Should().Equal("etapas[0].produtos[0].papel", "etapas[0].fim", "etapas[0].tiposDeDecisao[0]");
    }

    [Fact(DisplayName = "O id de etapa repetido vence os tipos inválidos")]
    public async Task Handle_IdRepetidoComTiposInvalidos_RecusaOIdRepetido()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        Guid id = Guid.CreateVersion7();

        Result resultado = await Executar(
            Comando(processo, Etapa(1, id: id, tipos: [null]), Etapa(2, id: id)), RepositorioDe(processo), Substitute.For<IUnitOfWork>());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
    }

    [Fact(DisplayName = "A recusa dos tipos não muda o processo nem grava, e o rastreamento é descartado")]
    public async Task Handle_TiposInvalidos_NaoMutaNemGravaEDescarta()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [TipoForaDoVocabulario])), RepositorioDe(processo), unitOfWork);

        resultado.IsFailure.Should().BeTrue();
        processo.Etapas.Should().BeEmpty();
        await unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Recusa de etapa depois de declarar os tipos descarta as alterações e não grava")]
    public async Task Handle_RecusaPosteriorADeclaracaoDosTipos_DescartaAsAlteracoes()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoAtoPublicadoReader catalogoSemAtos = Substitute.For<ITipoAtoPublicadoReader>();
        EtapaProcessoInput etapa = Etapa(1, tipos: [Classificacao]) with { Produtos = [new ProdutoDaEtapaInput("ATO_INEXISTENTE", null)] };

        Result resultado = await Executar(
            Comando(processo, etapa), RepositorioDe(processo), unitOfWork, cadastros: new(Atos: catalogoSemAtos));

        resultado.Error!.Code.Should().Be("ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo");
        await unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Processo inexistente vence os tipos inválidos")]
    public async Task Handle_ProcessoInexistenteComTiposInvalidos_RecusaPeloProcesso()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProcessoSeletivo?)null);

        Result resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [TipoForaDoVocabulario])), repository, Substitute.For<IUnitOfWork>());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Processo publicado vence os tipos inválidos")]
    public async Task Handle_ProcessoPublicadoComTiposInvalidos_RecusaPelaPublicacao()
    {
        (ProcessoSeletivo processo, _) = ProcessoPublicado();

        Result resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [TipoForaDoVocabulario])), RepositorioDe(processo), Substitute.For<IUnitOfWork>());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }
}
