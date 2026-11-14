namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O que a Autorização acrescenta ao contexto único: as configurações, a função da ordem da classificação, a sequência
/// da trilha de contenção, o interceptador dessa trilha e a conciliação do catálogo depois das migrations.
/// </summary>
public sealed class AutorizacaoContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "autorizacao";

    public const string SequenciaDaOrdemDaTrilha = "ordem_da_trilha_de_contencao";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutorizacaoContribuicao).Assembly);
        modelBuilder.HasDbFunction(FuncoesDoBanco.MetodoDaOrdemDaClassificacao);
        modelBuilder.HasSequence<long>(SequenciaDaOrdemDaTrilha, NomeDoEsquema).StartsAt(1).IncrementsBy(1);
    }

    public void AdicionarInterceptadores(DbContextOptionsBuilder options, IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(servicos);

        options.AddInterceptors(servicos.GetRequiredService<OrdemDaTrilhaDeContencaoInterceptor>());
    }

    public async Task ExecutarEtapaPosteriorDaMigrationAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        AsyncServiceScope escopo = servicos.CreateAsyncScope();
        await using (escopo.ConfigureAwait(false))
        {
            await escopo.ServiceProvider.GetRequiredService<ConciliacaoDoCatalogo>()
                .ExecutarAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
