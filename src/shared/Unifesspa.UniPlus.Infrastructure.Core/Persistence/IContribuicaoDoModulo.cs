namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// O que um módulo acrescenta ao modelo do contexto único: as suas configurações EF, as funções e as sequências do banco.
/// Todo tipo que a contribuição registra recebe o <see cref="Esquema"/> do módulo.
/// </summary>
public interface IContribuicaoDoModulo
{
    string Esquema { get; }

    void AplicarAoModelo(ModelBuilder modelBuilder);
}
