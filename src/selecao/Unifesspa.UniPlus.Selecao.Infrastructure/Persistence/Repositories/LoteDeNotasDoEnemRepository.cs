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
    private const int GrauDeParalelismo = 16;

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

    public async Task<IReadOnlyDictionary<Guid, string>> ObterNumerosDeInscricaoAsync(
        IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricaoIds);

        Guid[] ids = [.. inscricaoIds];
        var numeros = await _context.Inscricoes
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.NumeroInscricao != null)
            .Select(i => new { i.Id, Numero = i.NumeroInscricao! })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return numeros.ToDictionary(static n => n.Id, static n => n.Numero);
    }

    public async Task<IReadOnlyDictionary<Guid, IdentificacaoDoInscrito>> ObterIdentificacoesAsync(
        IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricaoIds);

        Guid[] ids = [.. inscricaoIds];
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, IdentificacaoDoInscrito>();
        }

        var cpfs = await _context.Inscricoes
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.CpfCiphertext })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nomes = await _context.RespostasDaInscricao
            .AsNoTracking()
            .Where(r => ids.Contains(r.InscricaoId)
                && r.OcorrenciaId == null
                && r.ValorTexto != null
                && (r.FatoCodigo == ConjuntoBasicoDaInscricao.Nome || r.FatoCodigo == ConjuntoBasicoDaInscricao.NomeSocial))
            .Select(r => new { r.InscricaoId, r.FatoCodigo, Texto = r.ValorTexto! })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // O nome social, quando preenchido, vale no lugar do civil (ADR-0082).
        Dictionary<Guid, string> nomeDeExibicao = nomes
            .GroupBy(static n => n.InscricaoId)
            .ToDictionary(
                static g => g.Key,
                static g => (g.FirstOrDefault(static n => n.FatoCodigo == ConjuntoBasicoDaInscricao.NomeSocial && !string.IsNullOrWhiteSpace(n.Texto))
                    ?? g.FirstOrDefault(static n => n.FatoCodigo == ConjuntoBasicoDaInscricao.Nome))?.Texto.Trim() ?? string.Empty);

        string[] mascarados = new string[cpfs.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, cpfs.Count),
            new ParallelOptions { MaxDegreeOfParallelism = GrauDeParalelismo, CancellationToken = cancellationToken },
            async (indice, ct) =>
            {
                byte[] plano = await _encryption
                    .DecryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, cpfs[indice].CpfCiphertext, ct)
                    .ConfigureAwait(false);
                mascarados[indice] = Cpf.Mascarar(Encoding.UTF8.GetString(plano));
            }).ConfigureAwait(false);

        Dictionary<Guid, IdentificacaoDoInscrito> resultado = [];
        for (int i = 0; i < cpfs.Count; i++)
        {
            string? nome = nomeDeExibicao.TryGetValue(cpfs[i].Id, out string? n) && n.Length > 0 ? n : null;
            resultado[cpfs[i].Id] = new IdentificacaoDoInscrito(nome, mascarados[i]);
        }

        return resultado;
    }

    public async Task<IReadOnlyDictionary<Guid, OfertasEscolhidasDaInscricao>> ObterOfertasEscolhidasAsync(
        IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricaoIds);

        Guid[] ids = [.. inscricaoIds];
        var ofertas = await _context.Inscricoes
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.VersaoFormularioId, i.OfertaCursoId, i.OfertaCursoSegundaOpcaoId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ofertas.ToDictionary(
            static o => o.Id,
            static o => new OfertasEscolhidasDaInscricao(o.VersaoFormularioId, o.OfertaCursoId, o.OfertaCursoSegundaOpcaoId));
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
