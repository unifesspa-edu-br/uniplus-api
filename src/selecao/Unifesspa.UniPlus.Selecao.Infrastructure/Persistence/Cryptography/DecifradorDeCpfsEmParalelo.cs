namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

using System.Text;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>Decifra os CPFs das inscrições em paralelo, preservando a ordem da entrada.</summary>
internal static class DecifradorDeCpfsEmParalelo
{
    private const int GrauDeParalelismo = 16;

    public static async Task<string[]> DecifrarAsync(
        IUniPlusEncryptionService encryption, IReadOnlyList<byte[]> cifrados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(cifrados);

        string[] cpfs = new string[cifrados.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, cifrados.Count),
            new ParallelOptions { MaxDegreeOfParallelism = GrauDeParalelismo, CancellationToken = cancellationToken },
            async (indice, ct) =>
            {
                byte[] plano = await encryption
                    .DecryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, cifrados[indice], ct)
                    .ConfigureAwait(false);
                cpfs[indice] = Encoding.UTF8.GetString(plano);
            }).ConfigureAwait(false);

        return cpfs;
    }
}
