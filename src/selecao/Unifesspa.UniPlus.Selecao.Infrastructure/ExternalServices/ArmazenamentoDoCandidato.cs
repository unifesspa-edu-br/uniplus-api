namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;

/// <summary>
/// O <see cref="IStorageService"/> que opera com a credencial do candidato. É um tipo próprio, e não um segundo
/// registro de <see cref="IStorageService"/>, para que nenhum consumidor por tipo resolva a credencial restrita
/// no lugar da geral.
/// </summary>
public sealed class ArmazenamentoDoCandidato
{
    public ArmazenamentoDoCandidato(IStorageService storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        Storage = storage;
    }

    public IStorageService Storage { get; }
}
