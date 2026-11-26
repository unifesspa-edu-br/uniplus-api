namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.BaseLegalDoFada;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Integração ponta-a-ponta de <c>BaseLegalDoFada</c> contra Postgres real: prova, no
/// nível do banco, que o soft-delete do agregado preserva os municípios da coleção owned —
/// o que o teste HTTP não alcança, porque o filtro global esconde o registro pai por completo
/// depois do soft-delete (ver <see cref="BaseLegalDoFadaEndpointTests"/>).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class BaseLegalDoFadaPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public BaseLegalDoFadaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Soft-delete do agregado preserva as linhas de município na tabela filha (owned collection)")]
    public async Task Remover_PreservaMunicipiosNaTabelaFilha()
    {
        Domain.Entities.BaseLegalDoFada baseLegal = NovaBaseLegal(("1504208", "Marabá", "PA"), ("1501402", "Belém", "PA"));
        await Gravar(baseLegal, "admin-a");

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            ctx.BasesLegaisDoFada.Remove(await ComMunicipios(ctx).SingleAsync(b => b.Id == baseLegal.Id));
            await ctx.SaveChangesAsync();
        }
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Domain.Entities.BaseLegalDoFada excluido = await ComMunicipios(readCtx).IgnoreQueryFilters().SingleAsync(b => b.Id == baseLegal.Id);
        excluido.IsDeleted.Should().BeTrue();
        excluido.Municipios.Select(m => m.CodigoIbge).Should().BeEquivalentTo(["1504208", "1501402"],
            "o soft-delete do agregado não pode apagar fisicamente os municípios da coleção owned");
    }

    [Fact(DisplayName = "Editar só os municípios carimba UpdatedAt e UpdatedBy do agregado")]
    public async Task Atualizar_SoOsMunicipios_CarimbaAuditoriaDoAgregado()
    {
        Domain.Entities.BaseLegalDoFada baseLegal = NovaBaseLegal(("1504208", "Marabá", "PA"));
        await Gravar(baseLegal, "admin-a");

        await AcrescentarMunicipioComo("admin-b", baseLegal.Id);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Domain.Entities.BaseLegalDoFada persistida = await readCtx.BasesLegaisDoFada.SingleAsync(b => b.Id == baseLegal.Id);
        persistida.UpdatedBy.Should().Be("admin-b", "mudar só os municípios também é edição da base legal");
        persistida.UpdatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "O tipo do instrumento é gravado pelo código declarado no enum")]
    public async Task Gravar_TipoInstrumento_GravaOCodigoDeclarado()
    {
        Domain.Entities.BaseLegalDoFada baseLegal = NovaBaseLegal(("1504208", "Marabá", "PA"));
        await Gravar(baseLegal, "admin-a");

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        string gravado = await ctx.Database
            .SqlQuery<string>($"SELECT tipo_instrumento AS \"Value\" FROM configuracao.bases_legais_do_fada WHERE id = {baseLegal.Id}")
            .SingleAsync();
        gravado.Should().Be("PORTARIA");
    }

    [Fact(DisplayName = "A restrição do tipo de instrumento lista os sete códigos declarados, na ordem do enum")]
    public async Task Modelo_RestricaoDoTipoInstrumento_ListaOsCodigosDeclarados()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        ICheckConstraint restricao = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Domain.Entities.BaseLegalDoFada))!
            .GetCheckConstraints()
            .Single(static c => c.Name == "ck_bases_legais_do_fada_tipo_instrumento");
        string[] esperados = [.. CatalogoDe<TipoInstrumentoNormativo>.Todos().Select(static declarado => declarado.Codigo)];
        string[] encontrados = [.. restricao.Sql.Split('\'').Where(static (_, indice) => indice % 2 == 1)];
        encontrados.Should().Equal(esperados);
    }

    [Fact(DisplayName = "O banco recusa tipo de instrumento fora dos códigos declarados")]
    public async Task Gravar_TipoInstrumentoDesconhecido_ViolaARestricao()
    {
        Domain.Entities.BaseLegalDoFada baseLegal = NovaBaseLegal(("1504208", "Marabá", "PA"));
        await Gravar(baseLegal, "admin-a");
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();

        Func<Task> atualizar = async () => await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.bases_legais_do_fada SET tipo_instrumento = 'MEDIDA_PROVISORIA' WHERE id = {baseLegal.Id}");

        PostgresException violacao = (await atualizar.Should().ThrowAsync<PostgresException>()).Which;
        await transacao.RollbackAsync();
        violacao.ConstraintName.Should().Be("ck_bases_legais_do_fada_tipo_instrumento");
    }

    // O código é único entre as bases legais vivas e o banco do teste é compartilhado: cada teste cadastra um ato próprio.
    private static Domain.Entities.BaseLegalDoFada NovaBaseLegal(params (string?, string?, string?)[] municipios) =>
        Domain.Entities.BaseLegalDoFada.Criar("PORTARIA", $"Portaria Teste {Guid.NewGuid():N}", "Institui inclusão regional", municipios).Value!;

    private static IQueryable<Domain.Entities.BaseLegalDoFada> ComMunicipios(UniPlusDbContext ctx) =>
        ctx.BasesLegaisDoFada.Include(b => b.Municipios);

    private async Task AcrescentarMunicipioComo(string usuario, Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        Domain.Entities.BaseLegalDoFada rastreada = await ComMunicipios(ctx).SingleAsync(b => b.Id == id);
        (string?, string?, string?)[] dois = [("1504208", "Marabá", "PA"), ("1501402", "Belém", "PA")];
        rastreada.Atualizar("PORTARIA", rastreada.Identificacao, "Institui inclusão regional", dois).IsSuccess.Should().BeTrue();
        await ctx.SaveChangesAsync();
    }

    private async Task Gravar(Domain.Entities.BaseLegalDoFada baseLegal, string usuario)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ctx.BasesLegaisDoFada.Add(baseLegal);
        await ctx.SaveChangesAsync();
    }
}
