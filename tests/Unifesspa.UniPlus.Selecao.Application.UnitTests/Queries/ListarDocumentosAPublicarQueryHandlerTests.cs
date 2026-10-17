namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Cobertura da leitura dos documentos do Edital: o pendente e o confirmado
/// convivem na mesma resposta, o processo sem documento não se confunde com o
/// processo inexistente, e nenhum endereço de storage atravessa a projeção.
/// </summary>
public sealed class ListarDocumentosAPublicarQueryHandlerTests
{
    private const string HashFixo = "9f2c1e0b7a4d5c6e8f0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60";

    private static DocumentoAPublicar NovoPendente(Guid processoSeletivoId) =>
        DocumentoAPublicar.IniciarPendente(processoSeletivoId, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;

    private static DocumentoAPublicar NovoConfirmado(Guid processoSeletivoId, long tamanhoBytes)
    {
        DocumentoAPublicar documento = NovoPendente(processoSeletivoId);
        documento.Confirmar(tamanhoBytes, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    [Fact(DisplayName = "Pendente e confirmado voltam na mesma lista, na ordem do repositório, com os metadados de confirmação nulos só no pendente")]
    public async Task Handle_PendenteEConfirmado_ProjetaCadaEstadoComOsMetadadosQueEleTem()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        DocumentoAPublicar confirmado = NovoConfirmado(processoSeletivoId, tamanhoBytes: 4096);
        DocumentoAPublicar pendente = NovoPendente(processoSeletivoId);

        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>())
            .Returns([pendente, confirmado]);
        processos.ExisteAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns(true);

        Result<ListarDocumentosAPublicarResult> resultado = await ListarDocumentosAPublicarQueryHandler.Handle(
            new ListarDocumentosAPublicarQuery(processoSeletivoId),
            documentos,
            processos,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();

        // A ordem é decidida pela consulta, no repositório; o handler apenas
        // projeta. Reordenar aqui desfaria a ordenação que o contrato promete.
        DocumentoAPublicarDto[] items = [.. resultado.Value!.Items];
        items.Select(i => i.Id).Should().Equal(pendente.Id, confirmado.Id);

        DocumentoAPublicarDto itemPendente = items[0];
        itemPendente.Status.Should().Be("Pendente");
        itemPendente.ProcessoSeletivoId.Should().Be(processoSeletivoId);
        itemPendente.ExpiraEm.Should().Be(pendente.ExpiraEm);
        itemPendente.TamanhoBytes.Should().BeNull();
        itemPendente.HashSha256.Should().BeNull();
        itemPendente.ConfirmadoEm.Should().BeNull();

        DocumentoAPublicarDto itemConfirmado = items[1];
        itemConfirmado.Status.Should().Be("Confirmado");
        itemConfirmado.TamanhoBytes.Should().Be(4096);
        itemConfirmado.HashSha256.Should().Be(HashFixo);
        itemConfirmado.ConfirmadoEm.Should().Be(confirmado.ConfirmadoEm);
    }

    [Fact(DisplayName = "Processo existente sem documento devolve lista vazia, não 404")]
    public async Task Handle_ProcessoExistenteSemDocumento_DevolveListaVazia()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns([]);
        processos.ExisteAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns(true);

        Result<ListarDocumentosAPublicarResult> resultado = await ListarDocumentosAPublicarQueryHandler.Handle(
            new ListarDocumentosAPublicarQuery(processoSeletivoId),
            documentos,
            processos,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Processo inexistente recusa com ProcessoSeletivo.NaoEncontrado em vez de lista vazia")]
    public async Task Handle_ProcessoInexistente_Recusa()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns([]);
        processos.ExisteAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<ListarDocumentosAPublicarResult> resultado = await ListarDocumentosAPublicarQueryHandler.Handle(
            new ListarDocumentosAPublicarQuery(processoSeletivoId),
            documentos,
            processos,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeFalse();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Processo fora de alcance recusa mesmo tendo documento — a chave estrangeira sobrevive à exclusão lógica")]
    public async Task Handle_ProcessoInvisivelComDocumento_Recusa()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        IDocumentoAPublicarRepository documentos = Substitute.For<IDocumentoAPublicarRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();

        // O processo é excluído logicamente: a linha continua no banco, as
        // chaves estrangeiras seguem íntegras e os documentos continuam
        // apontando para ele — só o filtro global tira o processo de alcance.
        // Ter documento, portanto, não prova que o processo é visível.
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>())
            .Returns([NovoPendente(processoSeletivoId)]);
        processos.ExisteAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<ListarDocumentosAPublicarResult> resultado = await ListarDocumentosAPublicarQueryHandler.Handle(
            new ListarDocumentosAPublicarQuery(processoSeletivoId),
            documentos,
            processos,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeFalse();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "O contrato do documento não tem onde carregar chave de objeto nem URL de storage")]
    public void DocumentoEditalDto_NaoExpoeEnderecoDeStorage()
    {
        string[] membros = [.. typeof(DocumentoAPublicarDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)];

        // A entidade guarda ObjectKey e ObjectKeyConfirmado, e o início do
        // upload emite uma URL pre-assinada que continua válida até o TTL
        // expirar. Qualquer um desses no DTO entregaria a quem lê a lista o
        // poder de sobrescrever o objeto — por isso a ausência é asserida, e
        // não apenas pretendida pela projeção.
        membros.Should().NotContain(nome =>
            nome.Contains("ObjectKey", StringComparison.OrdinalIgnoreCase)
            || nome.Contains("Url", StringComparison.OrdinalIgnoreCase));
    }
}
