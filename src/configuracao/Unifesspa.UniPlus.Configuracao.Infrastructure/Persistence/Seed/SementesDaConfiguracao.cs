namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            ILogger logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SementesDaConfiguracao));
            await AplicarAsync(contexto, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Aplica as sementes no <paramref name="contexto"/>, todas na mesma transação: ou o módulo fica com todas, ou com
    /// nenhuma. O termo e o modelo que o banco não grava, porque o identificador ou o código já existe ou porque o que o
    /// modelo cita não existe, ficam de fora e deixam uma linha no <paramref name="logger"/>; os demais entram.
    /// </summary>
    /// <param name="contexto">O contexto do módulo, apontado para o banco já migrado.</param>
    /// <param name="logger">Onde se registra o item que a semente não gravou.</param>
    /// <param name="cancellationToken">Token de cancelamento — opcional.</param>
    public static async Task AplicarAsync(UniPlusDbContext contexto, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(logger);

        IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transacao.ConfigureAwait(false))
        {
            await AplicacaoDosComandosDaSemente.ExecutarAsync(contexto, Comandos(), logger, cancellationToken).ConfigureAwait(false);
            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Aplica as sementes na transação que o <paramref name="contexto"/> já abriu, sem confirmá-la: quem a abriu a
    /// confirma ou a desfaz.
    /// </summary>
    internal static Task AplicarNaTransacaoAsync(UniPlusDbContext contexto, ILogger logger, CancellationToken cancellationToken) =>
        AplicacaoDosComandosDaSemente.ExecutarAsync(contexto, Comandos(), logger, cancellationToken);

    // A ordem respeita as chaves estrangeiras: o tipo, os termos e os fatos antes dos modelos que os citam.
    private static IReadOnlyList<ComandoDaSemente> Comandos() =>
    [
        .. SementePsrMedicina2027.ComandosDoTipoTermosEFatos(),
        .. SementePsrMedicina2027.ComandosDosModelos(),
        .. SementeIsencaoDaTaxa.Comandos(),
    ];
}
