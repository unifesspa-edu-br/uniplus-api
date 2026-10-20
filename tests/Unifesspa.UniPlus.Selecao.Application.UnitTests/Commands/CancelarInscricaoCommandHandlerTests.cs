namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cancelar a inscrição enviada só com as inscrições abertas; o rascunho e o período fechado nunca
/// chegam à gravação, e fora do período a recusa é a do período.
/// </summary>
public sealed class CancelarInscricaoCommandHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Cpf Dono = PersonasDeTeste.KevinCarvalho;

    private static readonly FatoColetado Item = FatoColetado.Criar(
        "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
        classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!;

    private static IUserContext Usuario(string sub)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(Dono.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    private static async Task<(Result Resultado, Inscricao Inscricao, ISelecaoUnitOfWork UnitOfWork)> Cancelar(
        Func<Guid, Inscricao> inscricao, DateTimeOffset agora, string sub = "sub-1", string? motivo = null)
    {
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = SalvarInscricaoCommandHandlerTests.MontarVersaoComItem(Item);
        Inscricao existente = inscricao(versao.Id);
        IInscricaoRepository repositorio = Substitute.For<IInscricaoRepository>();
        repositorio.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, grafo,
            fixada: (versao, DivulgacaoDeTeste.FimDoPeriodo),
            divulgada: (versao, DivulgacaoDeTeste.FimDoPeriodo));
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result resultado = await CancelarInscricaoCommandHandler.Handle(
            new CancelarInscricaoCommand(ProcessoId, motivo, PrecondicaoIfMatch.DeTags([existente.ETag])),
            repositorio, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(),
            unitOfWork, Usuario(sub), new RelogioFixo(agora), CancellationToken.None);
        return (resultado, existente, unitOfWork);
    }

    [Fact(DisplayName = "Cancelar a inscrição enviada, com as inscrições abertas, grava o cancelamento com o ator e o motivo")]
    public async Task Handle_EnviadaNoPeriodo_Cancela()
    {
        (Result resultado, Inscricao inscricao, ISelecaoUnitOfWork unitOfWork) = await Cancelar(
            DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada, DivulgacaoDeTeste.DentroDoPeriodo, motivo: "Desisti");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Cancelada);
        inscricao.CanceladaPor.Should().Be("sub-1");
        inscricao.CanceladaEm.Should().Be(DivulgacaoDeTeste.DentroDoPeriodo);
        inscricao.MotivoDoCancelamento.Should().Be("Desisti");
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fora do período, cancelar é recusado e a inscrição segue enviada")]
    public async Task Handle_ForaDoPeriodo_Recusa()
    {
        (Result resultado, Inscricao inscricao, ISelecaoUnitOfWork unitOfWork) = await Cancelar(
            DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada, DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Submetida);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fora do período, a recusa do rascunho é a do período: orientar a descartar levaria a outra recusa")]
    public async Task Handle_RascunhoForaDoPeriodo_RecusaPeloPeriodo()
    {
        (Result resultado, _, _) = await Cancelar(
            static versao => Inscricao.Iniciar(ProcessoId, Dono, "sub-1", "https://issuer", versao).Value!,
            DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "O rascunho não se cancela: a recusa orienta a descartar")]
    public async Task Handle_Rascunho_RecusaOrientandoDescartar()
    {
        (Result resultado, _, ISelecaoUnitOfWork unitOfWork) = await Cancelar(
            static versao => Inscricao.Iniciar(ProcessoId, Dono, "sub-1", "https://issuer", versao).Value!,
            DivulgacaoDeTeste.DentroDoPeriodo);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.CancelamentoSoDeEnviada);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição de outro candidato não é cancelada")]
    public async Task Handle_DeOutroCandidato_Recusa()
    {
        (Result resultado, Inscricao inscricao, _) = await Cancelar(
            DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada, DivulgacaoDeTeste.DentroDoPeriodo, sub: "sub-outro");

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.DonoDivergente);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Submetida);
    }
}
