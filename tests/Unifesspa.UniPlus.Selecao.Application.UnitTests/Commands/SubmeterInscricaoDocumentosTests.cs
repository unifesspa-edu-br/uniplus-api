namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

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
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// O envio conta os arquivos dos documentos obrigatórios.
/// </summary>
public sealed class SubmeterInscricaoDocumentosTests
{
    private readonly VersaoConfiguracao _versao;
    private readonly ConfiguracaoPublicada _configuracao;
    private readonly VersaoTermoEscolhida _termo;
    private DocumentoExigido _laudo = null!;
    private DocumentoExigido _naoSeAplica = null!;

    public SubmeterInscricaoDocumentosTests() =>
        (_versao, _configuracao, _termo) = MontarVersaoComItemObrigatorioETermo(exigencias: Exigencias);

    [Fact]
    public async Task Handle_DocumentoObrigatorioAusente_RecusaSemRemoverArquivos()
    {
        (Result<ReciboDaInscricaoDto> resultado, IDocumentoDaInscricaoRepository documentos, _, _, _) = await EnviarAsync(comLaudo: false);
        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.DocumentoObrigatorioAusente);
        await documentos.DidNotReceiveWithAnyArgs().RemoverArquivoAsync(default!, default);
    }

    [Fact]
    public async Task Handle_DocumentoObrigatorioEmVerificacao_OrientaAguardar()
    {
        (Result<ReciboDaInscricaoDto> resultado, _, _, _, _) = await EnviarAsync(comLaudo: false, laudoEmVerificacao: true);
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.DocumentoObrigatorioEmVerificacao)
            .Which.Error.Message.Should().Be("Aguarde a verificação completa dos documentos obrigatórios para enviar a inscrição.");
    }

    [Fact]
    public async Task Handle_DocumentoEItemObrigatoriosAusentes_AcumulaRecusas()
    {
        (Result<ReciboDaInscricaoDto> resultado, _, _, _, _) = await EnviarAsync(comLaudo: false, semApelido: true);
        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(InscricaoErrorCodes.DocumentoObrigatorioAusente)
            .And.Contain(c => c != InscricaoErrorCodes.DocumentoObrigatorioAusente);
    }

    [Fact]
    public async Task Handle_DocumentoObrigatorioVerificado_FixaArquivoELimpaOInaplicavel()
    {
        (Result<ReciboDaInscricaoDto> resultado, IDocumentoDaInscricaoRepository documentos,
            ArquivoDaApresentacao fixado, ArquivoDaApresentacao orfao, Inscricao enviada) = await EnviarAsync(comLaudo: true);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fixado.ConstouDeEnvio.Should().BeTrue();
        DocumentoDoComprovante comprovante = Emitido(enviada).Documentos.Should().ContainSingle().Subject;
        comprovante.Nome.Should().Be("Laudo");
        comprovante.Situacao.Should().Be(SituacaoDoDocumentoCodigo.Satisfeita);
        comprovante.Arquivos.Should().Equal(new ArquivoDoComprovante(new string('a', 64), 1024, "PDF"));
        await documentos.Received(1).RemoverArquivoAsync(orfao, Arg.Any<CancellationToken>());
        await documentos.Received(1).AgendarLimpezaAsync(orfao.ChaveDefinitivaPrevista, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private (IReadOnlyList<DocumentoExigido>, IReadOnlyList<NoExigencia>) Exigencias(ProcessoSeletivo processo)
    {
        Guid fase = processo.CronogramaFases.First(static f => f.ColetaInscricao).Id;
        _laudo = Documento(fase, "Laudo", Aplicabilidade.Geral, []);
        _naoSeAplica = Documento(fase, "Outro", Aplicabilidade.Condicional,
            [CondicaoGatilho.Criar(0, "APELIDO", Operador.Igual, JsonSerializer.SerializeToElement("Outro")).Value!]);
        return ([_laudo, _naoSeAplica], [NoExigencia.CriarFolha(_laudo, 0).Value!, NoExigencia.CriarFolha(_naoSeAplica, 1).Value!]);
    }

    private static DocumentoExigido Documento(Guid fase, string nome, Aplicabilidade aplicabilidade, IReadOnlyList<CondicaoGatilho> condicoes) =>
        DocumentoExigido.Criar(fase, Guid.CreateVersion7(), nome.ToUpperInvariant(), nome, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade, obrigatorio: true, null, condicoes, [], null, FormatosPermitidos.Criar(true, null).Value!, null,
            finalidade: FinalidadeFormulario.Inscricao).Value!;

    private async Task<(Result<ReciboDaInscricaoDto>, IDocumentoDaInscricaoRepository, ArquivoDaApresentacao, ArquivoDaApresentacao, Inscricao)> EnviarAsync(
        bool comLaudo, bool semApelido = false, bool laudoEmVerificacao = false)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao inscricao = NovaInscricao(cpf, "conta-1", _versao.Id);
        (IInscricaoRepository inscricoes, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = PortasPara(inscricao, _versao, _configuracao);
        (IDocumentoDaInscricaoRepository documentos, ArquivoDaApresentacao fixado, ArquivoDaApresentacao orfao) = MontarDocumentos(inscricao, comLaudo, laudoEmVerificacao);
        Result<ReciboDaInscricaoDto> resultado = await new SubmissaoDeTeste(inscricoes, processos, codecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf))
        {
            Documentos = documentos,
        }.SubmeterAsync(new SubmeterInscricaoCommand(ProcessoId, Pedido(cpf, semApelido), PrecondicaoIfMatch.DeTags([inscricao.ETag])));
        Guardar(inscricao, inscricoes);
        return (resultado, documentos, fixado, orfao, inscricao);
    }

    private InscricaoRequest Pedido(Cpf cpf, bool semApelido)
    {
        InscricaoRequest pedido = RequestComApelidoETermoAceito(cpf, _termo, _configuracao);
        return semApelido ? pedido with { Respostas = pedido.Respostas!.Where(static r => r.Key != "APELIDO").ToDictionary(static r => r.Key, static r => r.Value) } : pedido;
    }

    private (IDocumentoDaInscricaoRepository, ArquivoDaApresentacao, ArquivoDaApresentacao) MontarDocumentos(Inscricao inscricao, bool comLaudo, bool laudoEmVerificacao)
    {
        ApresentacaoDeDocumento doLaudo = ApresentacaoDeDocumento.Criar(inscricao.Id, _laudo, null);
        ApresentacaoDeDocumento doOutro = ApresentacaoDeDocumento.Criar(inscricao.Id, _naoSeAplica, null);
        ArquivoDaApresentacao fixado = Confirmado(inscricao, doLaudo, _laudo, comLaudo);
        ArquivoDaApresentacao orfao = Confirmado(inscricao, doOutro, _naoSeAplica);
        IDocumentoDaInscricaoRepository documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
        documentos.ListarApresentacoesDaInscricaoAsync(inscricao.Id, Arg.Any<CancellationToken>()).Returns([doLaudo, doOutro]);
        documentos.ListarArquivosDaInscricaoAsync(inscricao.Id, Arg.Any<CancellationToken>()).Returns(comLaudo || laudoEmVerificacao ? [fixado, orfao] : [orfao]);
        documentos.ObterArquivoAsync(fixado.Id, Arg.Any<CancellationToken>()).Returns(fixado);
        documentos.ObterArquivoAsync(orfao.Id, Arg.Any<CancellationToken>()).Returns(orfao);
        return (documentos, fixado, orfao);
    }

    private static ArquivoDaApresentacao Confirmado(Inscricao inscricao, ApresentacaoDeDocumento apresentacao, DocumentoExigido exigencia, bool verificado = true)
    {
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricao.Id, exigencia, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", TimeProvider.System);
        if (verificado)
        {
            arquivo.RegistrarVerificado(1024, new string('a', 64), FormatoPermitido.Pdf);
        }
        return arquivo;
    }
}
