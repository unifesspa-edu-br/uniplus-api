namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// As linhas filhas de uma ou mais inscrições (ADR-0141): respostas, endereços, ocorrências de grupo e aceites, uma linha
/// por valor, lidas do banco ou montadas a partir do que a inscrição declara.
/// </summary>
internal sealed record LinhasDaInscricao(
    List<RespostaDaInscricaoRecord> Respostas,
    List<EnderecoDaInscricaoRecord> Enderecos,
    List<OcorrenciaDeGrupoDaInscricaoRecord> Ocorrencias,
    List<AceiteDaInscricaoRecord> Aceites)
{
    public static LinhasDaInscricao Vazias => new([], [], [], []);

    /// <summary>
    /// Carrega as linhas atuais com rastreamento: a sincronização só funciona porque a recarga devolve as mesmas
    /// instâncias que o <c>ChangeTracker</c> já conhece.
    /// </summary>
    public static async Task<LinhasDaInscricao> CarregarAsync(
        UniPlusDbContext dbContext, IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        List<RespostaDaInscricaoRecord> respostas = await dbContext.RespostasDaInscricao
            .Where(r => inscricoes.Contains(r.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<EnderecoDaInscricaoRecord> enderecos = await dbContext.EnderecosDaInscricao
            .Where(e => inscricoes.Contains(e.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<OcorrenciaDeGrupoDaInscricaoRecord> ocorrencias = await dbContext.OcorrenciasDeGrupoDaInscricao
            .Where(o => inscricoes.Contains(o.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<AceiteDaInscricaoRecord> aceites = await dbContext.AceitesDaInscricao
            .Where(a => inscricoes.Contains(a.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new LinhasDaInscricao(respostas, enderecos, ocorrencias, aceites);
    }

    public LinhasDaInscricao Da(Guid inscricaoId) => new(
        [.. Respostas.Where(r => r.InscricaoId == inscricaoId)],
        [.. Enderecos.Where(e => e.InscricaoId == inscricaoId)],
        [.. Ocorrencias.Where(o => o.InscricaoId == inscricaoId)],
        [.. Aceites.Where(a => a.InscricaoId == inscricaoId)]);
}
