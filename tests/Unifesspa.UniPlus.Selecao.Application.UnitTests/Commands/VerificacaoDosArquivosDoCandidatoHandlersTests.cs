namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O lado da API na verificação dos arquivos do candidato: o primeiro resultado vale, o veredicto "não
/// definido" é erro e a reconciliação republica o que ficou sem resultado, uma instância por vez.
/// </summary>
public sealed class VerificacaoDosArquivosDoCandidatoHandlersTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly IDocumentoDaInscricaoRepository _documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

    public VerificacaoDosArquivosDoCandidatoHandlersTests() =>
        _documentos.TentarConcluirVerificacaoAsync(Arg.Any<Guid>(), Arg.Any<SituacaoDoArquivoDoCandidato>(), Arg.Any<CancellationToken>()).Returns(true);

    private static ArquivoDaApresentacao EmVerificacao(DateTimeOffset? confirmadoEm = null)
    {
        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Geral, false, null,
            [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), Guid.CreateVersion7(), laudo, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", new RelogioFixo(confirmadoEm ?? Agora)).IsSuccess.Should().BeTrue();
        return arquivo;
    }

    private Task Resultado(ResultadoDaVerificacaoDeArquivo resultado) =>
        RegistrarResultadoDaVerificacaoDoArquivoHandler.Handle(
            resultado, _documentos, _unitOfWork, new RelogioFixo(Agora), NullLogger.Instance, CancellationToken.None);

    private static ResultadoDaVerificacaoDeArquivo Verificado(Guid id) =>
        new(id, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, new string('b', 64), 2048, FormatoDoArquivo.Pdf);

    [Fact(DisplayName = "Resultado verificado grava o SHA-256, o tamanho, o formato e a chave definitiva")]
    public async Task Resultado_Verificado_GravaOQueOVerificadorConferiu()
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();
        _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);

        await Resultado(Verificado(arquivo.Id));

        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Verificado);
        arquivo.HashSha256.Should().Be(new string('b', 64));
        arquivo.TamanhoBytes.Should().Be(2048);
        arquivo.ChaveDefinitiva.Should().Be(arquivo.ChaveDefinitivaPrevista);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Segundo resultado para o mesmo arquivo: o primeiro prevalece e nada é gravado de novo")]
    public async Task Resultado_Repetido_PrimeiroPrevalece()
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();
        _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);
        await Resultado(Verificado(arquivo.Id));
        _unitOfWork.ClearReceivedCalls();

        await Resultado(new ResultadoDaVerificacaoDeArquivo(
            arquivo.Id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null));

        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Verificado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Verificado que perde a transição para uma recusa reaplica sobre o gravado e agenda a remoção do promovido")]
    public async Task Resultado_TransicaoPerdidaParaRecusa_AgendaARemocao()
    {
        ArquivoDaApresentacao lido = EmVerificacao();
        ArquivoDaApresentacao gravado = EmVerificacao();
        _ = gravado.RegistrarRecusa(MotivoDeRecusaDoArquivo.PdfComSenha);
        _documentos.ObterArquivoAsync(lido.Id, Arg.Any<CancellationToken>()).Returns(lido, gravado);
        _documentos.TentarConcluirVerificacaoAsync(lido.Id, Arg.Any<SituacaoDoArquivoDoCandidato>(), Arg.Any<CancellationToken>()).Returns(false);

        await Resultado(Verificado(lido.Id));

        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _documentos.Received(1).AgendarLimpezaAsync(ArquivoDaApresentacao.ChaveDefinitivaDe(lido.Id), Agora, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Verificado que perde a transição para outro verificado não grava nem remove o promovido")]
    public async Task Resultado_TransicaoPerdidaParaVerificado_NaoRemove()
    {
        ArquivoDaApresentacao lido = EmVerificacao();
        ArquivoDaApresentacao gravado = EmVerificacao();
        _ = gravado.RegistrarVerificado(2048, new string('b', 64), FormatoPermitido.Pdf);
        _documentos.ObterArquivoAsync(lido.Id, Arg.Any<CancellationToken>()).Returns(lido, gravado);
        _documentos.TentarConcluirVerificacaoAsync(lido.Id, Arg.Any<SituacaoDoArquivoDoCandidato>(), Arg.Any<CancellationToken>()).Returns(false);

        await Resultado(Verificado(lido.Id));

        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _documentos.DidNotReceive().AgendarLimpezaAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Veredicto não definido é erro: o arquivo continua em verificação, nunca verificado")]
    public async Task Resultado_NaoDefinido_EErro()
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();
        _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);

        Func<Task> acao = () => Resultado(new ResultadoDaVerificacaoDeArquivo(
            arquivo.Id, VeredictoDaVerificacao.NaoDefinido, MotivoDaRecusa.NaoSeAplica, new string('b', 64), 2048, FormatoDoArquivo.Pdf));

        await acao.Should().ThrowAsync<ResultadoDaVerificacaoInvalidoException>();
        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.EmVerificacao);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Verificado que difere do declarado (formato, tamanho ou SHA-256) é recusado por falha fechada e o promovido é removido")]
    public async Task Resultado_VerificadoQueNaoConfere_ERecusado()
    {
        foreach (Func<Guid, ResultadoDaVerificacaoDeArquivo> divergente in new Func<Guid, ResultadoDaVerificacaoDeArquivo>[]
        {
            id => Verificado(id) with { Formato = FormatoDoArquivo.Png },
            id => Verificado(id) with { TamanhoBytes = 20 * 1024 * 1024 + 1 },
            id => Verificado(id) with { HashSha256 = "curto" },
        })
        {
            ArquivoDaApresentacao arquivo = EmVerificacao();
            _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);

            await Resultado(divergente(arquivo.Id));

            arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Recusado);
            arquivo.MotivoDaRecusa.Should().Be(MotivoDeRecusaDoArquivo.NaoPassouNaVerificacaoDeSeguranca);
            await _documentos.Received(1).AgendarLimpezaAsync(arquivo.ChaveDefinitivaPrevista, Agora, Arg.Any<CancellationToken>());
        }
    }

    [Theory(DisplayName = "Verificado para arquivo que já terminou recusado ou a reenviar agenda a remoção do promovido e não altera o arquivo")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resultado_VerificadoParaArquivoJaEncerrado_AgendaARemocao(bool reenvio)
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();
        _ = reenvio ? arquivo.PedirReenvio() : arquivo.RegistrarRecusa(MotivoDeRecusaDoArquivo.PdfComSenha);
        _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);

        await Resultado(Verificado(arquivo.Id));

        arquivo.Situacao.Should().Be(reenvio ? SituacaoDoArquivoDoCandidato.ReenvioNecessario : SituacaoDoArquivoDoCandidato.Recusado);
        await _documentos.Received(1).AgendarLimpezaAsync(arquivo.ChaveDefinitivaPrevista, Agora, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Verificado para arquivo que não existe mais agenda a remoção da chave definitiva, para não deixar órfão no bucket")]
    public async Task Resultado_VerificadoSemArquivo_AgendaARemocaoDoPromovido()
    {
        Guid id = Guid.CreateVersion7();

        await Resultado(Verificado(id));
        await Resultado(new ResultadoDaVerificacaoDeArquivo(id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null));

        await _documentos.Received(1).AgendarLimpezaAsync(ArquivoDaApresentacao.ChaveDefinitivaDe(id), Agora, Arg.Any<CancellationToken>());
        await _documentos.ReceivedWithAnyArgs(1).AgendarLimpezaAsync(default!, default, default);
    }

    [Fact(DisplayName = "Resultado recusado grava o motivo, e o reenvio necessário pede um novo envio")]
    public async Task Resultado_RecusadoEReenvio_GravamOEstado()
    {
        ArquivoDaApresentacao recusado = EmVerificacao(), reenvio = EmVerificacao();
        _documentos.ObterArquivoAsync(recusado.Id, Arg.Any<CancellationToken>()).Returns(recusado);
        _documentos.ObterArquivoAsync(reenvio.Id, Arg.Any<CancellationToken>()).Returns(reenvio);

        await Resultado(new ResultadoDaVerificacaoDeArquivo(
            recusado.Id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null));
        await Resultado(new ResultadoDaVerificacaoDeArquivo(
            reenvio.Id, VeredictoDaVerificacao.ReenvioNecessario, MotivoDaRecusa.NaoSeAplica, null, null, null));

        recusado.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Recusado);
        recusado.MensagemAoCandidato.Should().Be("Este PDF está protegido por senha. Envie o arquivo sem senha.");
        reenvio.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.ReenvioNecessario);
    }

    private Task<(Result<int> Resposta, IEnumerable<object> Mensagens)> Reconciliar() =>
        ReconciliarVerificacaoDosArquivosCommandHandler.Handle(
            new ReconciliarVerificacaoDosArquivosCommand(), _documentos, _unitOfWork,
            Options.Create(new ReconciliacaoDaVerificacaoOptions()), new RelogioFixo(Agora), NullLogger.Instance, CancellationToken.None);

    [Fact(DisplayName = "A reconciliação republica o pedido do arquivo vencido e conta o prazo da nova publicação")]
    public async Task Reconciliacao_Republica_EContaOPrazoDaUltimaPublicacao()
    {
        ArquivoDaApresentacao arquivo = EmVerificacao(Agora.AddMinutes(-30));
        _documentos.TentarObterExclusividadeDaReconciliacaoAsync(Arg.Any<CancellationToken>()).Returns(true);
        _documentos.ListarEmVerificacaoParaRepublicarAsync(Agora, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([arquivo]);

        (Result<int> resposta, IEnumerable<object> mensagens) = await Reconciliar();

        resposta.Value.Should().Be(1);
        mensagens.Should().ContainSingle().Which.Should().BeOfType<PedidoDeVerificacaoDeArquivo>()
            .Which.ArquivoId.Should().Be(arquivo.Id);
        arquivo.Publicacoes.Should().Be(2);
        arquivo.ProximaPublicacaoEm.Should().Be(Agora.AddMinutes(20), "o prazo dobra a cada publicação");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A réplica que não obtém a exclusividade da reconciliação não republica nada")]
    public async Task Reconciliacao_SemExclusividade_NaoRepublica()
    {
        _documentos.TentarObterExclusividadeDaReconciliacaoAsync(Arg.Any<CancellationToken>()).Returns(false);

        (Result<int> resposta, IEnumerable<object> mensagens) = await Reconciliar();

        resposta.Value.Should().Be(0);
        mensagens.Should().BeEmpty();
        await _documentos.DidNotReceiveWithAnyArgs().ListarEmVerificacaoParaRepublicarAsync(default, default, default);
    }
}
