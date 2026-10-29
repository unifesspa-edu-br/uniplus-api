namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

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
/// Descartar o rascunho remove a inscrição e as linhas filhas; a enviada, a de outro candidato e o
/// <c>If-Match</c> vencido nunca chegam à remoção.
/// </summary>
public sealed class DescartarRascunhoDaInscricaoCommandHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Cpf Dono = PersonasDeTeste.KevinCarvalho;

    private static readonly FatoColetado Item = FatoColetado.Criar(
        "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
        classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!;

    private static Inscricao NovoRascunho(Guid versaoFormularioId) =>
        Inscricao.Iniciar(ProcessoId, Dono, "sub-1", "https://issuer", versaoFormularioId).Value!;

    private static IUserContext Usuario(string sub)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(Dono.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    /// <summary>A divulgação aponta para <paramref name="divulgada"/>; o rascunho nasce na versão <paramref name="fixada"/>, que é a mesma quando a retificação não mudou nada.</summary>
    private static async Task<(Result Resultado, IInscricaoRepository Repositorio, ISelecaoUnitOfWork UnitOfWork)> Descartar(
        Func<Inscricao, string> precondicao, Inscricao rascunho, VersaoConfiguracao fixada, VersaoConfiguracao divulgada, GrafoConfiguracao grafo, string sub = "sub-1", DateTimeOffset? agora = null,
        IDocumentoDaInscricaoRepository? documentos = null)
    {
        IInscricaoRepository repositorio = Substitute.For<IInscricaoRepository>();
        repositorio.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns(rascunho);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, grafo,
            fixada: (fixada, DivulgacaoDeTeste.FimDoPeriodo),
            divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo));
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result resultado = await DescartarRascunhoDaInscricaoCommandHandler.Handle(
            new DescartarRascunhoDaInscricaoCommand(ProcessoId, PrecondicaoIfMatch.DeTags([precondicao(rascunho)])),
            repositorio, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), new RelogioFixo(agora ?? DivulgacaoDeTeste.DentroDoPeriodo), unitOfWork, Usuario(sub),
            documentos ?? Substitute.For<IDocumentoDaInscricaoRepository>(), CancellationToken.None);
        return (resultado, repositorio, unitOfWork);
    }

    private static Task<(Result Resultado, IInscricaoRepository Repositorio, ISelecaoUnitOfWork UnitOfWork)> DescartarNaVersaoDivulgada(
        Func<Inscricao, string> precondicao, Func<Guid, Inscricao>? rascunho = null, string sub = "sub-1", DateTimeOffset? agora = null,
        IDocumentoDaInscricaoRepository? documentos = null)
    {
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = SalvarInscricaoCommandHandlerTests.MontarVersaoComItem(Item);
        return Descartar(precondicao, (rascunho ?? NovoRascunho)(versao.Id), versao, versao, grafo, sub, agora, documentos);
    }

    [Fact(DisplayName = "Descartar o próprio rascunho com o If-Match corrente remove a inscrição e grava")]
    public async Task Handle_RascunhoDoDono_Remove()
    {
        (Result resultado, IInscricaoRepository repositorio, ISelecaoUnitOfWork unitOfWork) = await DescartarNaVersaoDivulgada(static r => r.ETag);

        resultado.IsSuccess.Should().BeTrue();
        await repositorio.Received(1).RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Descartar o rascunho não agenda a chave de envio, que o bucket expira sozinho, nem a definitiva do arquivo ainda pendente")]
    public async Task Handle_RascunhoComArquivos_ApagaOsObjetos()
    {
        IDocumentoDaInscricaoRepository documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Geral, false, null,
            [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), Guid.CreateVersion7(), laudo, "application/pdf", 10, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documentos.ListarArquivosDaInscricaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([arquivo]);

        (Result resultado, _, _) = await DescartarNaVersaoDivulgada(static r => r.ETag, documentos: documentos);

        resultado.IsSuccess.Should().BeTrue();
        await documentos.DidNotReceive().AgendarLimpezaAsync(arquivo.ObjectKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await documentos.DidNotReceive().AgendarLimpezaAsync(arquivo.ChaveDefinitivaPrevista, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com as inscrições encerradas, o rascunho não é descartado")]
    public async Task Handle_ForaDoPeriodo_Recusa()
    {
        (Result resultado, IInscricaoRepository repositorio, _) = await DescartarNaVersaoDivulgada(
            static r => r.ETag, agora: DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await repositorio.DidNotReceive().RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fora do período, a recusa da inscrição enviada é a do período: orientar a cancelar levaria a outra recusa")]
    public async Task Handle_EnviadaForaDoPeriodo_RecusaPeloPeriodo()
    {
        (Result resultado, _, _) = await DescartarNaVersaoDivulgada(
            static r => r.ETag, rascunho: Enviada, agora: DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "Inscrição enviada não é descartada: recusa orientando a cancelar")]
    public async Task Handle_InscricaoEnviada_Recusa()
    {
        (Result resultado, IInscricaoRepository repositorio, ISelecaoUnitOfWork unitOfWork) = await DescartarNaVersaoDivulgada(
            static r => r.ETag, rascunho: Enviada);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.DescarteSoDeRascunho);
        await repositorio.DidNotReceive().RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    internal static Inscricao Enviada(Guid versaoFormularioId)
    {
        Inscricao enviada = NovoRascunho(versaoFormularioId);
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        enviada.Submeter(
            "000001", null, null, semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), new HashSet<Guid>(), new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)), FormularioDeTeste.AvaliacaoSemOcultos,
            semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([enviada.ETag]), DateTimeOffset.UnixEpoch, []).IsSuccess.Should().BeTrue();

        return enviada;
    }

    [Fact(DisplayName = "Rascunho de outro candidato é recusado e não é removido")]
    public async Task Handle_RascunhoDeOutroCandidato_Recusa()
    {
        (Result resultado, IInscricaoRepository repositorio, _) = await DescartarNaVersaoDivulgada(static r => r.ETag, sub: "sub-outro");

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.DonoDivergente);
        await repositorio.DidNotReceive().RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "If-Match que não casa com o estado atual recusa e não remove")]
    public async Task Handle_IfMatchVencido_Recusa()
    {
        (Result resultado, IInscricaoRepository repositorio, _) = await DescartarNaVersaoDivulgada(static _ => "\"vencido\"");

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
        await repositorio.DidNotReceive().RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Rascunho em versão anterior à divulgada é descartado com o ETag que a leitura devolve")]
    public async Task Handle_RetificacaoDivulgadaDepoisDoRascunho_ConfereOETagDoRascunhoAdotado()
    {
        (VersaoConfiguracao fixada, GrafoConfiguracao grafo) = SalvarInscricaoCommandHandlerTests.MontarVersaoComItem(Item);
        (VersaoConfiguracao divulgada, _) = SalvarInscricaoCommandHandlerTests.MontarVersaoComItem(Item);

        (Result resultado, IInscricaoRepository repositorio, _) = await Descartar(
            r => DivulgacaoDeTeste.ETagDepoisDeAdotar(r, divulgada), NovoRascunho(fixada.Id), fixada, divulgada, grafo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await repositorio.Received(1).RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
    }
}
