namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

public sealed class DefinirManifestacaoDeInteresseCommandHandlerTests
{
    [Theory(DisplayName = "Com um dos dois códigos, o handler grava a manifestação e salva")]
    [InlineData("UMA_VEZ", ManifestacaoDeInteresse.UmaVez)]
    [InlineData("CONFIRMA_A_CADA_CHAMADA", ManifestacaoDeInteresse.ConfirmaACadaChamada)]
    public async Task Handle_CodigoValido_GravaESalva(string codigo, ManifestacaoDeInteresse esperada)
    {
        ProcessoSeletivo processo = PublicacaoDeTeste.NovoProcessoConforme();
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir(codigo);

        resultado.IsSuccess.Should().BeTrue();
        processo.ManifestacaoDeInteresse.Should().Be(esperada);
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem código, o handler volta o processo à manifestação não prevista e salva")]
    public async Task Handle_SemCodigo_LimpaESalva()
    {
        ProcessoSeletivo processo = PublicacaoDeTeste.NovoProcessoConforme();
        processo.DefinirManifestacaoDeInteresse("UMA_VEZ");
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir(null);

        resultado.IsSuccess.Should().BeTrue();
        processo.ManifestacaoDeInteresse.Should().BeNull();
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O código desconhecido devolve a recusa no campo e não salva")]
    public async Task Handle_CodigoDesconhecido_RecusaSemSalvar()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());

        Result resultado = await contexto.Definir("OUTRA");

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("manifestacaoDeInteresse", ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido));
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo inexistente devolve não encontrado, sem salvar")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Contexto contexto = new(null);

        Result resultado = await contexto.Definir("UMA_VEZ");

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo publicado recusa a edição, sem salvar")]
    public async Task Handle_ProcessoPublicado_RecusaSemSalvar()
    {
        Contexto contexto = new(ApoioDaCascataDeRemanejamento.NovoProcessoPublicado());

        Result resultado = await contexto.Definir("UMA_VEZ");

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O conflito de concorrência descarta o rastreamento e devolve o conflito, sem relançar")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());
        contexto.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("xmin"));

        Result resultado = await contexto.Definir("UMA_VEZ");

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConflitoDeConcorrencia");
        contexto.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private sealed class Contexto
    {
        private readonly Guid _id;
        private readonly IProcessoSeletivoRepository _repositorio = Substitute.For<IProcessoSeletivoRepository>();

        public Contexto(ProcessoSeletivo? processo)
        {
            _id = processo?.Id ?? Guid.NewGuid();
            _repositorio.ObterParaMutacaoAsync(_id, Arg.Any<CancellationToken>()).Returns(processo);
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public Task<Result> Definir(string? codigo) =>
            DefinirManifestacaoDeInteresseCommandHandler.Handle(new(_id, codigo), _repositorio, UnitOfWork, CancellationToken.None);
    }
}
