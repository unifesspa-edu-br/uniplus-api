namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using System.Text;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Implementação de <see cref="ITermoConsentimentoReader"/> (ADR-0056). A junção com o termo aplica
/// o filtro de exclusão lógica dele: versão de termo removido não é oferecida a vínculo novo.
/// </summary>
internal sealed class TermoConsentimentoReader : ITermoConsentimentoReader
{
    private readonly UniPlusDbContext _dbContext;

    public TermoConsentimentoReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<VersaoTermoConsentimentoView>> ListarVersoesAsync(
        IReadOnlyCollection<Guid> versaoIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versaoIds);
        if (versaoIds.Count == 0)
        {
            return [];
        }

        List<LinhaDaVersao> linhas = await Juntar(
                _dbContext.VersoesTermoConsentimento.AsNoTracking().Where(v => versaoIds.Contains(v.Id)),
                _dbContext.TermosConsentimento.AsNoTracking())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. linhas.Select(static l => ParaVisao(l))];
    }

    public async Task<VersaoTermoConsentimentoView?> ObterVersaoPorCodigoENumeroAsync(
        string codigo, int numero, CancellationToken cancellationToken)
    {
        Result<CodigoTermoConsentimento> codigoValido = CodigoTermoConsentimento.Criar(codigo?.Normalize(NormalizationForm.FormC));
        if (codigoValido.IsFailure)
        {
            return null;
        }

        CodigoTermoConsentimento procurado = codigoValido.Value!;
        LinhaDaVersao? linha = await Juntar(
                _dbContext.VersoesTermoConsentimento.AsNoTracking().Where(v => v.Numero == numero),
                _dbContext.TermosConsentimento.AsNoTracking().Where(t => t.Codigo == procurado))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return linha is null ? null : ParaVisao(linha);
    }

    private static IQueryable<LinhaDaVersao> Juntar(IQueryable<TermoConsentimentoVersao> versoes, IQueryable<TermoConsentimento> termos) =>
        versoes.Join(termos, v => v.TermoConsentimentoId, t => t.Id, (v, t) => new LinhaDaVersao(
            t.Id, v.Id, t.Codigo, v.Numero, t.Nome, v.Texto, v.BaseLegal, v.FormaAceite, v.Hash));

    private static VersaoTermoConsentimentoView ParaVisao(LinhaDaVersao l) => new(
        l.TermoId,
        l.VersaoId,
        l.Codigo.Valor,
        l.Numero,
        l.Nome,
        l.Texto,
        l.BaseLegal,
        l.FormaAceite,
        l.Hash);

    private sealed record LinhaDaVersao(
        Guid TermoId, Guid VersaoId, CodigoTermoConsentimento Codigo, int Numero, string Nome, string Texto, string BaseLegal, FormaAceite FormaAceite, string Hash);
}
