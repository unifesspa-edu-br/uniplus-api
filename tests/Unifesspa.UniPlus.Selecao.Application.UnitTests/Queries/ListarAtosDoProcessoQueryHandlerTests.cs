namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A consulta administrativa projeta os registros dos atos do processo com os documentos.
/// </summary>
public sealed class ListarAtosDoProcessoQueryHandlerTests
{
    private const string HashDoEdital = "9f2c1e0b7a4d5c6e8f0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60";
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();

    public ListarAtosDoProcessoQueryHandlerTests()
    {
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(true);
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns([]);
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private Task<Result<ListarAtosDoProcessoResult>> Consultar() => ListarAtosDoProcessoQueryHandler.Handle(
        new ListarAtosDoProcessoQuery(_processoId),
        _processos,
        _atos,
        _documentos,
        CancellationToken.None);

    private DocumentoAPublicar Confirmado(string hash = HashDoEdital, FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            _processoId, finalidade, "documento.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private AtoDoProcesso Registro(
        DocumentoAPublicar principal, DateTimeOffset publicadoEm, IReadOnlyList<AnexoDeclarado>? anexos = null)
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), _processoId, "AVISO", "Aviso", "3/2026", new DateOnly(2026, 12, 10),
                "Diretor do CEPS", "ceps", "psm-medicina-2027", principal.Id, anexos ?? []),
            publicadoEm).Value!;
        typeof(AtoDoProcesso).GetProperty(nameof(AtoDoProcesso.CreatedAt))!
            .SetValue(ato, publicadoEm);
        return ato;
    }

    [Fact(DisplayName = "Os atos voltam na ordem do repositório, com o principal e os anexos pelo nome de exibição")]
    public async Task Handle_ProjetaDocumentosNaOrdemDoRepositorio()
    {
        DocumentoAPublicar principalDoMaisNovo = Confirmado();
        DocumentoAPublicar anexo = Confirmado(new string('b', 64));
        DocumentoAPublicar principalDoMaisAntigo = Confirmado(new string('c', 64));
        AtoDoProcesso maisNovo = Registro(principalDoMaisNovo, Agora.AddMinutes(-1), anexos: [new AnexoDeclarado(anexo.Id, "Anexo I")]);
        AtoDoProcesso maisAntigo = Registro(principalDoMaisAntigo, Agora.AddDays(-2));
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns([maisNovo, maisAntigo]);
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([principalDoMaisNovo, anexo, principalDoMaisAntigo]);

        Result<ListarAtosDoProcessoResult> resultado = await Consultar();

        AtoDoProcessoDto[] itens = [.. resultado.Value!.Items];
        itens.Select(i => i.Id).Should().Equal(maisNovo.Id, maisAntigo.Id);
        itens[0].TipoCodigo.Should().Be("AVISO");
        itens[0].DocumentoPrincipal!.DocumentoAPublicarId.Should().Be(principalDoMaisNovo.Id);
        itens[0].Anexos.Select(a => (a.DocumentoAPublicarId, a.NomeDeExibicao)).Should().Equal((anexo.Id, "Anexo I"));
    }

    [Fact(DisplayName = "Ato que referencia documento ausente é inconsistência de dado e não é escondido")]
    public async Task Handle_DocumentoReferenciadoAusente_Lanca()
    {
        AtoDoProcesso ato = Registro(Confirmado(), Agora.AddMinutes(-1));
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns([ato]);

        Func<Task> consultar = Consultar;

        await consultar.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{ato.Id}*");
    }

    [Fact(DisplayName = "Processo inexistente devolve não encontrado, e não lista vazia")]
    public async Task Handle_ProcessoInexistente_DevolveNaoEncontrado()
    {
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<ListarAtosDoProcessoResult> resultado = await Consultar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }
}
