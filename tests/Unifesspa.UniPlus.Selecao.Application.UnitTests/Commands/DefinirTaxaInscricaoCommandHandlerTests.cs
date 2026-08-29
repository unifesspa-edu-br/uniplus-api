namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirTaxaInscricaoCommandHandlerTests
{
    private sealed record Mocks(IProcessoSeletivoRepository Repository, ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<ISelecaoUnitOfWork>());
    }

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar(
            "PS 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirTaxaInscricaoCommand command = new(Guid.CreateVersion7(), true, 100m, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirTaxaInscricaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com Cobra nulo remove a declaração e persiste — processo volta a \"não declarado\" (CA-01)")]
    public async Task Handle_CobraNulo_RemoveDeclaracao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(
                cobra: true, valor: 100m, fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico]).Value!,
            PrecondicaoIfMatch.Ausente);

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirTaxaInscricaoCommand command = new(processo.Id, null, null, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirTaxaInscricaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoTaxaInscricao.Should().BeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Cobra=true, valor válido e fundamento define a configuração e persiste")]
    public async Task Handle_CobraComValorValido_DefineConfiguracao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirTaxaInscricaoCommand command = new(
            processo.Id, true, 150.00m, [FundamentoIsencaoCodigo.CadastroUnico], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirTaxaInscricaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoTaxaInscricao.Should().NotBeNull();
        processo.ConfiguracaoTaxaInscricao!.Cobra.Should().BeTrue();
        processo.ConfiguracaoTaxaInscricao.Valor.Should().Be(150.00m);
        processo.ConfiguracaoTaxaInscricao.Fundamentos.Should().Equal(FundamentoIsencao.CadastroUnico);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Cobra=true e sem valor propaga ValorObrigatorioQuandoCobra e NÃO persiste (CA-02)")]
    public async Task Handle_CobraSemValor_PropagaErroENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirTaxaInscricaoCommand command = new(
            processo.Id, true, null, [FundamentoIsencaoCodigo.CadastroUnico], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirTaxaInscricaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra");
        processo.ConfiguracaoTaxaInscricao.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Cobra=true e nenhum fundamento propaga FundamentoObrigatorioQuandoCobra e NÃO persiste (issue #1310)")]
    public async Task Handle_CobraSemFundamento_PropagaErroENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirTaxaInscricaoCommand command = new(
            processo.Id, true, 100m, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirTaxaInscricaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra");
        processo.ConfiguracaoTaxaInscricao.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Processo mínimo conforme (<see cref="ProcessoSeletivoConformeBuilder"/>, já com taxa
    /// declarada como <c>cobra:false</c>) e efetivamente publicado — exercita o bloqueio de
    /// mutação pós-publicação (CA-09) no handler.
    /// </summary>
    private static ProcessoSeletivo NovoProcessoPublicado()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Publicado");

        DadosEdital dados = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        byte[] bytesCanonicos = System.Text.Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
        processo.Publicar(
            dados, bytesCanonicos, "1.0", "canonical-json/sha256@v1", ProcessoSeletivoConformeBuilder.HashFixo,
            "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "Handle com processo já publicado propaga MutacaoPosPublicacaoBloqueada e NÃO persiste (CA-09)")]
    public async Task Handle_ProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        ConfiguracaoTaxaInscricao? antes = processo.ConfiguracaoTaxaInscricao;
        DefinirTaxaInscricaoCommand command = new(processo.Id, true, 100m, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirTaxaInscricaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.ConfiguracaoTaxaInscricao.Should().BeSameAs(antes);
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
