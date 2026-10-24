namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O acesso de leitura ao arquivo do próprio candidato: só o verificado, só do dono, com nome genérico.</summary>
public sealed class ObterAcessoDoArquivoDoCandidatoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();
    private readonly IDocumentoDaInscricaoRepository _documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
    private readonly IArquivoDoCandidatoStorage _storage = Substitute.For<IArquivoDoCandidatoStorage>();
    private readonly Inscricao _inscricao;
    private readonly DocumentoExigido _laudo;

    public ObterAcessoDoArquivoDoCandidatoQueryHandlerTests()
    {
        _inscricao = Inscricao.Iniciar(ProcessoId, PersonasDeTeste.KevinCarvalho, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!;
        _inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, PersonasDeTeste.KevinCarvalho, Arg.Any<CancellationToken>()).Returns(_inscricao);
        _laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", Aplicabilidade.Geral, false, null,
            [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;
        _storage.GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://armazenamento.exemplo/leitura");
    }

    private ArquivoDaApresentacao Arquivo(Guid inscricaoId, Action<ArquivoDaApresentacao>? estado = null)
    {
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), inscricaoId, _laudo, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", TimeProvider.System);
        estado?.Invoke(arquivo);
        _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);
        _documentos.ListarArquivosDaInscricaoAsync(inscricaoId, Arg.Any<CancellationToken>()).Returns([arquivo]);
        return arquivo;
    }

    private Task<Result<AcessoArquivoDoCandidatoDto>> Acessar(Guid arquivoId, string sub = "sub-1")
    {
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.Cpf.Returns(PersonasDeTeste.KevinCarvalho.Valor);
        usuario.UserId.Returns(sub);
        return ObterAcessoDoArquivoDoCandidatoQueryHandler.Handle(
            new ObterAcessoDoArquivoDoCandidatoQuery(ProcessoId, arquivoId), _inscricoes, _documentos, _storage, usuario,
            new RelogioFixo(DateTimeOffset.UnixEpoch), NullLogger.Instance, CancellationToken.None);
    }

    [Fact(DisplayName = "O arquivo verificado do dono devolve a URL assinada na chave definitiva, com o nome genérico")]
    public async Task Verificado_DoDono_DevolveAUrl()
    {
        ArquivoDaApresentacao arquivo = Arquivo(_inscricao.Id, a => a.RegistrarVerificado(1024, new string('a', 64), FormatoPermitido.Pdf));

        Result<AcessoArquivoDoCandidatoDto> resultado = await Acessar(arquivo.Id);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Url.Should().Be(new Uri("https://armazenamento.exemplo/leitura"));
        await _storage.Received(1).GerarUrlLeituraAsync(
            arquivo.ChaveDefinitivaPrevista, "Arquivo 1.pdf", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O arquivo de outra inscrição recebe a recusa de arquivo inexistente, sem chamar o armazenamento")]
    public async Task DeOutraInscricao_NaoEncontrado()
    {
        ArquivoDaApresentacao arquivo = Arquivo(Guid.CreateVersion7(), a => a.RegistrarVerificado(1024, new string('a', 64), FormatoPermitido.Pdf));

        Result<AcessoArquivoDoCandidatoDto> resultado = await Acessar(arquivo.Id);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado);
        _storage.ReceivedCalls().Should().BeEmpty();
    }

    [Theory(DisplayName = "O arquivo em verificação ou recusado não é verificado, e o armazenamento não é chamado")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NaoVerificado_Recusa(bool recusado)
    {
        ArquivoDaApresentacao arquivo = Arquivo(_inscricao.Id, a =>
        {
            if (recusado)
            {
                a.RegistrarRecusa(MotivoDeRecusaDoArquivo.PdfComSenha);
            }
        });

        Result<AcessoArquivoDoCandidatoDto> resultado = await Acessar(arquivo.Id);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoVerificado);
        _storage.ReceivedCalls().Should().BeEmpty();
    }
}
