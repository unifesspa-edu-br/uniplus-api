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
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
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

    private static readonly FatoColetado Item = FatoColetadoDeTeste.Coletando("APELIDO", 0).ComRotulo("Apelido").DeTexto("LIVRE").Construir();

    private static Inscricao NovoRascunho(Guid versaoFormularioId) =>
        Inscricao.Iniciar(ProcessoId, Dono, "conta-1", "https://issuer", versaoFormularioId).Value!;

    private static IUserContext Usuario(string contaId)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(Dono.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    private sealed class CenarioDeDescarte(VersaoConfiguracao fixada, VersaoConfiguracao divulgada, ConfiguracaoPublicada configuracao)
    {
        public IInscricaoRepository Inscricoes { get; } = Substitute.For<IInscricaoRepository>();
        public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();
        public IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public async Task<(Result Resultado, IInscricaoRepository Repositorio, IUnitOfWork UnitOfWork)> Descartar(
            Func<Inscricao, string> precondicao, Inscricao rascunho, string contaId = "conta-1",
            DateTimeOffset? agora = null, IDocumentoDaInscricaoRepository? documentos = null)
        {
            Inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns(rascunho);
            DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(Processos, Codecs, configuracao,
                fixada: (fixada, DivulgacaoDeTeste.FimDoPeriodo), divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo));
            Result resultado = await DescartarRascunhoDaInscricaoCommandHandler.Handle(
                new DescartarRascunhoDaInscricaoCommand(ProcessoId, PrecondicaoIfMatch.DeTags([precondicao(rascunho)])),
                new InscricaoDoDono(Inscricoes, Usuario(contaId)),
                CriarDescarte(documentos, agora), UnitOfWork, CancellationToken.None);
            return (resultado, Inscricoes, UnitOfWork);
        }

        private DescarteDaInscricao CriarDescarte(IDocumentoDaInscricaoRepository? documentos, DateTimeOffset? agora) =>
            new(new PeriodoDaInscricaoResolver(DivulgacaoDeTeste.Certame(ProcessoId), Processos, Codecs, DivulgacaoDeTeste.Fuso()),
                new VersaoDaInscricaoResolver(DivulgacaoDeTeste.Certame(ProcessoId), Processos, Codecs),
                Inscricoes, documentos ?? Substitute.For<IDocumentoDaInscricaoRepository>(),
                new RelogioFixo(agora ?? DivulgacaoDeTeste.DentroDoPeriodo));
    }

    private static Task<(Result Resultado, IInscricaoRepository Repositorio, IUnitOfWork UnitOfWork)> DescartarNaVersaoDivulgada(
        Func<Inscricao, string> precondicao, Func<Guid, Inscricao>? rascunho = null, string contaId = "conta-1", DateTimeOffset? agora = null,
        IDocumentoDaInscricaoRepository? documentos = null)
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao) = SalvarDeTeste.MontarVersaoComItem(Item);
        return new CenarioDeDescarte(versao, versao, configuracao).Descartar(precondicao, (rascunho ?? NovoRascunho)(versao.Id), contaId, agora, documentos);
    }

    [Fact(DisplayName = "Descartar o próprio rascunho com o If-Match corrente remove a inscrição e grava")]
    public async Task Handle_RascunhoDoDono_Remove()
    {
        (Result resultado, IInscricaoRepository repositorio, IUnitOfWork unitOfWork) = await DescartarNaVersaoDivulgada(static r => r.ETag);

        resultado.IsSuccess.Should().BeTrue();
        await repositorio.Received(1).RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Descartar o rascunho não agenda a chave de envio, que o bucket expira sozinho, nem a definitiva do arquivo ainda pendente")]
    public async Task Handle_RascunhoComArquivos_ApagaOsObjetos()
    {
        IDocumentoDaInscricaoRepository documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
        DocumentoExigido laudo = new DocumentoExigidoBuilder().ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO", "Laudo", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, null, [])).Construir();
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
        (Result resultado, IInscricaoRepository repositorio, IUnitOfWork unitOfWork) = await DescartarNaVersaoDivulgada(
            static r => r.ETag, rascunho: Enviada);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.DescarteSoDeRascunho);
        await repositorio.DidNotReceive().RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    internal static Inscricao Enviada(Guid versaoFormularioId) =>
        InscricaoEnviadaDeTeste.Enviar(NovoRascunho(versaoFormularioId));

    [Fact(DisplayName = "Rascunho de outro candidato é recusado e não é removido")]
    public async Task Handle_RascunhoDeOutroCandidato_Recusa()
    {
        (Result resultado, IInscricaoRepository repositorio, _) = await DescartarNaVersaoDivulgada(static r => r.ETag, contaId: "conta-outro");

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
        (VersaoConfiguracao fixada, ConfiguracaoPublicada configuracao) = SalvarDeTeste.MontarVersaoComItem(Item);
        (VersaoConfiguracao divulgada, _) = SalvarDeTeste.MontarVersaoComItem(Item);

        (Result resultado, IInscricaoRepository repositorio, _) = await new CenarioDeDescarte(fixada, divulgada, configuracao).Descartar(
            r => DivulgacaoDeTeste.ETagDepoisDeAdotar(r, divulgada), NovoRascunho(fixada.Id));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await repositorio.Received(1).RemoverAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
    }
}
