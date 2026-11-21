namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Uma relação de precedência entre duas fases do catálogo, cadastrada pelo teste no cadastro vivo e retirada ao fim dele: o banco da
/// coleção é compartilhado, e a relação que sobrasse mudaria a conferência dos outros testes.
/// </summary>
internal sealed class PrecedenciaCadastradaNoTeste : IAsyncDisposable
{
    private readonly IServiceProvider _servicos;
    private readonly Guid _id;
    private bool _retirada;

    private PrecedenciaCadastradaNoTeste(IServiceProvider servicos, Guid id)
    {
        _servicos = servicos;
        _id = id;
    }

    internal static async Task<PrecedenciaCadastradaNoTeste> CadastrarAsync(IServiceProvider servicos, string antecessora, string sucessora)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<PrecedenciaFase> vivas = await db.PrecedenciasFase.AsNoTracking().ToListAsync();
        PrecedenciaFase relacao = PrecedenciaFase.Criar(antecessora, sucessora, permiteSobreposicao: false, vivas).Value!;
        db.PrecedenciasFase.Add(relacao);
        await db.SaveChangesAsync();

        return new PrecedenciaCadastradaNoTeste(servicos, relacao.Id);
    }

    /// <summary>Retira a relação do cadastro; chamar de novo não faz nada, para a retirada também caber no fim do teste.</summary>
    internal async Task RetirarAsync()
    {
        if (_retirada)
        {
            return;
        }

        _retirada = true;
        await using AsyncServiceScope escopo = _servicos.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.PrecedenciasFase.Remove(await db.PrecedenciasFase.SingleAsync(relacao => relacao.Id == _id));
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await RetirarAsync();
}
