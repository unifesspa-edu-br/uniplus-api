namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

public sealed class LoteDeNotasDoEnemRepository : ILoteDeNotasDoEnemRepository
{
    private static readonly string[] OrdemDasAreas = [.. AreaDoEnem.Todas];

    private readonly UniPlusDbContext _context;
    private readonly IUniPlusEncryptionService _encryption;

    public LoteDeNotasDoEnemRepository(UniPlusDbContext context, IUniPlusEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(encryption);
        _context = context;
        _encryption = encryption;
    }

    public async Task AdicionarAsync(LoteDeNotasDoEnem lote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lote);
        await _context.LotesDeNotasDoEnem.AddAsync(lote, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoteDeNotasDoEnem?> ObterVigenteAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default)
    {
        return await DoEscopo(processoSeletivoId, etapaProcessoId, edicaoCodigo)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LoteDeNotasDoEnem?> ObterComOcorrenciasAsync(
        Guid processoSeletivoId,
        Guid etapaProcessoId,
        string edicaoCodigo,
        Guid loteId,
        CancellationToken cancellationToken = default)
    {
        return await DoEscopo(processoSeletivoId, etapaProcessoId, edicaoCodigo)
            .Include(l => l.Ocorrencias)
            .Where(l => l.Id == loteId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LoteDeNotasDoEnem>> ListarAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default)
    {
        return await DoEscopo(processoSeletivoId, etapaProcessoId, edicaoCodigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, InscritoDoLote>> ObterInscritosAsync(
        IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricaoIds);

        Guid[] ids = [.. inscricaoIds];
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, InscritoDoLote>();
        }

        return await MontarInscritosAsync(ids, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ObterAusentesDoLoteAsync(
        Guid loteId, CancellationToken cancellationToken = default)
    {
        var ausentes = await _context.LancamentosDeNota
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.Situacao == SituacaoDaProvaNoEnem.Ausente)
            .Select(l => new { l.InscricaoId, l.ItemCodigo })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ausentes
            .GroupBy(static a => a.InscricaoId)
            .ToDictionary(
                static g => g.Key,
                static g => (IReadOnlyList<string>)[.. g.Select(static a => a.ItemCodigo).Distinct().OrderBy(static c => IndiceDaArea(c))]);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> ContarAusentesPorLoteAsync(
        IReadOnlyCollection<Guid> loteIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loteIds);

        Guid[] ids = [.. loteIds];
        var contagens = await _context.LancamentosDeNota
            .AsNoTracking()
            .Where(l => ids.Contains(l.LoteId) && l.Situacao == SituacaoDaProvaNoEnem.Ausente)
            .GroupBy(l => l.LoteId)
            .Select(g => new { LoteId = g.Key, Total = g.Select(l => l.InscricaoId).Distinct().Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return contagens.ToDictionary(static c => c.LoteId, static c => c.Total);
    }

    private async Task<IReadOnlyDictionary<Guid, InscritoDoLote>> MontarInscritosAsync(
        Guid[] inscricaoIds, CancellationToken cancellationToken)
    {
        List<InscricaoDoLote> inscricoes = await ListarInscricoesAsync(inscricaoIds, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, string> nomesDeExibicao = await NomeDeExibicaoDaInscricao
            .LerAsync(_context.RespostasDaInscricao, inscricaoIds, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, string> cpfs = await CpfDaInscricao
            .LerAsync(_context.RespostasDaInscricao.Where(r => inscricaoIds.Contains(r.InscricaoId)), _encryption, cancellationToken)
            .ConfigureAwait(false);

        return inscricoes.ToDictionary(
            static i => i.Id,
            i => i.ComoInscrito(nomesDeExibicao.GetValueOrDefault(i.Id), CpfDaInscricao.Exigir(cpfs, i.Id)));
    }

    private Task<List<InscricaoDoLote>> ListarInscricoesAsync(Guid[] inscricaoIds, CancellationToken cancellationToken) =>
        _context.Inscricoes
            .AsNoTracking()
            .Where(i => inscricaoIds.Contains(i.Id))
            .Select(i => new InscricaoDoLote(
                i.Id, i.NumeroInscricao, i.VersaoFormularioId, i.OfertaCursoId, i.OfertaCursoSegundaOpcaoId))
            .ToListAsync(cancellationToken);

    private static int IndiceDaArea(string codigo) => Array.IndexOf(OrdemDasAreas, codigo);

    /// <summary>Os lotes do escopo, sem rastreamento, do mais recente para o mais antigo.</summary>
    private IQueryable<LoteDeNotasDoEnem> DoEscopo(Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo) =>
        _context.LotesDeNotasDoEnem
            .AsNoTracking()
            .Where(l => l.ProcessoSeletivoId == processoSeletivoId
                && l.EtapaProcessoId == etapaProcessoId
                && l.EdicaoCodigo == edicaoCodigo)
            .OrderByDescending(l => l.ImportadoEm)
            .ThenByDescending(l => EF.Property<long>(l, LoteDeNotasDoEnemConfiguration.OrdemDeRegistro));

    private sealed record InscricaoDoLote(
        Guid Id, string? NumeroInscricao, Guid VersaoFormularioId, Guid? OfertaCursoId, Guid? OfertaCursoSegundaOpcaoId)
    {
        public InscritoDoLote ComoInscrito(string? nome, string cpf) =>
            new(NumeroInscricao, nome, Cpf.Mascarar(cpf), VersaoFormularioId, OfertaCursoId, OfertaCursoSegundaOpcaoId);
    }
}
