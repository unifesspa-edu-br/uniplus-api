namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A junção <c>tipos_etapa_conceitos</c> contra Postgres real: os pares gravam e voltam pelo código, a edição
/// substitui o conjunto, apagar o tipo de etapa leva os pares e o tipo de conceito com par não pode ser apagado.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TipoEtapaConceitosAdmitidosPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public TipoEtapaConceitosAdmitidosPersistenceTests(ConfiguracaoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Os conceitos admitidos gravam um par por conceito e voltam ordenados pelo código")]
    public async Task Admitidos_GravamUmParPorConceito()
    {
        string prefixo = TiposConceitoHttpDeTeste.CodigoUnico();
        TipoConceito parecer = Conceito($"{prefixo}_PARECER");
        TipoConceito nota = Conceito($"{prefixo}_NOTA");
        TipoEtapa tipo = await GravarTipoAsync(parecer, nota);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        (await CodigosAdmitidosAsync(leitura, tipo.Id)).Should().Equal(nota.Codigo, parecer.Codigo);
        (await ContarParesAsync(leitura, tipo.Id)).Should().Be(2);
    }

    [Fact(DisplayName = "Substituir o conjunto troca os pares gravados, sem acumular os anteriores")]
    public async Task Substituir_TrocaOsPares()
    {
        TipoConceito anterior = Conceito(TiposConceitoHttpDeTeste.CodigoUnico());
        TipoConceito novo = Conceito(TiposConceitoHttpDeTeste.CodigoUnico());
        TipoEtapa tipo = await GravarTipoAsync(anterior, novo);
        await SubstituirPorAsync(tipo.Id, novo.Id);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        (await CodigosAdmitidosAsync(leitura, tipo.Id)).Should().Equal(novo.Codigo);
        (await ContarParesAsync(leitura, tipo.Id)).Should().Be(1);
    }

    [Fact(DisplayName = "Apagar o tipo de etapa leva os pares e preserva os tipos de conceito")]
    public async Task ApagarOTipoDeEtapa_LevaOsPares()
    {
        TipoConceito nota = Conceito(TiposConceitoHttpDeTeste.CodigoUnico());
        TipoEtapa tipo = await GravarTipoAsync(nota);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.TiposEtapa.Remove(await ctx.TiposEtapa.SingleAsync(t => t.Id == tipo.Id));
        await ctx.SaveChangesAsync();

        (await ContarParesAsync(ctx, tipo.Id)).Should().Be(0);
        (await ctx.Set<TipoConceito>().AnyAsync(c => c.Id == nota.Id)).Should().BeTrue();
    }

    [Fact(DisplayName = "O banco recusa apagar um tipo de conceito que algum tipo de etapa admite")]
    public async Task ApagarConceitoAdmitido_ViolaAChaveEstrangeira()
    {
        TipoConceito nota = Conceito(TiposConceitoHttpDeTeste.CodigoUnico());
        await GravarTipoAsync(nota);
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"DELETE FROM configuracao.tipos_conceito WHERE id = {nota.Id}");

        PostgresException erro = (await act.Should().ThrowAsync<PostgresException>()).Which;
        erro.SqlState.Should().Be(PostgresErrorCodes.RestrictViolation, "ON DELETE RESTRICT recusa o delete: o tipo de conceito se desativa, não se apaga");
    }

    private static TipoConceito Conceito(string codigo) => TipoConceito.Criar(
        codigo,
        new DefinicaoInformada("Nota da entrevista", null, "numerico", new EscalaInformada(0m, 10m, 2), null)).Value!;

    private async Task<TipoEtapa> GravarTipoAsync(params TipoConceito[] conceitos)
    {
        TipoEtapa tipo = TipoEtapa.Criar(TiposConceitoHttpDeTeste.CodigoUnico(), "Etapa avaliada por conceito", null, true, true).Value!;
        tipo.SubstituirConceitosAdmitidos(conceitos);
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.TiposEtapa.Add(tipo);
        await ctx.SaveChangesAsync();
        return tipo;
    }

    private async Task SubstituirPorAsync(Guid tipoEtapaId, Guid tipoConceitoId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        TipoEtapa tipo = await ctx.TiposEtapa.Include(t => t.ConceitosAdmitidos).SingleAsync(t => t.Id == tipoEtapaId);
        tipo.SubstituirConceitosAdmitidos([await ctx.Set<TipoConceito>().SingleAsync(c => c.Id == tipoConceitoId)]);
        await ctx.SaveChangesAsync();
    }

    private static async Task<string[]> CodigosAdmitidosAsync(UniPlusDbContext ctx, Guid tipoEtapaId)
    {
        TipoEtapa tipo = await ctx.TiposEtapa.Include(t => t.ConceitosAdmitidos).SingleAsync(t => t.Id == tipoEtapaId);
        return [.. tipo.ConceitosAdmitidos.Select(conceito => conceito.Codigo)];
    }

    private static async Task<int> ContarParesAsync(UniPlusDbContext ctx, Guid tipoEtapaId) =>
        await ctx.Database.SqlQuery<int>(
            $"""SELECT count(*)::int AS "Value" FROM configuracao.tipos_etapa_conceitos WHERE tipo_etapa_id = {tipoEtapaId}""")
            .SingleAsync();
}
