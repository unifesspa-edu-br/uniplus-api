namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>
/// A lista de manutenção dos modelos filtra por tipo de processo, por servir à fase de inscrição e por estado.
/// </summary>
public sealed class ListarModelosFormularioQueryHandlerTests
{
    private readonly IModeloFormularioRepository _repository = Substitute.For<IModeloFormularioRepository>();

    public ListarModelosFormularioQueryHandlerTests()
    {
        _repository.ListarPaginadoAsync(default, default, default, default, default!, default).ReturnsForAnyArgs(
            ((IReadOnlyList<ModeloFormulario>)[], ((string SortKey, Guid Id)?)null, ((string SortKey, Guid Id)?)null));
    }

    [Theory(DisplayName = "O filtro de servir à fase de inscrição vai ao repositório como veio, e ausente não filtra")]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Handle_FiltroDeInscricao_ConsultaComOFiltro(bool? serveAFaseDeInscricao)
    {
        await ListarAsync(serveAFaseDeInscricao: serveAFaseDeInscricao);

        await _repository.Received(1).ListarPaginadoAsync(
            null, null, 20, PaginationDirection.Next, new FiltroDosModelosDeFormulario(null, serveAFaseDeInscricao, null), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo de processo que o banco não grava vai ao repositório, que confere o cursor antes de devolver a página vazia")]
    public async Task Handle_TipoDeProcessoNaoGravavel_ConsultaORepositorio()
    {
        await ListarAsync(tipoProcesso: "TIPO\0");

        await _repository.Received(1).ListarPaginadoAsync(
            null, null, 20, PaginationDirection.Next, new FiltroDosModelosDeFormulario("TIPO\0", null, null), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "O tipo de processo é aparado")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("  REGULAR_UNIFICADO  ", "REGULAR_UNIFICADO")]
    [InlineData("REGULAR_UNIFICADO", "REGULAR_UNIFICADO")]
    [InlineData("regular_unificado", "regular_unificado")]
    public async Task Handle_TipoDeProcesso_AparaEspacos(string? informado, string? esperado)
    {
        await ListarAsync(tipoProcesso: informado);

        await _repository.Received(1).ListarPaginadoAsync(
            null, null, 20, PaginationDirection.Next, new FiltroDosModelosDeFormulario(esperado, null, null), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O filtro de inscrição, o estado e a posição da página vão ao repositório")]
    public async Task Handle_FiltrosEPagina_VaoAoRepositorio()
    {
        Guid pontoDeContinuacao = Guid.NewGuid();

        await ListarAsync(serveAFaseDeInscricao: false, ativo: false, afterSortKey: "chave", afterId: pontoDeContinuacao, direction: PaginationDirection.Prev);

        await _repository.Received(1).ListarPaginadoAsync(
            "chave", pontoDeContinuacao, 20, PaginationDirection.Prev, new FiltroDosModelosDeFormulario(null, false, false), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Os modelos da página saem para manutenção, com os cursores do repositório")]
    public async Task Handle_Pagina_ProjetaOsModelosEOsCursores()
    {
        ModeloFormulario modelo = Modelo();
        ((string SortKey, Guid Id) anterior, (string SortKey, Guid Id) proximo) = (("a", Guid.NewGuid()), ("b", Guid.NewGuid()));
        _repository.ListarPaginadoAsync(default, default, default, default, default!, default).ReturnsForAnyArgs(
            ((IReadOnlyList<ModeloFormulario>)[modelo], ((string SortKey, Guid Id)?)anterior, ((string SortKey, Guid Id)?)proximo));

        ListarModelosFormularioResult resultado = await ListarAsync();

        resultado.Items.Select(static m => (m.Id, m.Codigo, m.Declaracao)).Should().Equal((modelo.Id, "MODELO_DE_TESTE", new DeclaracaoDoModeloDto(false, true)));
        (resultado.Anterior, resultado.Proximo).Should().Be((anterior, proximo));
    }

    private Task<ListarModelosFormularioResult> ListarAsync(
        string? tipoProcesso = null,
        bool? serveAFaseDeInscricao = null,
        bool? ativo = null,
        string? afterSortKey = null,
        Guid? afterId = null,
        PaginationDirection direction = PaginationDirection.Next) =>
        ListarModelosFormularioQueryHandler.Handle(
            new ListarModelosFormularioQuery(afterSortKey, afterId, 20, direction, tipoProcesso, serveAFaseDeInscricao, ativo), _repository, CancellationToken.None);

    private static ModeloFormulario Modelo()
    {
        PaginaDoModelo dados = new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null);
        PaginaDoModelo revisao = new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);
        Kernel.Results.Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "MODELO_DE_TESTE", new DescritivoDoModelo("Modelo de teste", null, null), DeOutraFaseObrigatoria,
            new ConteudoDoModelo("Habilitação", [dados, revisao], [], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        return modelo.Value!;
    }
}
