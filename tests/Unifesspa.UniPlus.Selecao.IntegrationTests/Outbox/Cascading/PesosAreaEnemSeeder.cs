namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Semeia no cadastro da Configuração uma resolução de Pesos por Área, para a classificação
/// baseada em ENEM com cálculo local declarar e congelar. A resolução é única por chamada: o
/// banco é compartilhado pela coleção, e a chave de negócio é o par (resolução, grupo). Devolve a
/// resolução na forma em que o cadastro a gravou.
/// </summary>
internal static class PesosAreaEnemSeeder
{
    public static readonly string[] TodosOsGrupos =
        [GrupoCurso.Tecnologica, GrupoCurso.HumanisticaI, GrupoCurso.HumanisticaII, GrupoCurso.SaudeEBiologicas];

    public static async Task<string> SemearResolucaoAsync(
        CascadingApiFactory api, IEnumerable<string>? grupos = null, string prefixo = "Res. E2E")
    {
        ArgumentNullException.ThrowIfNull(api);

        string resolucao = $"{prefixo} {Guid.NewGuid().ToString("N")[..12]}";
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        IEnumerable<PesoAreaEnem> linhas = (grupos ?? TodosOsGrupos).Select(grupo => PesoAreaEnem.Criar(
            resolucao,
            grupo,
            [
                new AreaInformada(PesoAreaEnem.CodigoRedacao, 2.00m, 400m),
                new AreaInformada(PesoAreaEnem.CodigoCienciasDaNatureza, 1.50m, null),
                new AreaInformada(PesoAreaEnem.CodigoCienciasHumanas, 2.50m, null),
                new AreaInformada(PesoAreaEnem.CodigoLinguagens, 2.50m, null),
                new AreaInformada(PesoAreaEnem.CodigoMatematica, 1.50m, null),
            ],
            "Resolução nº 805/2024/Consepe – Anexo I").Value!);
        await db.PesosAreaEnem.AddRangeAsync(linhas).ConfigureAwait(false);

        await db.SaveChangesAsync().ConfigureAwait(false);
        return PesoAreaEnem.NormalizarResolucao(resolucao)!;
    }
}
