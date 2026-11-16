namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// Os tipos de decisão de cada etapa são conferidos no laço do payload, junto com a forma de todas as etapas, antes de
/// qualquer cadastro: a recusa de uma etapa não esconde a das outras, e a gravação só declara os tipos depois disso.
/// </summary>
public sealed class DefinirEtapasTiposDeDecisaoTests
{
    private const string Classificacao = TipoMotivoDeDecisaoCodigo.Classificacao;
    private const string Isencao = TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa;
    private const string Heteroidentificacao = TipoMotivoDeDecisaoCodigo.Heteroidentificacao;
    private const string TipoForaDoVocabulario = "RECURSO_DA_BANCA";

    private static EtapaProcessoInput Etapa(int ordem, string nome = "Prova Objetiva", Guid? id = null, params string?[] tipos) => new(
        nome, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, ordem, id,
        Produtos: [], Bancas: [], Recursos: [], TiposDeDecisao: tipos);

    private static DefinirEtapasCommand Comando(ProcessoSeletivo processo, params EtapaProcessoInput[] etapas) =>
        new(processo.Id, etapas, PrecondicaoIfMatch.Ausente);

    private static IProcessoSeletivoRepository RepositorioDe(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repository;
    }

    private static (string? Campo, string Codigo)[] Recusas(Result<MutacaoAceita> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "A etapa declara os tipos de decisão e a leitura os devolve em ordem canônica, do comando ao DTO")]
    public async Task Handle_EtapaComTipos_GravaEDevolveEmOrdemCanonica()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [Isencao, Classificacao, Heteroidentificacao])), RepositorioDe(processo), unitOfWork);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        EtapaProcessoDto dto = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(processo.Etapas.Single());
        dto.Publicacoes.TiposDeDecisao.Should().Equal(Classificacao, Heteroidentificacao, Isencao);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A gravação seguinte sem tipos limpa a declaração da mesma etapa")]
    public async Task Handle_EtapaExistenteSemTipos_LimpaADeclaracao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = RepositorioDe(processo);
        (await Executar(Comando(processo, Etapa(1, tipos: [Classificacao])), repository, Substitute.For<IUnitOfWork>())).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = processo.Etapas.Single();

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, Etapa(1, id: etapa.Id)), repository, Substitute.For<IUnitOfWork>());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().ContainSingle().Which.Should().BeSameAs(etapa).And.Match<EtapaProcesso>(e => e.TiposDeDecisao.Count == 0);
    }

    [Fact(DisplayName = "Tipo inválido numa etapa e forma inválida em outra saem no mesmo lote, sem consultar o cadastro")]
    public async Task Handle_TipoInvalidoNumaEtapaEFormaInvalidaEmOutra_AcumulaNoMesmoLote()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [null]), Etapa(2, nome: "")),
            RepositorioDe(processo), Substitute.For<IUnitOfWork>(), tipoEtapaReader);

        Recusas(resultado).Should().Equal(
            ("etapas[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("etapas[1].nome", "EtapaProcesso.NomeObrigatorio"));
        tipoEtapaReader.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipos inválidos em várias etapas saem juntos, por posição de etapa e de tipo")]
    public async Task Handle_TiposInvalidosEmVariasEtapas_RecusaPorPosicao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result<MutacaoAceita> resultado = await Executar(
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

    [Fact(DisplayName = "O id de etapa repetido vence os tipos inválidos")]
    public async Task Handle_IdRepetidoComTiposInvalidos_RecusaOIdRepetido()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        Guid id = Guid.CreateVersion7();

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, Etapa(1, id: id, tipos: [null]), Etapa(2, id: id)), RepositorioDe(processo), Substitute.For<IUnitOfWork>());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
    }

    [Fact(DisplayName = "A recusa dos tipos não muda o processo, não grava e não descarta alteração alguma")]
    public async Task Handle_TiposInvalidos_NaoMutaNemGravaNemDescarta()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [TipoForaDoVocabulario])), RepositorioDe(processo), unitOfWork);

        resultado.IsFailure.Should().BeTrue();
        processo.Etapas.Should().BeEmpty();
        await unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
        unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Recusa de etapa depois de declarar os tipos descarta as alterações e não grava")]
    public async Task Handle_RecusaPosteriorADeclaracaoDosTipos_DescartaAsAlteracoes()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoAtoPublicadoReader catalogoSemAtos = Substitute.For<ITipoAtoPublicadoReader>();
        EtapaProcessoInput etapa = Etapa(1, tipos: [Classificacao]) with { Produtos = [new ProdutoDaEtapaInput("ATO_INEXISTENTE", null)] };

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, etapa), RepositorioDe(processo), unitOfWork, tipoAtoPublicadoReader: catalogoSemAtos);

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

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [TipoForaDoVocabulario])), repository, Substitute.For<IUnitOfWork>());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Precondição recusada vence os tipos inválidos")]
    public async Task Handle_PrecondicaoRecusadaComTiposInvalidos_RecusaPelaPrecondicao()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        AbrirRetificacao(processo, versaoAtual);

        Result<MutacaoAceita> resultado = await Executar(
            Comando(processo, Etapa(1, tipos: [TipoForaDoVocabulario])), RepositorioDe(processo), Substitute.For<IUnitOfWork>());

        resultado.Error!.Code.Should().Be("Precondicao.Requerida");
    }
}
