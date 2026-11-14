namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Provoca o conflito de concorrência otimista na gravação de uma linha: logo antes de a gravação do host sob teste
/// sair, outra conexão regrava a linha e troca o xmin que o host leu.
/// </summary>
internal sealed class ConflitoNaGravacaoDoHost
{
    private readonly Lock _trava = new();
    private (string Sql, object[] Parametros)? _regravacao;

    public void Armar(Guid documentoId) =>
        Armar("UPDATE autorizacao.documentos_da_versao SET updated_at = now() WHERE id = {0}", documentoId);

    /// <summary>Arma a instrução que regrava a linha na próxima gravação; os parâmetros seguem a sintaxe <c>{0}</c> do EF.</summary>
    public void Armar(string sql, params object[] parametros)
    {
        lock (_trava)
        {
            _regravacao = (sql, parametros);
        }
    }

    /// <summary>Instala a unidade de trabalho que provoca o conflito no lugar da registrada pelo módulo.</summary>
    public void Instalar(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ServiceDescriptor original = services.Single(static d => d.ServiceType == typeof(IUnitOfWork));
        services.Remove(original);
        services.AddScoped<IUnitOfWork>(sp => new UnidadeComConflito((IUnitOfWork)original.ImplementationFactory!(sp), this, sp));
    }

    private async Task ProvocarAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        (string Sql, object[] Parametros) regravacao;
        lock (_trava)
        {
            if (_regravacao is not { } armada)
            {
                return;
            }

            regravacao = armada;
            _regravacao = null;
        }

        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .ExecuteSqlRawAsync(regravacao.Sql, regravacao.Parametros, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class UnidadeComConflito(IUnitOfWork interna, ConflitoNaGravacaoDoHost conflito, IServiceProvider servicos)
        : IUnitOfWork
    {
        public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
        {
            await conflito.ProvocarAsync(servicos, cancellationToken).ConfigureAwait(false);

            return await interna.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        public void DescartarAlteracoesNaoSalvas() => interna.DescartarAlteracoesNaoSalvas();

        public Task ForcarChecagemImediataDeConstraintsAsync(CancellationToken cancellationToken = default) =>
            interna.ForcarChecagemImediataDeConstraintsAsync(cancellationToken);
    }
}
