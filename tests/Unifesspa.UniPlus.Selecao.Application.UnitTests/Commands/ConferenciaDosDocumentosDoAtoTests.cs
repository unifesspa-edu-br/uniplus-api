namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

public sealed class ConferenciaDosDocumentosDoAtoTests
{
    [Fact]
    public async Task ConferirAsync_NomeComSubstitutoIsolado_RecusaNomeDoAnexo()
    {
        Guid processoId = Guid.CreateVersion7();
        DocumentoAPublicar principal = Documento(processoId);
        DocumentoAPublicar anexo = Documento(processoId);
        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        documentos.ListarDoProcessoAsync(processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([principal, anexo]);
        ConferenciaDosDocumentosDoAto conferencia = new(documentos, Substitute.For<IAtoDoProcessoRepository>());

        Result<DocumentosDoAtoConferidos> resultado = await conferencia.ConferirAsync(
            processoId, principal.Id, [new(anexo.Id, "Quadro\uD800")], CancellationToken.None);

        resultado.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.AnexoNomeInvalido);
    }

    private static DocumentoAPublicar Documento(Guid processoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, "arquivo.pdf", TiposDeMidiaDeDocumento.Pdf,
            TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }
}
