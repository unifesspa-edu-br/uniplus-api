namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Cobertura da leitura dos documentos do dono — processo ou rascunho: o pendente e o confirmado convivem na mesma
/// resposta, o dono sem documento não se confunde com o dono inexistente, e nenhum endereço de storage atravessa a
/// projeção.
/// </summary>
public sealed class ListarDocumentosAPublicarQueryHandlerTests
{
    private const string HashFixo = "9f2c1e0b7a4d5c6e8f0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60";

    private readonly IUnidadeDonaDoProcesso _donos = Substitute.For<IUnidadeDonaDoProcesso>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();

    private static DocumentoAPublicar NovoPendente(Guid processoSeletivoId) =>
        DocumentoAPublicar.IniciarPendente(processoSeletivoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;

    private static DocumentoAPublicar NovoConfirmado(Guid processoSeletivoId, long tamanhoBytes)
    {
        DocumentoAPublicar documento = NovoPendente(processoSeletivoId);
        documento.Confirmar(tamanhoBytes, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private Task<Result<ListarDocumentosAPublicarResult>> ListarAsync(
        Guid processoSeletivoId, Guid? unidadeDona, params DocumentoAPublicar[] documentosDoRepositorio)
    {
        _donos.DoProcessoAsync(processoSeletivoId, Arg.Any<CancellationToken>()).Returns(unidadeDona);
        _documentos.ListarPorProcessoAsync(processoSeletivoId, Arg.Any<FinalidadeDoDocumento?>(), Arg.Any<CancellationToken>())
            .Returns(documentosDoRepositorio);

        return ListarDocumentosAPublicarQueryHandler.Handle(
            new ListarDocumentosAPublicarQuery(processoSeletivoId), _donos, _documentos, CancellationToken.None);
    }

    private static void ConferirProjecaoDoPendente(DocumentoAPublicarDto item, DocumentoAPublicar pendente)
    {
        item.Status.Codigo.Should().Be("PENDENTE");
        item.ProcessoSeletivoId.Should().Be(pendente.ProcessoSeletivoId);
        item.ExpiraEm.Should().Be(pendente.ExpiraEm);
        item.TamanhoBytes.Should().BeNull();
        item.HashSha256.Should().BeNull();
        item.ConfirmadoEm.Should().BeNull();
    }

    private static void ConferirProjecaoDoConfirmado(DocumentoAPublicarDto item, DocumentoAPublicar confirmado)
    {
        item.Status.Codigo.Should().Be("CONFIRMADO");
        item.TamanhoBytes.Should().Be(4096);
        item.HashSha256.Should().Be(HashFixo);
        item.ConfirmadoEm.Should().Be(confirmado.ConfirmadoEm);
    }

    [Fact(DisplayName = "Pendente e confirmado voltam na mesma lista, na ordem do repositório, com os metadados de confirmação nulos só no pendente")]
    public async Task Handle_PendenteEConfirmado_ProjetaCadaEstadoComOsMetadadosQueEleTem()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        DocumentoAPublicar confirmado = NovoConfirmado(processoSeletivoId, tamanhoBytes: 4096);
        DocumentoAPublicar pendente = NovoPendente(processoSeletivoId);

        Result<ListarDocumentosAPublicarResult> resultado = await ListarAsync(processoSeletivoId, Guid.CreateVersion7(), pendente, confirmado);

        // A ordem é decidida pela consulta, no repositório; o handler apenas projeta.
        DocumentoAPublicarDto[] items = [.. resultado.Value!.Items];
        items.Select(i => i.Id).Should().Equal(pendente.Id, confirmado.Id);
        ConferirProjecaoDoPendente(items[0], pendente);
        ConferirProjecaoDoConfirmado(items[1], confirmado);
    }

    [Fact(DisplayName = "Processo existente sem documento devolve lista vazia, não 404")]
    public async Task Handle_ProcessoExistenteSemDocumento_DevolveListaVazia()
    {
        Result<ListarDocumentosAPublicarResult> resultado = await ListarAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Dono inexistente recusa com ProcessoSeletivo.NaoEncontrado em vez de lista vazia, sem ler documento")]
    public async Task Handle_DonoInexistente_RecusaSemLerDocumentos()
    {
        Result<ListarDocumentosAPublicarResult> resultado = await ListarAsync(Guid.CreateVersion7(), unidadeDona: null);

        resultado.IsSuccess.Should().BeFalse();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await _documentos.DidNotReceive().ListarPorProcessoAsync(
            Arg.Any<Guid>(), Arg.Any<FinalidadeDoDocumento?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo fora de alcance recusa mesmo tendo documento — o documento sobrevive à exclusão lógica do processo")]
    public async Task Handle_ProcessoInvisivelComDocumento_Recusa()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();

        // A linha do processo excluído logicamente e os documentos continuam no banco; só o filtro global, que a porta
        // aplica, tira o processo de alcance. Ter documento, portanto, não prova que o dono existe.
        Result<ListarDocumentosAPublicarResult> resultado = await ListarAsync(processoSeletivoId, null, NovoPendente(processoSeletivoId));

        resultado.IsSuccess.Should().BeFalse();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Rascunho, que tem unidade dona e nenhuma linha de processo, lista os documentos")]
    public async Task Handle_RascunhoComDono_ListaOsDocumentos()
    {
        Guid rascunhoId = Guid.CreateVersion7();
        DocumentoAPublicar pendente = NovoPendente(rascunhoId);

        Result<ListarDocumentosAPublicarResult> resultado = await ListarAsync(rascunhoId, Guid.CreateVersion7(), pendente);

        resultado.Value!.Items.Select(i => i.Id).Should().Equal(pendente.Id);
    }

    [Fact(DisplayName = "O contrato do documento não tem onde carregar chave de objeto nem URL de storage")]
    public void DocumentoEditalDto_NaoExpoeEnderecoDeStorage()
    {
        string[] membros = [.. typeof(DocumentoAPublicarDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)];

        // A entidade guarda ObjectKey e ObjectKeyConfirmado, e o início do upload emite uma URL pre-assinada que
        // continua válida até o TTL expirar: qualquer um desses no DTO entregaria a quem lê a lista o poder de
        // sobrescrever o objeto.
        membros.Should().NotContain(nome =>
            nome.Contains("ObjectKey", StringComparison.OrdinalIgnoreCase)
            || nome.Contains("Url", StringComparison.OrdinalIgnoreCase));
    }
}
