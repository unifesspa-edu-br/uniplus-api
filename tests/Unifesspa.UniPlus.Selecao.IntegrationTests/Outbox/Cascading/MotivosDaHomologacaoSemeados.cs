namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A escolha dos motivos da homologação das inscrições que o processo semeado traz gravada: o motivo desfavorável de cada
/// condição da regra, no cadastro (um só por código, reaproveitado entre os processos) e escolhido na fase que homologa.
/// </summary>
internal static class MotivosDaHomologacaoSemeados
{
    internal static async Task EscolherAsync(UniPlusDbContext db, ProcessoSeletivo processo)
    {
        foreach ((FaseCronograma fase, string codigo) in MotivosDaHomologacaoDeTeste.MotivosExigidos(processo))
        {
            MotivoDeDecisao origem = await OrigemNoCadastroAsync(db, codigo);
            await db.MotivosDoProcesso.AddAsync(MotivosDaHomologacaoDeTeste.Escolher(processo, fase, origem));
        }
    }

    /// <summary>A identidade, no cadastro, do motivo de código informado, criado se ainda não existe.</summary>
    internal static async Task<Guid> NoCadastroAsync(IServiceProvider services, string codigo)
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return (await OrigemNoCadastroAsync(db, codigo)).Id;
    }

    private static async Task<MotivoDeDecisao> OrigemNoCadastroAsync(UniPlusDbContext db, string codigo)
    {
        MotivoDeDecisao nova = MotivosDaHomologacaoDeTeste.NovaOrigem(codigo);
        MotivoDeDecisao? existente = await db.MotivosDeDecisao.FirstOrDefaultAsync(m => m.Tipo == nova.Tipo && m.Codigo == nova.Codigo);
        if (existente is not null)
        {
            return existente;
        }

        await db.MotivosDeDecisao.AddAsync(nova);
        await db.SaveChangesAsync();

        return nova;
    }
}
