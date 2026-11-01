namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// O tipo de documento e o fato gravados direto no catálogo nascem pendentes de aprovação da proteção de dados
/// (ADR-0153), e o processo só publica proteção aprovada. Os testes de Seleção que montam o catálogo à mão o dão por
/// aprovado, como depois da conferência da proteção de dados.
/// </summary>
internal static class CatalogoAprovadoDeTeste
{
    public static async Task AprovarCatalogoAsync(this ConfiguracaoDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        await db.Database.ExecuteSqlRawAsync("UPDATE configuracao.rol_de_fatos_candidato SET protecao_situacao = 'APROVADA'");
        await db.Database.ExecuteSqlRawAsync("UPDATE configuracao.tipo_documento SET protecao_situacao = 'APROVADA'");
    }
}
