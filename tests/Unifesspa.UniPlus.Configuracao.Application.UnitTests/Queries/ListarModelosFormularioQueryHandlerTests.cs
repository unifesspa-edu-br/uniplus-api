namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A lista de manutenção dos modelos filtra por tipo de processo, finalidade e estado: o filtro que
/// não casa com nada que exista dá a página vazia, sem consultar o banco.
/// </summary>
public sealed class ListarModelosFormularioQueryHandlerTests
{
    private readonly IModeloFormularioRepository _repository = Substitute.For<IModeloFormularioRepository>();

    public ListarModelosFormularioQueryHandlerTests()
    {
        _repository.ListarPaginadoAsync(default, default, default, default, default, default, default).ReturnsForAnyArgs(
            ((IReadOnlyList<ModeloFormulario>)[], (Guid?)null, (Guid?)null));
    }

    [Fact(DisplayName = "Finalidade desconhecida dá a página vazia, em vez de ignorar o filtro")]
    public async Task Handle_FinalidadeDesconhecida_DevolvePaginaVaziaSemConsultar()
    {
        ListarModelosFormularioResult resultado = await ListarAsync(finalidade: "NAO_EXISTE");

        resultado.Items.Should().BeEmpty();
        (resultado.AnteriorAfterId, resultado.ProximoAfterId).Should().Be((null, null));
        await _repository.DidNotReceiveWithAnyArgs().ListarPaginadoAsync(default, default, default, default, default, default, default);
    }

    [Fact(DisplayName = "Tipo de processo que o banco não grava não é de nenhum modelo: a página é vazia")]
    public async Task Handle_TipoDeProcessoNaoGravavel_DevolvePaginaVaziaSemConsultar()
    {
        ListarModelosFormularioResult resultado = await ListarAsync(tipoProcesso: "TIPO\0");

        resultado.Items.Should().BeEmpty();
        await _repository.DidNotReceiveWithAnyArgs().ListarPaginadoAsync(default, default, default, default, default, default, default);
    }

    [Theory(DisplayName = "O tipo de processo em branco não filtra, e o informado vai aparado")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("  PSR  ", "PSR")]
    public async Task Handle_TipoDeProcesso_VaiAparadoOuNaoFiltra(string? informado, string? esperado)
    {
        await ListarAsync(tipoProcesso: informado);

        await _repository.Received(1).ListarPaginadoAsync(
            null, 20, PaginationDirection.Next, esperado, null, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A finalidade conhecida, o estado e a posição da página vão ao repositório")]
    public async Task Handle_FiltrosEPagina_VaoAoRepositorio()
    {
        Guid ancora = Guid.NewGuid();

        await ListarAsync(finalidade: "HABILITACAO", ativo: false, afterId: ancora, direction: PaginationDirection.Prev);

        await _repository.Received(1).ListarPaginadoAsync(
            ancora, 20, PaginationDirection.Prev, null, FinalidadeFormulario.Habilitacao, false, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Os modelos da página saem para manutenção, com os cursores do repositório")]
    public async Task Handle_Pagina_ProjetaOsModelosEOsCursores()
    {
        ModeloFormulario modelo = Modelo();
        (Guid anterior, Guid proximo) = (Guid.NewGuid(), Guid.NewGuid());
        _repository.ListarPaginadoAsync(default, default, default, default, default, default, default).ReturnsForAnyArgs(
            ((IReadOnlyList<ModeloFormulario>)[modelo], (Guid?)anterior, (Guid?)proximo));

        ListarModelosFormularioResult resultado = await ListarAsync();

        resultado.Items.Select(static m => (m.Id, m.Codigo, m.Finalidade.Codigo)).Should().Equal((modelo.Id, "MODELO_DE_TESTE", "HABILITACAO"));
        (resultado.AnteriorAfterId, resultado.ProximoAfterId).Should().Be((anterior, proximo));
    }

    private Task<ListarModelosFormularioResult> ListarAsync(
        string? tipoProcesso = null,
        string? finalidade = null,
        bool? ativo = null,
        Guid? afterId = null,
        PaginationDirection direction = PaginationDirection.Next) =>
        ListarModelosFormularioQueryHandler.Handle(
            new ListarModelosFormularioQuery(afterId, 20, direction, tipoProcesso, finalidade, ativo), _repository, CancellationToken.None);

    private static ModeloFormulario Modelo()
    {
        EtapaDoModelo dados = new("DADOS", 0, TipoEtapaFormulario.Secao, null, "Dados", null, null, null);
        EtapaDoModelo revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);
        Kernel.Results.Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "MODELO_DE_TESTE", "Modelo de teste", null, FinalidadeFormulario.Habilitacao, null,
            new ConteudoDoModelo("Habilitação", [dados, revisao], [], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        return modelo.Value!;
    }
}
