namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Os comprovantes de cada inscrição na ordem de emissão, lidos um a um pelo número, como a consulta os lê.</summary>
internal static class ComprovantesDeTeste
{
    public static async Task<IReadOnlyList<ComprovanteInscricao>> ObterComprovantesAsync(
        this IInscricaoRepository repositorio, IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default)
    {
        List<ComprovanteInscricao> comprovantes = [];
        foreach (Guid inscricao in inscricoes)
        {
            int total = (await repositorio.ObterEmissoesAsync([inscricao], cancellationToken).ConfigureAwait(false)).Count;
            for (int numero = 1; numero <= total; numero++)
            {
                comprovantes.Add((await repositorio.ObterComprovanteAsync(inscricao, numero, cancellationToken).ConfigureAwait(false))!.Comprovante);
            }
        }

        return comprovantes;
    }
}
