namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// A regra única do nome que a Seleção exibe de uma inscrição a quem a opera (ADR-0082): o nome social, quando
/// preenchido, vale no lugar do civil; o texto sai sem espaços nas pontas; a inscrição sem nome algum fica sem nome.
/// Lê só as respostas de texto do nome, que não são cifradas, sem hidratar a inscrição.
/// </summary>
/// <remarks>
/// Conta apenas a resposta <b>informada</b> pelo candidato, na ocorrência raiz. O nome exibido é o que a pessoa declarou, e
/// uma resposta derivada pelo sistema com o mesmo código de fato nunca deve se sobrepor a ela; nenhum dos dois fatos é
/// derivado hoje, então o filtro só fixa a invariante, no lugar de deixar cada leitor decidir.
/// </remarks>
internal static class NomeDeExibicaoDaInscricao
{
    private static readonly string[] Fatos = [ConjuntoBasicoDaInscricao.Nome, ConjuntoBasicoDaInscricao.NomeSocial];

    /// <summary>O nome de exibição de cada inscrição de <paramref name="ids"/> que tem nome; as demais não aparecem.</summary>
    public static async Task<Dictionary<Guid, string>> LerAsync(
        IQueryable<RespostaDaInscricaoRecord> respostas, Guid[] ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(ids);

        var linhas = await respostas
            .AsNoTracking()
            .Where(r => ids.Contains(r.InscricaoId)
                && r.OcorrenciaId == null
                && r.Origem == OrigemDaResposta.Informada
                && r.ValorTexto != null
                && Fatos.Contains(r.FatoCodigo))
            .Select(static r => new { r.InscricaoId, r.FatoCodigo, Texto = r.ValorTexto! })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, string> nomes = [];
        foreach (var porInscricao in linhas.GroupBy(static l => l.InscricaoId))
        {
            string? social = TextoDe(porInscricao.Where(static l => l.FatoCodigo == ConjuntoBasicoDaInscricao.NomeSocial).Select(static l => l.Texto));
            string? civil = TextoDe(porInscricao.Where(static l => l.FatoCodigo == ConjuntoBasicoDaInscricao.Nome).Select(static l => l.Texto));
            if ((social ?? civil) is { } nome)
            {
                nomes[porInscricao.Key] = nome;
            }
        }

        return nomes;
    }

    private static string? TextoDe(IEnumerable<string> textos) =>
        textos.Select(static t => t.Trim()).FirstOrDefault(static t => t.Length > 0);
}
