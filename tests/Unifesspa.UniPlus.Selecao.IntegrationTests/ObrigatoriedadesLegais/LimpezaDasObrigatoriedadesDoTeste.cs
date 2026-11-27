namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Apaga de vez as regras que o teste semeou para os tipos de processo informados, com o histórico de cada uma, que a chave
/// estrangeira não deixa para trás.
/// </summary>
internal static class LimpezaDasObrigatoriedadesDoTeste
{
    internal static async Task ApagarDosTiposAsync(UniPlusDbContext db, string[] tipos)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tipos);

        await db.Database.ExecuteSqlAsync(
            $"DELETE FROM selecao.historicos_da_obrigatoriedade WHERE regra_id IN (SELECT id FROM selecao.obrigatoriedades_legais WHERE tipo_processo_codigo = ANY({tipos}))");
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.obrigatoriedades_legais WHERE tipo_processo_codigo = ANY({tipos})");
    }
}
