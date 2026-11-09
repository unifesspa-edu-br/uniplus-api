namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

/// <summary>
/// Provoca o conflito de concorrência otimista na gravação do documento de uma versão: logo antes de a gravação do
/// host sob teste sair, outra conexão regrava a linha do documento e troca o xmin que o host leu.
/// </summary>
internal sealed class ConflitoNaGravacaoDoHost
{
    private readonly Lock _trava = new();
    private Guid? _documento;

    public void Armar(Guid documentoId)
    {
        lock (_trava)
        {
            _documento = documentoId;
        }
    }

    /// <summary>Instala a unidade de trabalho que provoca o conflito no lugar da registrada pelo módulo.</summary>
    public void Instalar(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ServiceDescriptor original = services.Single(static d => d.ServiceType == typeof(IAutorizacaoUnitOfWork));
        services.Remove(original);
        services.AddScoped<IAutorizacaoUnitOfWork>(sp => new UnidadeComConflito((IAutorizacaoUnitOfWork)original.ImplementationFactory!(sp), this, sp));
    }

    private async Task ProvocarAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        Guid documentoId;
        lock (_trava)
        {
            if (_documento is not { } armado)
            {
                return;
            }

            documentoId = armado;
            _documento = null;
        }

        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .ExecuteSqlRawAsync("UPDATE autorizacao.documentos_da_versao SET updated_at = now() WHERE id = {0}", [documentoId], cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class UnidadeComConflito(IAutorizacaoUnitOfWork interna, ConflitoNaGravacaoDoHost conflito, IServiceProvider servicos)
        : IAutorizacaoUnitOfWork
    {
        public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
        {
            await conflito.ProvocarAsync(servicos, cancellationToken).ConfigureAwait(false);

            return await interna.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        public void DescartarAlteracoesNaoSalvas() => interna.DescartarAlteracoesNaoSalvas();
    }
}
