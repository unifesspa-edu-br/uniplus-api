namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o servidor faz com o par <c>(código, versão)</c> declarado: resolve no rol de
/// regras e congela a identidade completa (UNI-REQ-0112).
/// </summary>
public sealed class DefinirAlgoritmoContagemPrazoCommandHandlerTests
{
    private const string HashDoCatalogo = "1e2d3c4b5a69788796a5b4c3d2e1f00112233445566778899aabbccddeeff001";

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader Catalogo,
        ISelecaoUnitOfWork UnitOfWork);

    private static JsonElement Json(string bruto) => JsonDocument.Parse(bruto).RootElement.Clone();

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static RegraCatalogo RegraDoCatalogo(
        string codigo = AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial,
        TipoRegra tipo = TipoRegra.AlgoritmoContagemPrazo)
    {
        RegraCatalogo regra = RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal").Value!;

        // O hash da entrada real é derivado da definição; aqui só precisa ser um valor
        // distinto e reconhecível, para que o teste consiga afirmar de ONDE ele veio.
        typeof(RegraCatalogo).GetProperty(nameof(RegraCatalogo.Hash))!.SetValue(regra, HashDoCatalogo);
        return regra;
    }

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId, RegraCatalogo? regra)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        IRegraCatalogoReader catalogo = Substitute.For<IRegraCatalogoReader>();
        catalogo.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(regra);

        return new Mocks(repository, catalogo, Substitute.For<ISelecaoUnitOfWork>());
    }

    [Fact(DisplayName = "O servidor resolve a identidade completa: declarados código e versão, congela também o hash do catálogo")]
    public async Task Handle_ResolveIdentidadeCompleta()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id, RegraDoCatalogo());
        DefinirAlgoritmoContagemPrazoCommand command = new(
            processo.Id, AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "v1", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.AlgoritmoContagemPrazo.Should().NotBeNull();
        processo.AlgoritmoContagemPrazo!.Codigo.Should().Be(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial);
        processo.AlgoritmoContagemPrazo.Versao.Should().Be("v1");
        processo.AlgoritmoContagemPrazo.Hash.Should().Be(HashDoCatalogo,
            "o hash vem do catálogo resolvido, e é ele que prova qual definição foi aplicada");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Par que não existe no rol de regras é recusado com causa própria, distinta de não declarar")]
    public async Task Handle_ParInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id, regra: null);
        DefinirAlgoritmoContagemPrazoCommand command = new(
            processo.Id, "CONTAGEM-PRAZO-INVENTADA", "v9", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado");
        processo.AlgoritmoContagemPrazo.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Entrada de OUTRO tipo de regra é recusada — o rol tem muitas famílias, e só uma conta prazo")]
    public async Task Handle_TipoErrado_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(
            processo, processo.Id, RegraDoCatalogo(codigo: "BONUS-MULTIPLICATIVO", tipo: TipoRegra.RegraBonus));
        DefinirAlgoritmoContagemPrazoCommand command = new(
            processo.Id, "BONUS-MULTIPLICATIVO", "v1", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado");
    }

    [Theory(DisplayName = "Par ausente ou pela metade recusa antes de consultar o catálogo — meio par não aponta entrada nenhuma")]
    [InlineData(null, null)]
    [InlineData(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, null)]
    [InlineData(null, "v1")]
    [InlineData(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "   ")]
    public async Task Handle_ParAusenteOuIncompleto_RecusaSemConsultarCatalogo(string? codigo, string? versao)
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id, RegraDoCatalogo());
        DefinirAlgoritmoContagemPrazoCommand command = new(processo.Id, codigo, versao, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
            "reportar 'não encontrado' diria a quem chamou que o par não existe no rol, quando o que houve foi um campo esquecido");
        await mocks.Catalogo.DidNotReceive().ObterAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7(), RegraDoCatalogo());
        DefinirAlgoritmoContagemPrazoCommand command = new(
            Guid.CreateVersion7(), AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "v1", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }
}
