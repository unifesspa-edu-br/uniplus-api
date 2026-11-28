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

    [Fact(DisplayName = "Documento principal inexistente é recusado apontando documentoPrincipalId, sem citar o identificador")]
    public async Task ConferirAsync_PrincipalInexistente_ApontaOCampoDoPrincipal()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid principalInexistente = Guid.CreateVersion7();

        Result<DocumentosDoAtoConferidos> resultado = await ConferirAsync(processoId, principalInexistente, [], []);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("documentoPrincipalId");
        recusa.Error.Code.Should().Be(AtoDoProcessoErrorCodes.DocumentoNaoEncontrado);
        recusa.Error.Message.Should().Be("O documento principal do ato deve ser um dos documentos deste processo.");
    }

    [Fact(DisplayName = "Anexo inexistente é recusado apontando a posição dele no pedido, sem citar o identificador")]
    public async Task ConferirAsync_AnexoInexistente_ApontaOCampoDoAnexo()
    {
        Guid processoId = Guid.CreateVersion7();
        DocumentoAPublicar principal = Documento(processoId);
        DocumentoAPublicar anexo = Documento(processoId);
        AnexoSolicitadoDoAto[] pedidos = [new(anexo.Id, "Quadro"), new(Guid.CreateVersion7(), "Lista")];

        Result<DocumentosDoAtoConferidos> resultado = await ConferirAsync(processoId, principal.Id, pedidos, [principal, anexo]);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("anexos[1].documentoId");
        recusa.Error.Code.Should().Be(AtoDoProcessoErrorCodes.DocumentoNaoEncontrado);
        recusa.Error.Message.Should().Be("O documento do anexo deve ser um dos documentos deste processo.");
    }

    private static Task<Result<DocumentosDoAtoConferidos>> ConferirAsync(
        Guid processoId, Guid principalId, AnexoSolicitadoDoAto[] pedidos, DocumentoAPublicar[] existentes)
    {
        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        documentos.ListarDoProcessoAsync(processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(existentes);
        ConferenciaDosDocumentosDoAto conferencia = new(documentos, Substitute.For<IAtoDoProcessoRepository>());

        return conferencia.ConferirAsync(processoId, principalId, pedidos, CancellationToken.None);
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
