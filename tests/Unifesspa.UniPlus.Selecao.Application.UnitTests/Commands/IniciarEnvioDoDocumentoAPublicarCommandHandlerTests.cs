namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;

public sealed class IniciarEnvioDoDocumentoAPublicarCommandHandlerTests
{
    private const string UrlAssinada = "https://minio.local/uniplus-documentos/x?X-Amz-Signature=fake";

    private readonly CenarioDoDocumentoAPublicar _cenario = new();

    [Fact(DisplayName = "Sem rascunho nem processo com o identificador, recusa 404 sem criar documento")]
    public async Task SemDono_Recusa()
    {
        Result<IniciarEnvioDoDocumentoAPublicarDto> resultado = await _cenario.IniciarAsync();

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Theory(DisplayName = "Com dono, o rascunho ou o processo, cria o pendente ligado ao identificador e devolve a URL pré-assinada do tipo de mídia")]
    [InlineData(TiposDeMidiaDeDocumento.Pdf)]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public async Task ComDono_CriaPendenteEDevolveUrl(string tipoDeMidia)
    {
        _cenario.Donos.DoProcessoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        _cenario.Storage.GerarUrlUploadAsync(Arg.Any<string>(), tipoDeMidia, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(UrlAssinada);

        Result<IniciarEnvioDoDocumentoAPublicarDto> resultado = await _cenario.IniciarAsync(tipoDeMidia);

        resultado.Value!.UrlUpload.Should().Be(new Uri(UrlAssinada));
        resultado.Value.ContentTypeExigido.Should().Be(tipoDeMidia);
        await _cenario.Documentos.Received(1).AdicionarAsync(
            Arg.Is<DocumentoAPublicar>(d => d.ProcessoSeletivoId == _cenario.ProcessoId && d.Status == StatusArquivoEnviado.Pendente), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com dono, grava o pendente na unidade de trabalho")]
    public async Task ComDono_Grava()
    {
        _cenario.Donos.DoProcessoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        _cenario.Storage.GerarUrlUploadAsync(default!, default!, default, default).ReturnsForAnyArgs(UrlAssinada);

        await _cenario.IniciarAsync();

        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de mídia fora da lista é recusado sem gerar URL nem criar documento")]
    public async Task TipoDeMidiaForaDaLista_RecusaSemGerarUrl()
    {
        _cenario.Donos.DoProcessoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());

        Result<IniciarEnvioDoDocumentoAPublicarDto> resultado = await _cenario.IniciarAsync("application/vnd.ms-excel");

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("DocumentoAPublicar.TipoDeMidiaNaoAceito");
        await _cenario.Storage.DidNotReceiveWithAnyArgs().GerarUrlUploadAsync(default!, default!, default, default);
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "Se a geração da URL falha, nada é persistido: o retry não deixa pendente órfão")]
    public async Task FalhaAoGerarUrl_NaoPersiste()
    {
        _cenario.Donos.DoProcessoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        _cenario.Storage.GerarUrlUploadAsync(default!, default!, default, default)
            .ReturnsForAnyArgs<Task<string>>(_ => throw new InvalidOperationException("Armazenamento indisponível (simulado)"));

        Func<Task> iniciar = () => _cenario.IniciarAsync();

        await iniciar.Should().ThrowAsync<InvalidOperationException>();
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await _cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }
}
