namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Gravação, SQL cru e leitura de valores avulsos contra o Postgres real, compartilhados pelas classes de
/// teste de persistência do ato normativo.
/// </summary>
internal sealed class AtoNormativoPersistenciaApoio(PublicacoesDbFixture fixture)
{
    internal async Task GravarAsync(params AtoNormativo[] atos)
    {
        foreach (AtoNormativo ato in atos)
        {
            await using UniPlusDbContext ctx = fixture.CreateDbContext(userId: null);
            ctx.AtosNormativos.Add(ato);
            await ctx.SaveChangesAsync();
        }
    }

    internal async Task ExecutarAsync(FormattableString sql)
    {
        await using UniPlusDbContext ctx = fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(sql);
    }

    /// <summary>Lê o valor único que a consulta devolve na coluna <c>"Value"</c>.</summary>
    internal async Task<T> LerValorAsync<T>(FormattableString sql)
    {
        await using UniPlusDbContext ctx = fixture.CreateDbContext(userId: null);
        return await ctx.Database.SqlQuery<T>(sql).SingleAsync();
    }

    internal async Task InserirCruAsync(LinhaCruaDoAto linha)
    {
        ArgumentNullException.ThrowIfNull(linha);

        await ExecutarAsync(
            $"""
            INSERT INTO publicacoes.atos_normativos
                (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
                 unico_por_objeto, data_do_ato, documento_hash, assinante, publicado_em, versao_do_objeto_id, versao_do_objeto_hash,
                 ato_retificado_id, motivo_retificacao)
            VALUES
                ({linha.Id}, 'CEPS', 'EDITAL', {linha.Ano}, '13', 'EDITAL_ABERTURA', false, false,
                 false, {linha.DataDoAto}, {linha.DocumentoHash}, 'Assinante', now(), {linha.VersaoId}, {linha.VersaoHash},
                 {linha.AtoRetificadoId}, {linha.MotivoRetificacao})
            """);
    }
}
