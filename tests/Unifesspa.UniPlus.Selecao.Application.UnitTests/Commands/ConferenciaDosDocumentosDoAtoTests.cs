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

        ResultadoDosDocumentos resultado = await conferencia.ConferirAsync(
            processoId, PedidoDosDocumentosDoAto.De(principal.Id, [new(anexo.Id, "Quadro\uD800")]), CancellationToken.None);

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(AtoDoProcessoErrorCodes.AnexoNomeInvalido);
    }

    [Fact(DisplayName = "Documento principal inexistente é recusado apontando documentoPrincipalId, sem citar o identificador")]
    public async Task ConferirAsync_PrincipalInexistente_ApontaOCampoDoPrincipal()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid principalInexistente = Guid.CreateVersion7();

        ResultadoDosDocumentos resultado = await ConferirAsync(processoId, principalInexistente, [], []);

        FieldError recusa = resultado.Recusas.Should().ContainSingle().Subject;
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

        ResultadoDosDocumentos resultado = await ConferirAsync(processoId, principal.Id, pedidos, [principal, anexo]);

        FieldError recusa = resultado.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("anexos[1].documentoId");
        recusa.Error.Code.Should().Be(AtoDoProcessoErrorCodes.DocumentoNaoEncontrado);
        recusa.Error.Message.Should().Be("O documento do anexo deve ser um dos documentos deste processo.");
    }

    [Fact(DisplayName = "O documento já usado em outro ato é recusado junto do nome inválido do mesmo anexo, que não dependem um do outro")]
    public async Task ConferirAsync_AnexoJaUsadoENomeInvalido_SomaAsDuasRecusas()
    {
        Guid processoId = Guid.CreateVersion7();
        DocumentoAPublicar principal = Documento(processoId);
        DocumentoAPublicar anexo = Documento(processoId);
        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        documentos.ListarDoProcessoAsync(processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([principal, anexo]);
        IAtoDoProcessoRepository atos = Substitute.For<IAtoDoProcessoRepository>();
        atos.ListarDocumentosJaUsadosAsync(processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { anexo.Id });

        ResultadoDosDocumentos resultado = await new ConferenciaDosDocumentosDoAto(documentos, atos).ConferirAsync(
            processoId, PedidoDosDocumentosDoAto.De(principal.Id, [new(anexo.Id, "a/b")]), CancellationToken.None);

        resultado.Recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("anexos[0].nome", AtoDoProcessoErrorCodes.AnexoNomeInvalido),
            ("anexos[0].documentoId", AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto));
        resultado.Conferidos.Should().BeNull();
    }

    private static Task<ResultadoDosDocumentos> ConferirAsync(
        Guid processoId, Guid principalId, AnexoSolicitadoDoAto[] pedidos, DocumentoAPublicar[] existentes)
    {
        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        documentos.ListarDoProcessoAsync(processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(existentes);
        ConferenciaDosDocumentosDoAto conferencia = new(documentos, Substitute.For<IAtoDoProcessoRepository>());

        return conferencia.ConferirAsync(processoId, PedidoDosDocumentosDoAto.De(principalId, pedidos), CancellationToken.None);
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
