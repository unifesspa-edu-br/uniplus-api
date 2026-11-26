namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que o servidor faz com o par <c>(código, versão)</c> declarado: resolve no rol de
/// regras e congela a identidade completa (UNI-REQ-0112).
/// </summary>
public sealed class DefinirAlgoritmoContagemPrazoCommandHandlerTests
{
    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader Catalogo,
        IUnitOfWork UnitOfWork);

    private static JsonElement Json(string bruto) => JsonDocument.Parse(bruto).RootElement.Clone();

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static RegraCatalogo RegraDoCatalogo(
        string codigo = "CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL",
        TipoRegra tipo = TipoRegra.ConvencaoContagemPrazo)
    {
        return RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal");
    }

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId, RegraCatalogo? regra)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        IRegraCatalogoReader catalogo = Substitute.For<IRegraCatalogoReader>();
        catalogo.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(regra);

        return new Mocks(repository, catalogo, Substitute.For<IUnitOfWork>());
    }

    [Fact(DisplayName = "O servidor resolve a identidade completa: declarados código e versão, congela também o hash do catálogo")]
    public async Task Handle_ResolveIdentidadeCompleta()
    {
        ProcessoSeletivo processo = NovoProcesso();
        RegraCatalogo regra = RegraDoCatalogo();
        Mocks mocks = NovosMocks(processo, processo.Id, regra);
        DefinirAlgoritmoContagemPrazoCommand command = new(
            processo.Id, ConvencaoDeContagemDoPrazo.ExcluiDiaInicial.Codigo(), "v1");

        Result result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        ConferirIdentidade(processo, regra);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static void ConferirIdentidade(ProcessoSeletivo processo, RegraCatalogo regra)
    {
        processo.AlgoritmoContagemPrazo.Should().NotBeNull();
        processo.AlgoritmoContagemPrazo!.Codigo.Should().Be(ConvencaoDeContagemDoPrazo.ExcluiDiaInicial.Codigo());
        processo.AlgoritmoContagemPrazo.Versao.Should().Be("v1");
        processo.AlgoritmoContagemPrazo.Hash.Should().Be(regra.Hash,
            "o hash vem do catálogo resolvido, e é ele que prova qual definição foi aplicada");
    }

    [Fact(DisplayName = "Par que não existe no rol de regras é recusado com causa própria, distinta de não declarar")]
    public async Task Handle_ParInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id, regra: null);
        DefinirAlgoritmoContagemPrazoCommand command = new(
            processo.Id, "CONTAGEM-PRAZO-INVENTADA", "v9");

        Result result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado");
        result.Error.Message.Should().NotContain("CONTAGEM-PRAZO-INVENTADA").And.NotContain("v9");
        processo.AlgoritmoContagemPrazo.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Entrada de OUTRO tipo de regra é recusada — o rol tem muitos assuntos, e só uma conta prazo")]
    public async Task Handle_TipoErrado_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(
            processo, processo.Id, RegraDoCatalogo(codigo: "FADA_MULTIPLICATIVO", tipo: TipoRegra.RegraFada));
        DefinirAlgoritmoContagemPrazoCommand command = new(
            processo.Id, "FADA_MULTIPLICATIVO", "v1");

        Result result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado");
        result.Error.Message.Should().NotContain("FADA_MULTIPLICATIVO");
    }

    [Theory(DisplayName = "Par ausente ou pela metade recusa antes de consultar o catálogo — meio par não aponta entrada nenhuma")]
    [InlineData(null, null)]
    [InlineData("CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL", null)]
    [InlineData(null, "v1")]
    [InlineData("CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL", "   ")]
    public async Task Handle_ParAusenteOuIncompleto_RecusaSemConsultarCatalogo(string? codigo, string? versao)
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id, RegraDoCatalogo());
        DefinirAlgoritmoContagemPrazoCommand command = new(processo.Id, codigo, versao);

        Result result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
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
            Guid.CreateVersion7(), ConvencaoDeContagemDoPrazo.ExcluiDiaInicial.Codigo(), "v1");

        Result result = await DefinirAlgoritmoContagemPrazoCommandHandler.Handle(
            command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await ConferirQueNadaFoiConsultadoNemGravadoAsync(mocks);
    }

    [Fact(DisplayName = "Processo inexistente é recusado antes do algoritmo não declarado, sem consultar o catálogo nem gravar")]
    public async Task Handle_ProcessoInexistenteComAlgoritmoNaoDeclarado_RecusaPeloProcesso()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7(), RegraDoCatalogo());

        Result result = await DefinirAsync(mocks, new DefinirAlgoritmoContagemPrazoCommand(Guid.CreateVersion7(), null, null));

        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await ConferirQueNadaFoiConsultadoNemGravadoAsync(mocks);
    }

    [Fact(DisplayName = "Processo publicado é recusado antes do algoritmo não declarado, sem consultar o catálogo nem gravar")]
    public async Task Handle_ProcessoPublicadoComAlgoritmoNaoDeclarado_RecusaPelaSituacao()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id, RegraDoCatalogo());

        Result result = await DefinirAsync(mocks, new DefinirAlgoritmoContagemPrazoCommand(processo.Id, null, null));

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await ConferirQueNadaFoiConsultadoNemGravadoAsync(mocks);
    }

    private static Task<Result> DefinirAsync(Mocks mocks, DefinirAlgoritmoContagemPrazoCommand command) =>
        DefinirAlgoritmoContagemPrazoCommandHandler.Handle(command, mocks.Repository, mocks.Catalogo, mocks.UnitOfWork, CancellationToken.None);

    private static async Task ConferirQueNadaFoiConsultadoNemGravadoAsync(Mocks mocks)
    {
        await mocks.Catalogo.DidNotReceive().ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
