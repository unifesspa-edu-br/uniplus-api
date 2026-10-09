namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// O modelo de solicitação de isenção da taxa existe ao subir o sistema, gravado pela migration
/// <c>SemeiaModeloDeIsencaoDaTaxa</c>, para qualquer processo que cobre taxa.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class SementeIsencaoDaTaxaTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public SementeIsencaoDaTaxaTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Depois das migrations, o modelo de isenção é ativo, sem tipo de processo, e coleta ou pressupõe todo fato que a condição da Lei nº 12.799/2013 cita")]
    public async Task Migrations_GravamOModeloDeIsencao()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        ModeloFormulario modelo = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementeIsencaoDaTaxa.ModeloDeIsencao);

        modelo.Ativo.Should().BeTrue();
        modelo.Finalidade.Should().Be(FinalidadeFormulario.IsencaoTaxa);
        modelo.TipoProcessoCodigo.Should().BeNull("o modelo de isenção serve a qualquer processo que cobre taxa");
        modelo.Conteudo.Pressupostos.Should().BeEquivalentTo(IsencaoPorCarenciaSocioeconomica.FatosDaInscricao);
        modelo.Conteudo.Itens.Select(static i => i.FatoCodigo).Concat(modelo.Conteudo.Pressupostos)
            .Should().BeEquivalentTo(IsencaoPorCarenciaSocioeconomica.Condicao.FatosCitados, "a condição do direito é avaliável sobre as respostas que o modelo reúne");
        modelo.Conteudo.Termos.Select(static t => t.VersaoId).Should().BeEquivalentTo(SementeIsencaoDaTaxa.Termos.Select(static t => t.VersaoId));
    }

    [Fact(DisplayName = "Reaplicar a semente da isenção não duplica nem altera o que já existe")]
    public async Task Reaplicar_NaoDuplicaNemAltera()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        string antes = await RetratoAsync(ctx);
        antes.Should().NotBeEmpty("a migration já gravou os termos e o modelo");

        foreach (string comando in SementeIsencaoDaTaxa.Comandos())
        {
            // O conteúdo do modelo é JSON: as chaves escapadas não viram marcadores de parâmetro.
            await ctx.Database.ExecuteSqlRawAsync(comando.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal));
        }

        (await RetratoAsync(ctx)).Should().Be(antes);
    }

    /// <summary>A contagem e o conteúdo das linhas que a semente grava, para comparar antes e depois.</summary>
    private static async Task<string> RetratoAsync(ConfiguracaoDbContext ctx)
    {
        List<string> linhas = await ctx.Database.SqlQueryRaw<string>(
            """
            SELECT concat_ws('|', 'termo', nome, texto_rascunho) AS "Value" FROM configuracao.termo_consentimento WHERE id::text LIKE '15e0%'
            UNION ALL SELECT concat_ws('|', 'versao', hash) FROM configuracao.termo_consentimento_versao WHERE id::text LIKE '15e0%'
            UNION ALL SELECT concat_ws('|', 'modelo', codigo, ativo::text, conteudo::text) FROM configuracao.modelos_formulario WHERE id::text LIKE '15e0%'
            """).ToListAsync();
        return string.Join("\n", linhas.Order(StringComparer.Ordinal));
    }
}
