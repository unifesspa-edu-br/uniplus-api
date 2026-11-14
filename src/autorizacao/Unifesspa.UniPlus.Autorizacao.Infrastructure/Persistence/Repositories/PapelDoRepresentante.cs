namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>A trava e a leitura do papel de Representante do controlador, sobre a linha do perfil de sistema dele.</summary>
public sealed class PapelDoRepresentante : IPapelDoRepresentante
{
    private readonly UniPlusDbContext _context;

    public PapelDoRepresentante(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public Task TravarAsync(CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoContribuicao.NomeDoEsquema}.perfis WHERE id = {{0}} FOR UPDATE",
            [PerfisDeSistema.RepresentanteDoControladorId],
            cancellationToken);

    public Task<bool> HaOutroVigenteAsync(Identidade? exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        exceto is null
            ? Vigentes(agora).AnyAsync(cancellationToken)
            : Vigentes(agora).AnyAsync(c => c.BeneficiarioEmissor != exceto.Emissor || c.BeneficiarioSub != exceto.Sub, cancellationToken);

    public async Task<IReadOnlySet<Identidade>> ListarVigentesAsync(DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var vigentes = await Vigentes(agora)
            .Select(c => new { c.BeneficiarioEmissor, c.BeneficiarioSub })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return vigentes.Select(static v => new Identidade(v.BeneficiarioEmissor, v.BeneficiarioSub)).ToHashSet();
    }

    public Task<bool> EVigenteAsync(Identidade pessoa, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return Vigentes(agora)
            .AnyAsync(c => c.BeneficiarioEmissor == pessoa.Emissor && c.BeneficiarioSub == pessoa.Sub, cancellationToken);
    }

    private IQueryable<Concessao> Vigentes(DateTimeOffset agora) =>
        _context.Concessoes
            .AsNoTracking()
            .VigentesDaInstituicao(agora)
            .Where(c => c.PerfilId == PerfisDeSistema.RepresentanteDoControladorId);
}
