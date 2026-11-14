namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O ponto único das sementes que o módulo grava por código, depois das migrations e dos scripts: o tipo de processo, os
/// termos, os fatos e os modelos do edital de Medicina 2027 e o modelo de isenção da taxa. Cada comando ignora a linha
/// que já existe, então reaplicar não duplica nem sobrescreve o que a administração editou. O que o modelo declara por
/// <c>HasData</c> não passa por aqui: entra na migration inicial.
/// </summary>
public static class SementesDaConfiguracao
{
    /// <summary>
    /// A etapa posterior do módulo: abre o próprio escopo e aplica as sementes sob a trava do banco que a subida mantém.
    /// </summary>
    /// <param name="services">O provider raiz.</param>
    /// <param name="cancellationToken">Token de cancelamento — opcional.</param>
    public static async Task AplicarAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        AsyncServiceScope escopo = services.CreateAsyncScope();
        await using (escopo.ConfigureAwait(false))
        {
            UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            await AplicarAsync(contexto, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Aplica as sementes no <paramref name="contexto"/>, todas na mesma transação: ou o módulo fica com todas, ou com
    /// nenhuma.
    /// </summary>
    /// <param name="contexto">O contexto do módulo, apontado para o banco já migrado.</param>
    /// <param name="cancellationToken">Token de cancelamento — opcional.</param>
    public static async Task AplicarAsync(UniPlusDbContext contexto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transacao.ConfigureAwait(false))
        {
            foreach (string comando in Comandos())
            {
                await ExecutarAsync(contexto, transacao, comando, cancellationToken).ConfigureAwait(false);
            }

            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // A ordem respeita as chaves estrangeiras: o tipo, os termos e os fatos antes dos modelos que os citam.
    private static IEnumerable<string> Comandos() =>
    [
        .. SementePsrMedicina2027.ComandosDoTipoTermosEFatos(),
        .. SementePsrMedicina2027.ComandosDosModelos(),
        .. SementeIsencaoDaTaxa.Comandos(),
    ];

    // O comando vai como texto, sem formatação: o conteúdo dos modelos é JSON, e as chaves dele não são marcadores.
    private static async Task ExecutarAsync(
        UniPlusDbContext contexto, IDbContextTransaction transacao, string comando, CancellationToken cancellationToken)
    {
        DbCommand execucao = contexto.Database.GetDbConnection().CreateCommand();
        await using (execucao.ConfigureAwait(false))
        {
            execucao.CommandText = comando;
            execucao.Transaction = transacao.GetDbTransaction();
            await execucao.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
