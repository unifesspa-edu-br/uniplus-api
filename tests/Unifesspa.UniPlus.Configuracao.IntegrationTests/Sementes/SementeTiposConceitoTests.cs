namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Os tipos de conceito de uso corrente existem ao subir o sistema, gravados pelo script de sementes do módulo.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class SementeTiposConceitoTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public SementeTiposConceitoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Depois da preparação do banco, existem a nota numérica e o apto/inapto categórico")]
    public async Task PreparacaoDoBanco_GravaOsTiposDeConceito()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        TipoConceito nota = await ctx.TiposConceito.SingleAsync(t => t.Codigo == "NOTA");
        TipoConceito aptoInapto = await ctx.TiposConceito.SingleAsync(t => t.Codigo == "APTO_INAPTO");

        nota.Ativo.Should().BeTrue();
        nota.Natureza.Should().Be(NaturezaConceito.Numerico);
        nota.EscalaMinima.Should().Be(0);
        nota.EscalaMaxima.Should().Be(1000);
        nota.CasasDecimais.Should().Be(2);

        aptoInapto.Natureza.Should().Be(NaturezaConceito.Categorico);
        aptoInapto.Valores.Select(static v => (v.Codigo, v.ContaComoAprovacao, v.Posicao))
            .Should().Equal(("APTO", true, 1), ("INAPTO", false, 2));
    }

    [Fact(DisplayName = "Reaplicar o script de sementes não duplica nada nem devolve o que a administração editou ou removeu")]
    public async Task Reaplicar_PreservaAsEdicoesDaAdministracao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        // As edições da administração são desfeitas ao fim: o banco da coleção é compartilhado com os demais testes.
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await AdministracaoEditaOAptoInaptoAsync(ctx);
        int tiposAntes = await ctx.TiposConceito.CountAsync();

        await ReaplicarOScriptAsync(ctx);

        (await ctx.TiposConceito.CountAsync()).Should().Be(tiposAntes);
        (await VinculosDoAptoInaptoAsync(ctx)).Should().Be(0, "o vínculo que a administração removeu não volta");
        (await ValoresDoAptoInaptoAsync(ctx)).Should().Equal(["APTO|Habilitado", "INAPTO|Inapto"], "o rótulo editado não é sobrescrito e os valores não se duplicam");
        await transacao.RollbackAsync();
    }

    private const string TipoAptoInapto = "(SELECT id FROM configuracao.tipos_conceito WHERE codigo = 'APTO_INAPTO')";

    private static async Task AdministracaoEditaOAptoInaptoAsync(UniPlusDbContext ctx)
    {
        await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM configuracao.tipos_etapa_conceitos WHERE tipo_conceito_id = {TipoAptoInapto}");
        await ctx.Database.ExecuteSqlRawAsync(
            $"UPDATE configuracao.opcoes_do_tipo_de_conceito SET rotulo = 'Habilitado' WHERE codigo = 'APTO' AND tipo_conceito_id = {TipoAptoInapto}");
    }

    private static async Task ReaplicarOScriptAsync(UniPlusDbContext ctx) =>
        await ctx.Database.ExecuteSqlRawAsync(ScriptDeSemente().Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal));

    private static async Task<int> VinculosDoAptoInaptoAsync(UniPlusDbContext ctx) =>
        await ctx.Database.SqlQueryRaw<int>(
            $"SELECT count(*)::int AS \"Value\" FROM configuracao.tipos_etapa_conceitos WHERE tipo_conceito_id = {TipoAptoInapto}").SingleAsync();

    private static async Task<List<string>> ValoresDoAptoInaptoAsync(UniPlusDbContext ctx) =>
        await ctx.Database.SqlQueryRaw<string>(
            $"SELECT codigo || '|' || rotulo AS \"Value\" FROM configuracao.opcoes_do_tipo_de_conceito WHERE tipo_conceito_id = {TipoAptoInapto} ORDER BY posicao").ToListAsync();

    private static string ScriptDeSemente()
    {
        Assembly infra = typeof(ConfiguracaoDbSets).Assembly;
        string recurso = infra.GetManifestResourceNames().Single(static n => n.Contains("006_semente_dos_tipos_de_conceito", StringComparison.Ordinal));
        using var leitor = new StreamReader(infra.GetManifestResourceStream(recurso)!);
        return leitor.ReadToEnd();
    }
}
