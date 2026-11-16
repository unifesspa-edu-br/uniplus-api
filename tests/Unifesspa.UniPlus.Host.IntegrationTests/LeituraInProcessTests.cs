namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Prova central do monólito modular: com os 5 módulos co-hospedados
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

    [Fact(DisplayName = "Resolve a Unidade viva pelo código e entrega a mesma Unidade que o Id")]
    public async Task ObterPorCodigo_UnidadeViva_ResolveAMesmaUnidadeDoId()
    {
        Unidade unidade = await SemearUnidadeAsync($"cod-vivo-{Guid.NewGuid():N}"[..20], $"CV{Guid.NewGuid():N}"[..10], $"COD-{Guid.NewGuid():N}"[..14]);

        UnidadeView? porCodigo = await ObterPorCodigoAsync(unidade.Codigo);

        porCodigo.Should().NotBeNull();
        porCodigo!.Id.Should().Be(unidade.Id);
        porCodigo.Codigo.Should().Be(unidade.Codigo);
    }

    [Fact(DisplayName = "Código inexistente não resolve")]
    public async Task ObterPorCodigo_CodigoInexistente_RetornaNulo()
    {
        UnidadeView? view = await ObterPorCodigoAsync($"NAO-EXISTE-{Guid.NewGuid():N}"[..20]);

        view.Should().BeNull();
    }

    [Fact(DisplayName = "Código que a Unidade perdeu não resolve, mesmo com a lista em cache ainda mostrando o código antigo")]
    public async Task ObterPorCodigo_CodigoAntigoComCacheAquecido_NaoResolve()
    {
        string sufixo = Guid.NewGuid().ToString("N");
        Unidade unidade = await SemearUnidadeAsync($"cod-antigo-{sufixo}"[..20], $"CA{sufixo}"[..10], $"ANT-{sufixo}"[..14]);
        string codigoAntigo = unidade.Codigo;
        string codigoNovo = $"NOV-{sufixo}"[..14];

        await using (AsyncServiceScope aquece = _fixture.Factory.Services.CreateAsyncScope())
        {
            await aquece.ServiceProvider.GetRequiredService<IUnidadeReader>().ListarAtivasAsync(CancellationToken.None);
        }

        await TrocarCodigoAsync(unidade.Id, codigoNovo);

        (await ObterPorCodigoAsync(codigoAntigo)).Should().BeNull("o código antigo deixou de ser da Unidade");
        UnidadeView? novo = await ObterPorCodigoAsync(codigoNovo);
        novo.Should().NotBeNull();
        novo!.Id.Should().Be(unidade.Id);
    }

    [Fact(DisplayName = "Unidade excluída não resolve pelo código")]
    public async Task ObterPorCodigo_UnidadeExcluida_NaoResolve()
    {
        string sufixo = Guid.NewGuid().ToString("N");
        Unidade unidade = await SemearUnidadeAsync($"cod-excl-{sufixo}"[..20], $"CE{sufixo}"[..10], $"EXC-{sufixo}"[..14]);

        await using (AsyncServiceScope escrita = _fixture.Factory.Services.CreateAsyncScope())
        {
            UniPlusDbContext dbContext = escrita.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            Unidade alvo = await dbContext.Unidades.FindAsync(unidade.Id) ?? throw new InvalidOperationException("Unidade semeada sumiu.");
            dbContext.Unidades.Remove(alvo);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        (await ObterPorCodigoAsync(unidade.Codigo)).Should().BeNull();
    }

    private async Task<UnidadeView?> ObterPorCodigoAsync(string codigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUnidadeReader>().ObterPorCodigoAsync(codigo, CancellationToken.None);
    }

    private async Task TrocarCodigoAsync(Guid unidadeId, string novoCodigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = await dbContext.Unidades.FindAsync(unidadeId) ?? throw new InvalidOperationException("Unidade semeada sumiu.");

        unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(
                new IdentificadoresDaUnidade(unidade.Slug.Valor, unidade.Sigla, novoCodigo, unidade.Alias), DataInicio.AddDays(1), "troca de código"),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            unidade.Tipo,
            unidade.UnidadeAcademica).IsSuccess.Should().BeTrue();
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<Unidade> SemearUnidadeAsync(string slug, string sigla, string codigo)
    {
        Unidade unidade = Unidade.Criar(
            $"Unidade {sigla}",
            new IdentificadoresDaUnidade(slug, sigla, codigo, null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false).Value!;

        // Semeia pelo contexto do container do host — o
        // schema `organizacao` já foi criado pelas migrations on startup no boot.
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return unidade;
    }
}
