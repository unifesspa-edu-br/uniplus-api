namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Linq.Expressions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

public sealed class DocumentoDaInscricaoRepository : IDocumentoDaInscricaoRepository
{
    private readonly SelecaoDbContext _context;

    public DocumentoDaInscricaoRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<ApresentacaoDeDocumento?> ObterApresentacaoAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.ApresentacoesDeDocumento.FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<ArquivoDaApresentacao?> ObterArquivoAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.ArquivosDaApresentacao.FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task AdicionarApresentacaoAsync(ApresentacaoDeDocumento apresentacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        await _context.ApresentacoesDeDocumento.AddAsync(apresentacao, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarArquivoAsync(ArquivoDaApresentacao arquivo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        await _context.ArquivosDaApresentacao.AddAsync(arquivo, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(ArquivoDaApresentacao arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        _context.ArquivosDaApresentacao.Update(arquivo);
    }

    public async Task RemoverArquivoAsync(ArquivoDaApresentacao arquivo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        _context.ArquivosDaApresentacao.Remove(arquivo);

        // A apresentação sai com o último arquivo; os irmãos que continuam a seguram.
        bool temIrmaos = await _context.ArquivosDaApresentacao
            .AnyAsync(a => a.ApresentacaoId == arquivo.ApresentacaoId && a.Id != arquivo.Id, cancellationToken)
            .ConfigureAwait(false);
        if (!temIrmaos
            && await _context.ApresentacoesDeDocumento.FirstOrDefaultAsync(a => a.Id == arquivo.ApresentacaoId, cancellationToken).ConfigureAwait(false) is { } apresentacao)
        {
            _context.ApresentacoesDeDocumento.Remove(apresentacao);
        }
    }

    public async Task<IReadOnlyList<ArquivoDaApresentacao>> ListarArquivosDaInscricaoAsync(
        Guid inscricaoId, CancellationToken cancellationToken = default) =>
        await _context.ArquivosDaApresentacao
            .AsNoTracking()
            .Where(a => a.InscricaoId == inscricaoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        int linhasAfetadas = await _context.ArquivosDaApresentacao
            .Where(a => a.Id == id && a.Status == StatusArquivoEnviado.Pendente)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Status, StatusArquivoEnviado.Confirmado), cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public async Task<IReadOnlyList<ArquivoPendenteVencido>> ListarPendentesVencidosAsync(
        DateTimeOffset agora, int limite, CancellationToken cancellationToken = default) =>
        await _context.ArquivosDaApresentacao
            .AsNoTracking()
            .Where(PendenteVencido(agora))
            .OrderBy(a => a.ExpiraEm)
            .ThenBy(a => a.Id)
            .Take(limite)
            .Select(a => new ArquivoPendenteVencido(a.Id, a.ObjectKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> RemoverSePendenteVencidoAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        Guid? apresentacaoId = await _context.ArquivosDaApresentacao
            .Where(a => a.Id == id)
            .Select(a => (Guid?)a.ApresentacaoId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        int linhasAfetadas = await _context.ArquivosDaApresentacao
            .Where(a => a.Id == id)
            .Where(PendenteVencido(agora))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        if (linhasAfetadas == 1)
        {
            // A confirmação que falhou no commit pode ter deixado a cópia selada: as duas chaves saem.
            _context.ChavesALimpar.Add(ChaveALimpar.Agendar(ArquivoDaApresentacao.ChaveSeladaDe(id), agora));
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _context.ApresentacoesDeDocumento
                .Where(p => p.Id == apresentacaoId && !_context.ArquivosDaApresentacao.Any(a => a.ApresentacaoId == p.Id))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return linhasAfetadas == 1;
    }

    public async Task AgendarLimpezaAsync(string objectKey, DateTimeOffset limparApos, CancellationToken cancellationToken = default) =>
        await _context.ChavesALimpar.AddAsync(ChaveALimpar.Agendar(objectKey, limparApos), cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ChaveALimpar>> ListarChavesVencidasAsync(
        DateTimeOffset agora, int limite, CancellationToken cancellationToken = default) =>
        await _context.ChavesALimpar
            .AsNoTracking()
            .Where(c => c.LimparApos <= agora)
            .OrderBy(c => c.LimparApos)
            .ThenBy(c => c.Id)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task RemoverChaveAgendadaAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.ChavesALimpar.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

    private static Expression<Func<ArquivoDaApresentacao, bool>> PendenteVencido(DateTimeOffset agora) =>
        a => a.Status == StatusArquivoEnviado.Pendente && a.ExpiraEm <= agora;
}
