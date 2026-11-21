namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que as sementes gravaram no banco: as contagens e os códigos dos modelos, para comparar com o esperado.</summary>
internal sealed record ResumoDaSemente(int Termos, int Versoes, string Modelos, int Tipos, int Fatos);

/// <summary>
/// O que os testes da semente sobre banco real fazem com o banco da coleção, sempre dentro de uma transação que o teste
/// desfaz: apagar o que a semente gravou, cadastrar o que a administração cadastraria e conferir o que ficou.
/// </summary>
internal static class BancoDaSementeParaTeste
{
    public const string CodigoDoTipoDeProcesso = SementePsrMedicina2027.TipoProcessoCodigo;

    private static readonly DeclaracaoDaSemente Declaracao = new(
        "de teste", "semente-de-teste", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    public static readonly string[] CodigosDosModelos =
    [
        SementePsrMedicina2027.ModeloDeInscricaoSemProva, SementePsrMedicina2027.ModeloDeInscricaoComProva,
        SementePsrMedicina2027.ModeloDeHabilitacao, SementeIsencaoDaTaxa.ModeloDeIsencao,
    ];

    public static readonly Guid[] IdsDosTermos = [.. SementePsrMedicina2027.Termos.Concat(SementeIsencaoDaTaxa.Termos).Select(static t => t.TermoId)];

    public static readonly Guid[] IdsDasVersoes = [.. SementePsrMedicina2027.Termos.Concat(SementeIsencaoDaTaxa.Termos).Select(static t => t.VersaoId)];

    /// <summary>O código que o termo da semente tem depois de gravado, formado do nome pelo domínio.</summary>
    public static string CodigoDoTermo(TermoDaSemente termo) => SqlDaSemente.CodigoDoTermo(termo, Declaracao);

    public static async Task ApagarSementeAsync(UniPlusDbContext ctx)
    {
        await ctx.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM configuracao.modelos_formulario WHERE codigo = ANY({0});
            DELETE FROM configuracao.valores_de_dominio_do_fato WHERE id::text LIKE '5e3d%';
            DELETE FROM configuracao.fatos_do_candidato WHERE id::text LIKE '5e3d%';
            DELETE FROM configuracao.versoes_do_termo_consentimento WHERE id = ANY({1});
            DELETE FROM configuracao.termos_de_consentimento WHERE id = ANY({2});
            DELETE FROM configuracao.tipos_processo WHERE codigo = {3};
            """,
            CodigosDosModelos, IdsDasVersoes, IdsDosTermos, CodigoDoTipoDeProcesso);
    }

    public static Task AplicarAsync(UniPlusDbContext ctx, RegistroDeLogDeTeste log) =>
        SementesDaConfiguracao.AplicarNaTransacaoAsync(ctx, log, CancellationToken.None);

    public static async Task CadastrarTermoAsync(UniPlusDbContext ctx, Guid id, string codigo, string nome, bool removido) =>
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO configuracao.termos_de_consentimento
                (id, codigo, nome, forma_aceite_rascunho, revisado, is_deleted, deleted_at, deleted_by, created_at)
            VALUES ({id}, {codigo}, {nome}, 'REGISTRO_DIGITAL_COM_LOG_IP', FALSE, {removido},
                    CASE WHEN {removido} THEN now() END, CASE WHEN {removido} THEN 'administracao-de-teste' END, now())
            """);

    public static async Task CadastrarVersaoAsync(UniPlusDbContext ctx, Guid id, Guid termoId, int numero) =>
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO configuracao.versoes_do_termo_consentimento
                (id, termo_consentimento_id, numero, texto, base_legal, forma_aceite, hash, promovida_em, promovida_por)
            VALUES ({id}, {termoId}, {numero}, 'Texto fictício.', 'Base legal fictícia.', 'REGISTRO_DIGITAL_COM_LOG_IP',
                    repeat('a', 64), now(), 'administracao-de-teste')
            """);

    public static async Task<ResumoDaSemente> ResumoAsync(UniPlusDbContext ctx)
    {
        int termos = await ContarAsync(ctx, $"SELECT count(*)::int AS \"Value\" FROM configuracao.termos_de_consentimento WHERE id = ANY({IdsDosTermos})");
        int versoes = await ContarAsync(ctx, $"SELECT count(*)::int AS \"Value\" FROM configuracao.versoes_do_termo_consentimento WHERE id = ANY({IdsDasVersoes})");
        int tipos = await ContarAsync(ctx, $"SELECT count(*)::int AS \"Value\" FROM configuracao.tipos_processo WHERE codigo = {CodigoDoTipoDeProcesso}");
        int fatos = await ContarAsync(ctx, $"SELECT count(*)::int AS \"Value\" FROM configuracao.fatos_do_candidato WHERE id::text LIKE '5e3d%'");
        List<string> modelos = await ctx.Database.SqlQuery<string>(
            $"SELECT codigo AS \"Value\" FROM configuracao.modelos_formulario WHERE codigo = ANY({CodigosDosModelos})").ToListAsync();
        return new ResumoDaSemente(termos, versoes, string.Join(',', modelos.Order(StringComparer.Ordinal)), tipos, fatos);
    }

    /// <summary>O resumo do banco em que a semente gravou tudo: os termos, as versões, os modelos, o tipo PSR e os fatos.</summary>
    public static ResumoDaSemente ResumoCompleto { get; } = new(
        Termos: 5, Versoes: 5, Modelos: ModelosOrdenados(CodigosDosModelos), Tipos: 1, Fatos: 11);

    public static string ModelosOrdenados(IEnumerable<string> codigos) => string.Join(',', codigos.Order(StringComparer.Ordinal));

    /// <summary>
    /// Quantas vezes os modelos da semente citam um par termo e versão. Com <paramref name="soAusentes"/>, só as citações
    /// cujo termo não existe, cuja versão não é dele ou cujo termo foi removido, lidas do conteúdo gravado.
    /// </summary>
    public static async Task<int> CitacoesDosModelosAsync(UniPlusDbContext ctx, bool soAusentes) =>
        await ctx.Database.SqlQuery<int>(
            $"""
            SELECT count(*)::int AS "Value"
              FROM configuracao.modelos_formulario modelo
             CROSS JOIN LATERAL jsonb_array_elements(modelo.conteudo -> 'termos') AS citado
             WHERE modelo.codigo = ANY({CodigosDosModelos})
               AND (NOT {soAusentes} OR NOT EXISTS (
                    SELECT 1 FROM configuracao.termos_de_consentimento termo
                      JOIN configuracao.versoes_do_termo_consentimento versao ON versao.termo_consentimento_id = termo.id
                     WHERE termo.id = (citado ->> 'termoId')::uuid AND versao.id = (citado ->> 'versaoId')::uuid
                       AND NOT termo.is_deleted))
            """).SingleAsync();

    /// <summary>O conteúdo de todas as linhas que a semente grava, para comparar antes e depois.</summary>
    public static async Task<string> RetratoAsync(UniPlusDbContext ctx)
    {
        List<string> linhas = await ctx.Database.SqlQuery<string>(
            $"""
            SELECT 'termo ' || to_jsonb(t)::text AS "Value" FROM configuracao.termos_de_consentimento t WHERE t.id = ANY({IdsDosTermos})
            UNION ALL SELECT 'versao ' || to_jsonb(v)::text FROM configuracao.versoes_do_termo_consentimento v WHERE v.id = ANY({IdsDasVersoes})
            UNION ALL SELECT 'modelo ' || to_jsonb(m)::text FROM configuracao.modelos_formulario m WHERE m.codigo = ANY({CodigosDosModelos})
            UNION ALL SELECT 'tipo ' || to_jsonb(p)::text FROM configuracao.tipos_processo p WHERE p.codigo = {CodigoDoTipoDeProcesso}
            UNION ALL SELECT 'fato ' || to_jsonb(f)::text FROM configuracao.fatos_do_candidato f WHERE f.id::text LIKE '5e3d%'
            UNION ALL SELECT 'valor ' || to_jsonb(d)::text FROM configuracao.valores_de_dominio_do_fato d WHERE d.id::text LIKE '5e3d%'
            """).ToListAsync();
        return string.Join("\n", linhas.Order(StringComparer.Ordinal));
    }

    private static async Task<int> ContarAsync(UniPlusDbContext ctx, FormattableString consulta) =>
        await ctx.Database.SqlQuery<int>(consulta).SingleAsync();
}
