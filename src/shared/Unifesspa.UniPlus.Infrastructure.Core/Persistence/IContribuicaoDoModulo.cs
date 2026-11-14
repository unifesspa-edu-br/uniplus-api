namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// O que um módulo acrescenta ao contexto único: as suas configurações EF, as funções e as sequências do banco, os
/// interceptadores e a etapa que roda depois das migrations. Todo tipo que a contribuição registra no modelo recebe o
/// <see cref="Esquema"/> do módulo.
/// </summary>
public interface IContribuicaoDoModulo
{
    string Esquema { get; }

    void AplicarAoModelo(ModelBuilder modelBuilder);

    /// <summary>
    /// Acrescenta os interceptadores do módulo às opções do contexto. É chamado depois dos interceptadores
    /// compartilhados e na ordem dos módulos, que é a ordem em que eles rodam.
    /// </summary>
    void AdicionarInterceptadores(DbContextOptionsBuilder options, IServiceProvider servicos)
    {
    }

    /// <summary>
    /// Roda depois que as migrations do contexto foram aplicadas, no mesmo lugar em que elas rodam. A falha é a falha
    /// da migration.
    /// </summary>
    Task ExecutarEtapaPosteriorDaMigrationAsync(IServiceProvider servicos, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
