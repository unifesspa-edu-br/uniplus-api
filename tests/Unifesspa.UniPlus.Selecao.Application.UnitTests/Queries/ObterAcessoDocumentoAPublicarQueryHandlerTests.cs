namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

public sealed class ObterAcessoDocumentoAPublicarQueryHandlerTests
{
    private const string UrlAssinada = "http://minio.local/uniplus/selecao/documentos-a-publicar/x/confirmado.pdf?X-Amz-Signature=abc";

    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly IUnidadeDonaDoProcesso _donos = Substitute.For<IUnidadeDonaDoProcesso>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();
    private readonly RelogioFixo _clock = new(Agora);

    private DocumentoAPublicar Pendente(Guid processoSeletivoId) =>
        DocumentoAPublicar.IniciarPendente(processoSeletivoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, _clock, TimeSpan.FromMinutes(15)).Value!;

    private DocumentoAPublicar Confirmado(Guid processoSeletivoId)
    {
        DocumentoAPublicar documento = Pendente(processoSeletivoId);
        documento.Confirmar(2048, new string('a', 64), _clock).IsSuccess.Should().BeTrue();
        return documento;
    }

    private Task<Result<AcessoDocumentoAPublicarDto>> AcessarAsync(Guid processoId, Guid documentoId) =>
        ObterAcessoDocumentoAPublicarQueryHandler.Handle(
            new ObterAcessoDocumentoAPublicarQuery(processoId, documentoId), _donos, _documentos, _storage, _clock, CancellationToken.None);

    private void ProcessoComDono(Guid processoId) =>
        _donos.DoProcessoAsync(processoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());

    private async Task NaoDeveTerAssinadoAsync() =>
        await _storage.DidNotReceive().GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());

    [Fact(DisplayName = "Dono inexistente recusa antes de tocar no documento ou no storage")]
    public async Task DonoInexistente_Recusa()
    {
        _donos.DoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);

        Result<AcessoDocumentoAPublicarDto> resultado = await AcessarAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await _documentos.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await NaoDeveTerAssinadoAsync();
    }

    [Fact(DisplayName = "Processo excluído logicamente, que a porta não devolve, recusa mesmo com documento confirmado")]
    public async Task ProcessoExcluidoLogicamenteComDocumentoConfirmado_Recusa()
    {
        Guid processoId = Guid.CreateVersion7();
        DocumentoAPublicar confirmado = Confirmado(processoId);
        _documentos.ObterPorIdAsync(confirmado.Id, Arg.Any<CancellationToken>()).Returns(confirmado);
        _donos.DoProcessoAsync(processoId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        Result<AcessoDocumentoAPublicarDto> resultado = await AcessarAsync(processoId, confirmado.Id);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await NaoDeveTerAssinadoAsync();
    }

    [Fact(DisplayName = "Documento inexistente recusa sem assinar")]
    public async Task DocumentoInexistente_Recusa()
    {
        Guid processoId = Guid.CreateVersion7();
        ProcessoComDono(processoId);
        _documentos.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DocumentoAPublicar?)null);

        Result<AcessoDocumentoAPublicarDto> resultado = await AcessarAsync(processoId, Guid.CreateVersion7());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoAPublicar.NaoEncontrado");
        await NaoDeveTerAssinadoAsync();
    }

    /// <summary>
    /// A recusa é a mesma de documento inexistente, e de propósito: distinguir
    /// as duas confirmaria a quem tenta um id colhido em outro lugar que ele
    /// existe em algum processo.
    /// </summary>
    [Fact(DisplayName = "Documento de outro processo recusa como inexistente, sem assinar")]
    public async Task DocumentoDeOutroProcesso_RecusaComoInexistente()
    {
        Guid processoId = Guid.CreateVersion7();
        DocumentoAPublicar deOutroProcesso = Confirmado(Guid.CreateVersion7());
        ProcessoComDono(processoId);
        _documentos.ObterPorIdAsync(deOutroProcesso.Id, Arg.Any<CancellationToken>()).Returns(deOutroProcesso);

        Result<AcessoDocumentoAPublicarDto> resultado = await AcessarAsync(processoId, deOutroProcesso.Id);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoAPublicar.NaoEncontrado");
        await NaoDeveTerAssinadoAsync();
    }

    /// <summary>
    /// O pendente não passou pela validação de conteúdo — o objeto na chave de
    /// upload pode não ser o PDF que o registro virá a atestar.
    /// </summary>
    [Fact(DisplayName = "Documento pendente recusa sem assinar")]
    public async Task DocumentoPendente_RecusaSemAssinar()
    {
        Guid processoId = Guid.CreateVersion7();
        DocumentoAPublicar pendente = Pendente(processoId);
        ProcessoComDono(processoId);
        _documentos.ObterPorIdAsync(pendente.Id, Arg.Any<CancellationToken>()).Returns(pendente);

        Result<AcessoDocumentoAPublicarDto> resultado = await AcessarAsync(processoId, pendente.Id);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoAPublicar.NaoConfirmado");
        await NaoDeveTerAssinadoAsync();
    }

    [Fact(DisplayName = "Documento confirmado devolve URL assinada da cópia gravada na confirmação com o prazo do relógio")]
    public async Task DocumentoConfirmado_DevolveUrlDaCopiaGravada()
    {
        Guid processoId = Guid.CreateVersion7();
        DocumentoAPublicar confirmado = Confirmado(processoId);
        ProcessoComDono(processoId);
        _documentos.ObterPorIdAsync(confirmado.Id, Arg.Any<CancellationToken>()).Returns(confirmado);
        _storage.GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(UrlAssinada);

        Result<AcessoDocumentoAPublicarDto> resultado = await AcessarAsync(processoId, confirmado.Id);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Url.Should().Be(new Uri(UrlAssinada, UriKind.Absolute));
        resultado.Value.ExpiraEm.Should().Be(Agora.Add(ObterAcessoDocumentoAPublicarQueryHandler.TtlLeitura));
        await ConferirQueAssinouACopiaGravadaAsync(confirmado);
    }

    // A cópia gravada na confirmação, nunca a chave de upload: é ela que o hash atesta, e a de upload segue alcançável pela URL de PUT
    // enquanto o TTL não expira.
    private async Task ConferirQueAssinouACopiaGravadaAsync(DocumentoAPublicar confirmado)
    {
        await _storage.Received(1).GerarUrlLeituraAsync(
            confirmado.ObjectKeyConfirmado!, ObterAcessoDocumentoAPublicarQueryHandler.TtlLeitura, Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().GerarUrlLeituraAsync(confirmado.ObjectKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
