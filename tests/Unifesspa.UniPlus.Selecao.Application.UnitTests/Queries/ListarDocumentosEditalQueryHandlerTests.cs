namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosEdital;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura da leitura dos documentos do Edital: o pendente e o confirmado
/// convivem na mesma resposta, o processo sem documento não se confunde com o
/// processo inexistente, e nenhum endereço de storage atravessa a projeção.
/// </summary>
public sealed class ListarDocumentosEditalQueryHandlerTests
{
    private const string HashFixo = "9f2c1e0b7a4d5c6e8f0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60";

    private static DocumentoEdital NovoPendente(Guid processoSeletivoId) =>
        DocumentoEdital.IniciarPendente(processoSeletivoId, TimeProvider.System, TimeSpan.FromMinutes(15));

    private static DocumentoEdital NovoConfirmado(Guid processoSeletivoId, long tamanhoBytes)
    {
        DocumentoEdital documento = NovoPendente(processoSeletivoId);
        documento.Confirmar(tamanhoBytes, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    [Fact(DisplayName = "Pendente e confirmado voltam na mesma lista, na ordem do repositório, com os metadados de confirmação nulos só no pendente")]
    public async Task Handle_PendenteEConfirmado_ProjetaCadaEstadoComOsMetadadosQueEleTem()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        DocumentoEdital confirmado = NovoConfirmado(processoSeletivoId, tamanhoBytes: 4096);
        DocumentoEdital pendente = NovoPendente(processoSeletivoId);

        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>())
            .Returns([pendente, confirmado]);

        Result<ListarDocumentosEditalResult> resultado = await ListarDocumentosEditalQueryHandler.Handle(
            new ListarDocumentosEditalQuery(processoSeletivoId),
            documentos,
            processos,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();

        // A ordem é decidida pela consulta, no repositório; o handler apenas
        // projeta. Reordenar aqui desfaria a ordenação que o contrato promete.
        DocumentoEditalDto[] items = [.. resultado.Value!.Items];
        items.Select(i => i.Id).Should().Equal(pendente.Id, confirmado.Id);

        DocumentoEditalDto itemPendente = items[0];
        itemPendente.Status.Should().Be("Pendente");
        itemPendente.ProcessoSeletivoId.Should().Be(processoSeletivoId);
        itemPendente.ExpiraEm.Should().Be(pendente.ExpiraEm);
        itemPendente.TamanhoBytes.Should().BeNull();
        itemPendente.HashSha256.Should().BeNull();
        itemPendente.ConfirmadoEm.Should().BeNull();

        DocumentoEditalDto itemConfirmado = items[1];
        itemConfirmado.Status.Should().Be("Confirmado");
        itemConfirmado.TamanhoBytes.Should().Be(4096);
        itemConfirmado.HashSha256.Should().Be(HashFixo);
        itemConfirmado.ConfirmadoEm.Should().Be(confirmado.ConfirmadoEm);
    }

    [Fact(DisplayName = "Processo existente sem documento devolve lista vazia, não 404")]
    public async Task Handle_ProcessoExistenteSemDocumento_DevolveListaVazia()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns([]);
        processos.ExisteAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns(true);

        Result<ListarDocumentosEditalResult> resultado = await ListarDocumentosEditalQueryHandler.Handle(
            new ListarDocumentosEditalQuery(processoSeletivoId),
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
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns([]);
        processos.ExisteAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<ListarDocumentosEditalResult> resultado = await ListarDocumentosEditalQueryHandler.Handle(
            new ListarDocumentosEditalQuery(processoSeletivoId),
            documentos,
            processos,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeFalse();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Lista com documento dispensa a consulta de existência do processo — a chave estrangeira já respondeu")]
    public async Task Handle_ComDocumento_NaoConsultaExistenciaDoProcesso()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>())
            .Returns([NovoPendente(processoSeletivoId)]);

        await ListarDocumentosEditalQueryHandler.Handle(
            new ListarDocumentosEditalQuery(processoSeletivoId),
            documentos,
            processos,
            CancellationToken.None);

        await processos.DidNotReceive().ExisteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O contrato do documento não tem onde carregar chave de objeto nem URL de storage")]
    public void DocumentoEditalDto_NaoExpoeEnderecoDeStorage()
    {
        string[] membros = [.. typeof(DocumentoEditalDto)
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
