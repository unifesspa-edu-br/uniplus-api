namespace Unifesspa.UniPlus.Host.IntegrationTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Prova que as três consultas da Organização Institucional, que devolvem <c>Result</c>, atravessam o bus do
/// composition root real: o sucesso e a recusa chegam ao chamador como o handler os devolveu.
/// </summary>
/// <remarks>
/// O Wolverine monta o handler em compile-time e só um <c>Send</c> pelo host real expõe um retorno que o
/// codegen não sabe despachar; o teste unitário do handler, com dublê, não passa por ele.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
public sealed class OrganizacaoQueriesNoBusTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public OrganizacaoQueriesNoBusTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A query de Unidade por Id atravessa o bus: falha com não encontrada e devolve o DTO da Unidade criada")]
    public async Task QueryDeUnidadePorId_AtravessaOBusComSucessoERecusa()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        Result<Guid> criada = await commandBus.Send(NovaUnidade("bus-por-id"), CancellationToken.None);

        Result<UnidadeDto> inexistente = await queryBus.Send(new ObterUnidadePorIdQuery(Guid.NewGuid()), CancellationToken.None);
        Result<UnidadeDto> existente = await queryBus.Send(new ObterUnidadePorIdQuery(criada.Value), CancellationToken.None);

        inexistente.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
        existente.Value!.Sigla.Should().Be("BUS-POR-ID");
    }

    [Fact(DisplayName = "A query de listagem de Unidades atravessa o bus: recusa o tipo inválido e pagina com o filtro válido")]
    public async Task QueryDeListagem_AtravessaOBusComSucessoERecusa()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        await commandBus.Send(NovaUnidade("bus-listagem"), CancellationToken.None);

        Result<ListarUnidadesAtivasResult> recusada = await ListarAsync(queryBus, "bus-listagem", 999);
        Result<ListarUnidadesAtivasResult> pagina = await ListarAsync(queryBus, "bus-listagem", (int)TipoUnidade.Centro);

        recusada.Error!.Code.Should().Be(UnidadeErrorCodes.FiltroDeTipoInvalido);
        pagina.Value!.Items.Should().ContainSingle().Which.Sigla.Should().Be("BUS-LISTAGEM");
    }

    [Fact(DisplayName = "A query da Instituição atravessa o bus: falha antes do cadastro e devolve o DTO depois")]
    public async Task QueryDaInstituicao_AtravessaOBusComRecusaESucesso()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        Result<InstituicaoDto> antes = await queryBus.Send(new ObterInstituicaoQuery(), CancellationToken.None);
        CriarInstituicaoCommand command = new InstituicaoCommandBuilder().Build();
        Result<Guid> criada = await commandBus.Send(command, CancellationToken.None);
        Result<InstituicaoDto> depois = await queryBus.Send(new ObterInstituicaoQuery(), CancellationToken.None);

        antes.Error!.Code.Should().Be(InstituicaoErrorCodes.NaoEncontrada);
        depois.Value!.Id.Should().Be(criada.Value);
    }

    private static Task<Result<ListarUnidadesAtivasResult>> ListarAsync(IQueryBus queryBus, string busca, int tipo) =>
        queryBus.Send(
            new ListarUnidadesAtivasQuery(null, 20, PaginationDirection.Next, busca, [tipo]),
            CancellationToken.None);

    private static CriarUnidadeCommand NovaUnidade(string identificador) =>
        new(
            Nome: "Unidade exercitada pelo bus",
            Alias: null,
            Slug: identificador,
            Sigla: identificador.ToUpperInvariant(),
            Codigo: identificador,
            UnidadeSuperiorId: null,
            Tipo: TipoUnidade.Centro,
            UnidadeAcademica: false,
            VigenciaInicio: new DateOnly(2026, 1, 1),
            VigenciaFim: null);
}
