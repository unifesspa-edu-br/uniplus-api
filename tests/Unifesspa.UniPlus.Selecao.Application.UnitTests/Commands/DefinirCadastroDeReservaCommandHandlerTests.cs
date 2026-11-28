namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

public sealed class DefinirCadastroDeReservaCommandHandlerTests
{
    [Fact(DisplayName = "Com as chamadas válidas, o handler grava em ordem crescente e salva")]
    public async Task Handle_ChamadasValidas_GravaESalva()
    {
        ProcessoSeletivo processo = PublicacaoDeTeste.NovoProcessoConforme();
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir([new(2, 1, "ANTECIPA_A_HABILITACAO"), new(1, 0, null)]);

        resultado.IsSuccess.Should().BeTrue();
        processo.CadastroDeReserva.Chamadas.Select(static c => (c.Chamada.Valor, c.PessoasDaReservaPorVaga, c.Participacao))
            .Should().Equal((1, 0, null), (2, 1, ParticipacaoDaReserva.AntecipaAHabilitacao));
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A lista vazia limpa o cadastro anterior e salva")]
    public async Task Handle_ListaVazia_LimpaESalva()
    {
        ProcessoSeletivo processo = PublicacaoDeTeste.NovoProcessoConforme();
        processo.DefinirCadastroDeReserva([new(1, 2, "ASSUME_NA_MESMA_CHAMADA")]);
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir([]);

        resultado.IsSuccess.Should().BeTrue();
        processo.CadastroDeReserva.Should().Be(CadastroDeReservaPorChamada.SemReserva);
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa do domínio devolve os erros por campo e não salva")]
    public async Task Handle_ChamadaInvalida_RecusaSemSalvar()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());

        Result resultado = await contexto.Definir([new(1, 2, "ASSUME_NA_MESMA_CHAMADA"), new(1, -1, null)]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("cadastroDeReserva[1].numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("cadastroDeReserva[1].pessoasDaReservaPorVaga", CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida));
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A chamada com reserva sem participação e a chamada sem reserva com participação são recusadas no campo da participação, sem salvar")]
    public async Task Handle_ParticipacaoIncoerenteComAQuantidade_RecusaSemSalvar()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());

        Result resultado = await contexto.Definir([new(1, 2, null), new(2, 0, "ASSUME_NA_MESMA_CHAMADA"), new(3, 1, "OUTRA")]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("cadastroDeReserva[0].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoAusente),
            ("cadastroDeReserva[1].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoSemReserva),
            ("cadastroDeReserva[2].participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoDesconhecida));
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O item nulo é recusado pelo domínio, sem salvar")]
    public async Task Handle_ItemNulo_RecusaPeloDominioSemSalvar()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());

        Result resultado = await contexto.Definir([null]);

        resultado.Errors.Should().Contain(e => e.Field == "cadastroDeReserva[0].numeroDaChamada");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo inexistente devolve não encontrado, sem salvar")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Contexto contexto = new(null);

        Result resultado = await contexto.Definir([]);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo publicado recusa a edição, sem salvar")]
    public async Task Handle_ProcessoPublicado_RecusaSemSalvar()
    {
        Contexto contexto = new(ApoioDaCascataDeRemanejamento.NovoProcessoPublicado());

        Result resultado = await contexto.Definir([new(1, 2, "ASSUME_NA_MESMA_CHAMADA")]);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lista de chamadas ausente é recusada como obrigatória, sem salvar; lista vazia declara nenhuma")]
    public async Task Handle_ListaAusente_RecusaComoObrigatorio()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());

        Result ausente = await contexto.Definir(null);
        Result vazia = await contexto.Definir([]);

        ausente.Errors.Should().ContainSingle(e => e.Field == "cadastroDeReserva" && e.Error.Code == "uniplus.validacao.obrigatorio");
        vazia.IsSuccess.Should().BeTrue();
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O conflito de concorrência descarta o rastreamento e devolve o conflito, sem relançar")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());
        contexto.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("xmin"));

        Result resultado = await contexto.Definir([new(1, 2, "ASSUME_NA_MESMA_CHAMADA")]);

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

        public Task<Result> Definir(IReadOnlyList<CadastroDeReservaInformado?>? chamadas) =>
            DefinirCadastroDeReservaCommandHandler.Handle(new(_id, chamadas), _repositorio, UnitOfWork, CancellationToken.None);
    }
}
