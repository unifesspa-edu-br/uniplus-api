namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>As fases e etapas semeadas para o processo de teste e as rotas em que os motivos são escolhidos nelas.</summary>
internal static class LugaresDoProcessoDeTeste
{
    private const string Rota = "/api/selecao/processos-seletivos";

    /// <summary>A fase canônica de origem da fase de código informado.</summary>
    internal static async Task<Guid> FaseAsync(IServiceProvider services, Guid processoId, string codigo = "RESULTADO_FINAL")
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        FaseCronograma fase = await db.FasesCronograma.AsNoTracking().SingleAsync(f => f.ProcessoSeletivoId == processoId && f.Codigo == codigo);

        return fase.FaseCanonicaOrigemId;
    }

    /// <summary>A identidade, no catálogo canônico, da fase de código informado.</summary>
    internal static async Task<Guid> FaseDoCatalogoAsync(IServiceProvider services, string codigo)
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<FaseCanonica> vivas = await db.FasesCanonicas.AsNoTracking().ToListAsync();

        return vivas.Single(f => string.Equals(f.Codigo.Valor, codigo, StringComparison.Ordinal)).Id;
    }

    /// <summary>A identidade da etapa de nome informado.</summary>
    internal static async Task<Guid> EtapaAsync(IServiceProvider services, Guid processoId, string nome = "Prova Objetiva")
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.EtapasProcesso.AsNoTracking().Where(e => e.ProcessoSeletivoId == processoId && e.Nome == nome).Select(e => e.Id).SingleAsync();
    }

    internal static string RotaDaFase(Guid processoId, Guid faseCanonicaOrigemId, Guid motivoId) =>
        $"{Rota}/{processoId}/fases/{faseCanonicaOrigemId}/motivos-de-decisao/{motivoId}";

    internal static string RotaDaEtapa(Guid processoId, Guid etapaId, Guid motivoId) =>
        $"{Rota}/{processoId}/etapas/{etapaId}/motivos-de-decisao/{motivoId}";

    internal static string RotaDaLeitura(Guid processoId, int? versao = null) =>
        $"{Rota}/{processoId}/motivos-de-decisao" + (versao is { } numero ? $"?versao={numero}" : string.Empty);
}
