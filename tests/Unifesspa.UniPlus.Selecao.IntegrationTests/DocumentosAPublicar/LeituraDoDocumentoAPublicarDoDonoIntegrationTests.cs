namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// A leitura do documento a publicar decide pelo dono, lido pela porta sobre o banco real: o rascunho, que não tem linha
/// de processo, é lido; o identificador desconhecido e o processo excluído logicamente, que mantém a linha e os
/// documentos, são recusados como inexistentes antes de qualquer documento ser lido ou assinado.
/// </summary>
public sealed class LeituraDoDocumentoAPublicarDoDonoIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string UrlAssinada = "http://armazenamento.local/confirmado.pdf?assinatura=abc";

    private readonly ProcessoSeletivoDbFixture _db;
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();

    public LeituraDoDocumentoAPublicarDoDonoIntegrationTests(ProcessoSeletivoDbFixture db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _storage.GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(UrlAssinada);
    }

    [Fact(DisplayName = "A lista do rascunho, que não tem linha de processo, traz os documentos dele")]
    public async Task Listar_Rascunho_TrazOsDocumentos()
    {
        await using EnvioDoDocumentoAPublicar envio = await EnvioDoDocumentoAPublicar.NovoDeRascunhoAsync(_db, _storage);
        DocumentoAPublicar confirmado = await envio.GravarConfirmadoAsync();

        Result<ListarDocumentosAPublicarResult> lista = await envio.ListarAsync();

        lista.IsSuccess.Should().BeTrue();
        lista.Value!.Items.Select(i => i.Id).Should().Equal(confirmado.Id);
    }

    [Fact(DisplayName = "O acesso ao documento confirmado do rascunho assina a leitura da cópia gravada na confirmação")]
    public async Task Acessar_DocumentoConfirmadoDoRascunho_AssinaALeitura()
    {
        await using EnvioDoDocumentoAPublicar envio = await EnvioDoDocumentoAPublicar.NovoDeRascunhoAsync(_db, _storage);
        DocumentoAPublicar confirmado = await envio.GravarConfirmadoAsync();

        Result<AcessoDocumentoAPublicarDto> acesso = await envio.AcessarAsync(confirmado.Id);

        acesso.IsSuccess.Should().BeTrue();
        acesso.Value!.Url.Should().Be(new Uri(UrlAssinada, UriKind.Absolute));
        await _storage.Received(1).GerarUrlLeituraAsync(confirmado.ObjectKeyConfirmado!, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem processo nem rascunho com o identificador, a lista e o acesso recusam como inexistente")]
    public async Task IdentificadorInexistente_RecusaListaEAcesso()
    {
        await using EnvioDoDocumentoAPublicar envio = EnvioDoDocumentoAPublicar.NovoSemDono(_db, _storage);

        Result<ListarDocumentosAPublicarResult> lista = await envio.ListarAsync();
        Result<AcessoDocumentoAPublicarDto> acesso = await envio.AcessarAsync(Guid.CreateVersion7());

        lista.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        acesso.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "O processo excluído logicamente recusa a lista e o acesso mesmo com documento confirmado gravado")]
    public async Task ProcessoExcluidoLogicamente_RecusaListaEAcessoSemAssinar()
    {
        await using EnvioDoDocumentoAPublicar envio = await EnvioDoDocumentoAPublicar.NovoDeProcessoExcluidoAsync(_db, _storage);
        DocumentoAPublicar confirmado = await envio.GravarConfirmadoAsync();

        Result<ListarDocumentosAPublicarResult> lista = await envio.ListarAsync();
        Result<AcessoDocumentoAPublicarDto> acesso = await envio.AcessarAsync(confirmado.Id);

        lista.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        acesso.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await _storage.DidNotReceive().GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}
