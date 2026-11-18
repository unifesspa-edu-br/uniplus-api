namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
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
        classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static IUserContext Usuario(string contaId)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(Dono.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    private static async Task<(Result Resultado, Inscricao Inscricao, IUnitOfWork UnitOfWork)> Cancelar(
        Func<Guid, Inscricao> inscricao, DateTimeOffset agora, string contaId = "conta-1", string? motivo = null)
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao) = SalvarDeTeste.MontarVersaoComItem(Item);
        Inscricao existente = inscricao(versao.Id);
        IInscricaoRepository repositorio = Substitute.For<IInscricaoRepository>();
        repositorio.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns(existente);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await CancelarInscricaoCommandHandler.Handle(
            new CancelarInscricaoCommand(ProcessoId, motivo, PrecondicaoIfMatch.DeTags([existente.ETag])),
            new InscricaoDoDono(repositorio, Usuario(contaId)),
            new CancelamentoDaInscricaoPeloTitular(Periodo(versao, configuracao), repositorio, Usuario(contaId), new RelogioFixo(agora)),
            unitOfWork, CancellationToken.None);
        return (resultado, existente, unitOfWork);
    }

    private static PeriodoDaInscricaoResolver Periodo(VersaoConfiguracao versao, ConfiguracaoPublicada configuracao)
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, configuracao);
        return new PeriodoDaInscricaoResolver(DivulgacaoDeTeste.Certame(ProcessoId), processoRepository, registroCodecs, DivulgacaoDeTeste.Fuso());
    }

    [Fact(DisplayName = "Cancelar a inscrição enviada, com as inscrições abertas, grava o cancelamento com o ator e o motivo")]
    public async Task Handle_EnviadaNoPeriodo_Cancela()
    {
        (Result resultado, Inscricao inscricao, IUnitOfWork unitOfWork) = await Cancelar(
            DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada, DivulgacaoDeTeste.DentroDoPeriodo, motivo: "Desisti");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Cancelada);
        inscricao.Estado.Cancelamento?.CanceladaPor.Should().Be("conta-1");
        inscricao.Estado.Cancelamento?.CanceladaEm.Should().Be(DivulgacaoDeTeste.DentroDoPeriodo);
        inscricao.Estado.Cancelamento?.MotivoDoCancelamento.Should().Be("Desisti");
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fora do período, cancelar é recusado e a inscrição segue enviada")]
    public async Task Handle_ForaDoPeriodo_Recusa()
    {
        (Result resultado, Inscricao inscricao, IUnitOfWork unitOfWork) = await Cancelar(
            DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada, DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Submetida);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fora do período, a recusa do rascunho é a do período: orientar a descartar levaria a outra recusa")]
    public async Task Handle_RascunhoForaDoPeriodo_RecusaPeloPeriodo()
    {
        (Result resultado, _, _) = await Cancelar(
            static versao => Inscricao.Iniciar(ProcessoId, Dono, "conta-1", "https://issuer", versao).Value!,
            DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "O rascunho não se cancela: a recusa orienta a descartar")]
    public async Task Handle_Rascunho_RecusaOrientandoDescartar()
    {
        (Result resultado, _, IUnitOfWork unitOfWork) = await Cancelar(
            static versao => Inscricao.Iniciar(ProcessoId, Dono, "conta-1", "https://issuer", versao).Value!,
            DivulgacaoDeTeste.DentroDoPeriodo);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.CancelamentoSoDeEnviada);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição de outro candidato não é cancelada")]
    public async Task Handle_DeOutroCandidato_Recusa()
    {
        (Result resultado, Inscricao inscricao, _) = await Cancelar(
            DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada, DivulgacaoDeTeste.DentroDoPeriodo, contaId: "conta-outro");

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.DonoDivergente);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Submetida);
    }
}
