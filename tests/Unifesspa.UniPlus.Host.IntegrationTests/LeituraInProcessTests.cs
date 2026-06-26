namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Prova central do monólito modular: com os 4 módulos co-hospedados
/// num processo único sobre o banco <c>uniplus</c> (schema-por-módulo), um
/// consumidor cross-módulo (ex.: Configuração / Seleção) lê uma <c>Unidade</c>
/// viva <em>in-process</em> via <see cref="IUnidadeReader"/> — sem acesso direto
/// ao schema <c>organizacao</c> nem hop de rede. É o desbloqueio da #588
/// (OfertaCurso lê a Unidade in-process).
/// </summary>
/// <remarks>
/// O <see cref="IUnidadeReader"/> é registrado pelo
/// <c>AddOrganizacaoInstitucionalModule</c> e exposto via Governance.Contracts —
/// o consumidor depende do contrato, não da Infrastructure de Organização
/// (fronteira preservada para extração futura; R8). Aqui resolvemos o reader do
/// container do host (o mesmo que um handler de outro módulo resolveria) e
/// confirmamos que ele enxerga a Unidade semeada no schema <c>organizacao</c>.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class LeituraInProcessTests
{
    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private readonly MonolitoPostgresFixture _fixture;

    public LeituraInProcessTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Reader cross-módulo lê in-process uma Unidade viva pelo Id (desbloqueio #588)")]
    public async Task ObterPorId_LeUnidadeVivaInProcess()
    {
        Unidade unidade = await SemearUnidadeAsync("ceps-in-process", "CEPSIP", "CIP001");

        // Resolve o reader do container do host — exatamente o que um handler de
        // Configuração/Seleção faria. Escopo independente do de escrita.
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        IUnidadeReader reader = scope.ServiceProvider.GetRequiredService<IUnidadeReader>();

        UnidadeView? view = await reader.ObterPorIdAsync(unidade.Id, CancellationToken.None);

        view.Should().NotBeNull("a Unidade semeada no schema organizacao deve ser visível in-process");
        view!.Id.Should().Be(unidade.Id);
        view.Sigla.Should().Be("CEPSIP");
        view.Slug.Should().Be("ceps-in-process");
        view.Nome.Should().Be("Unidade CEPSIP");
    }

    [Fact(DisplayName = "Reader cross-módulo lista in-process as Unidades ativas semeadas")]
    public async Task ListarAtivas_IncluiUnidadeSemeada()
    {
        Unidade unidade = await SemearUnidadeAsync("listagem-in-process", "LISTIP", "LIP001");

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        IUnidadeReader reader = scope.ServiceProvider.GetRequiredService<IUnidadeReader>();

        IReadOnlyList<UnidadeView> ativas = await reader.ListarAtivasAsync(CancellationToken.None);

        ativas.Should().Contain(u => u.Id == unidade.Id && u.Sigla == "LISTIP");
    }

    private async Task<Unidade> SemearUnidadeAsync(string slug, string sigla, string codigo)
    {
        Unidade unidade = Unidade.Criar(
            nome: $"Unidade {sigla}",
            alias: null,
            slug: Slug.From(slug).Value!,
            sigla: sigla,
            codigo: codigo,
            unidadeSuperiorId: null,
            tipo: TipoUnidade.Centro,
            unidadeAcademica: false,
            vigenciaInicio: DataInicio,
            vigenciaFim: null,
            origem: OrigemUnidade.CriadoNoUniPlus).Value!;

        // Semeia pelo DbContext do MÓDULO resolvido do container do host — o
        // schema `organizacao` já foi criado pelas migrations on startup no boot.
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();

        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return unidade;
    }
}
