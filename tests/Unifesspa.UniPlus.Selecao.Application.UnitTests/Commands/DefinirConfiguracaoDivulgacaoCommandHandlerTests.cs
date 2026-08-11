namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirConfiguracaoDivulgacaoCommandHandlerTests
{
    private sealed record Mocks(IProcessoSeletivoRepository Repository, ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<ISelecaoUnitOfWork>());
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirConfiguracaoDivulgacaoCommand command = new(Guid.CreateVersion7(), null, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com CamposPublicos nulo remove a configuração explícita e persiste (toggle por ausência)")]
    public async Task Handle_CamposPublicosNulo_RemoveConfiguracaoEPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(processo.Id, null, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao.Should().BeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com campos válidos define a configuração e persiste")]
    public async Task Handle_CamposValidos_DefineConfiguracaoEPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(
            processo.Id, ["numero_inscricao", "nome"], "Ampliação para dar transparência ao resultado.", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao.Should().NotBeNull();
        processo.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("nome", "numero_inscricao");
        processo.ConfiguracaoDivulgacao.Justificativa.Should().Be("Ampliação para dar transparência ao resultado.");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com campo fora do vocabulário propaga o erro do domínio e NÃO persiste")]
    public async Task Handle_CampoInvalido_PropagaErroDoDominioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(processo.Id, ["numero_inscricao", "cpf"], null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDivulgacao.CampoNaoPermitido");
        processo.ConfiguracaoDivulgacao.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle substitui integralmente uma configuração já existente")]
    public async Task Handle_SubstituiConfiguracaoExistente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(processo.Id, ["numero_inscricao"], null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("numero_inscricao");
    }

    /// <summary>
    /// Monta um processo minimamente conforme e já publicado (mesmo par exigido pelo gate de
    /// conformidade de <see cref="ProcessoSeletivo.Publicar"/>) — replicado aqui, mesmo molde
    /// de <see cref="DefinirBonusRegionalCommandHandlerTests.NovoProcessoPublicado"/>, para
    /// exercitar o bloqueio de mutação pós-publicação SEM sessão editorial (CA-06).
    /// </summary>
    private static ProcessoSeletivo NovoProcessoPublicado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        string hashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", hashFixo).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: regraDistribuicao,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ReferenciaRegra regraCalculo = ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", hashFixo).Value!;
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", hashFixo).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: regraCalculo,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: regraOrdemAlocacao,
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseConforme = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        byte[] bytesCanonicos = System.Text.Encoding.UTF8.GetBytes(
            new System.Text.Json.Nodes.JsonObject { ["status"] = "ok" }.ToJsonString());
        processo.Publicar(dados, bytesCanonicos, "1.0", "canonical-json/sha256@v1", hashFixo, "user-sub-123", TimeProvider.System)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "Handle com processo já publicado (SEM sessão editorial) propaga MutacaoPosPublicacaoBloqueada e NÃO persiste (CA-06)")]
    public async Task Handle_ProcessoPublicadoSemSessao_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(
            processo.Id, ["numero_inscricao", "nome_abreviado"], null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.ConfiguracaoDivulgacao.Should().BeNull("a mutação bloqueada não pode ter tocado a configuração viva");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
