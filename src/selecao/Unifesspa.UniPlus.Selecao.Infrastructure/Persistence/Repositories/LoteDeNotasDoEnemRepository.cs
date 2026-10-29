namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

public sealed class LoteDeNotasDoEnemRepository : ILoteDeNotasDoEnemRepository
{
    private static readonly string[] OrdemDasAreas = [.. AreaDoEnem.Todas];

    private readonly SelecaoDbContext _context;
    private readonly IUniPlusEncryptionService _encryption;

    public LoteDeNotasDoEnemRepository(SelecaoDbContext context, IUniPlusEncryptionService encryption)
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

        var inscricoes = await _context.Inscricoes
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new
            {
                i.Id,
                i.NumeroInscricao,
                i.CpfCiphertext,
                i.VersaoFormularioId,
                i.OfertaCursoId,
                i.OfertaCursoSegundaOpcaoId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, string> nomeDeExibicao = await NomeDeExibicaoDaInscricao
            .LerAsync(_context.RespostasDaInscricao, ids, cancellationToken)
            .ConfigureAwait(false);

        string[] cpfs = await DecifradorDeCpfsEmParalelo
            .DecifrarAsync(_encryption, [.. inscricoes.Select(static i => i.CpfCiphertext)], cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, InscritoDoLote> resultado = [];
        for (int i = 0; i < inscricoes.Count; i++)
        {
            var inscricao = inscricoes[i];
            string? nome = nomeDeExibicao.GetValueOrDefault(inscricao.Id);
            resultado[inscricao.Id] = new InscritoDoLote(
                inscricao.NumeroInscricao,
                nome,
                Cpf.Mascarar(cpfs[i]),
                inscricao.VersaoFormularioId,
                inscricao.OfertaCursoId,
                inscricao.OfertaCursoSegundaOpcaoId);
        }

        return resultado;
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

    private static int IndiceDaArea(string codigo) => Array.IndexOf(OrdemDasAreas, codigo);

    /// <summary>Os lotes do escopo, sem rastreamento, do mais recente para o mais antigo.</summary>
    private IQueryable<LoteDeNotasDoEnem> DoEscopo(Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo) =>
        _context.LotesDeNotasDoEnem
            .AsNoTracking()
            .Where(l => l.ProcessoSeletivoId == processoSeletivoId
                && l.EtapaProcessoId == etapaProcessoId
                && l.EdicaoCodigo == edicaoCodigo)
            .OrderByDescending(l => l.ImportadoEm)
            .ThenByDescending(l => l.Id);
}
