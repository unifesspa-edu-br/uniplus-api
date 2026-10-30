namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório dos pedidos de concessão, de seus anexos e dos acessos a eles.</summary>
public sealed class PedidoDeConcessaoRepository : IPedidoDeConcessaoRepository
{
    private readonly AutorizacaoDbContext _context;

    public PedidoDeConcessaoRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<PedidoDeConcessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Pedidos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PedidoDeConcessao>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.Pedidos.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(PedidoDeConcessao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Pedidos.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(PedidoDeConcessao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Só a linha do pedido, e não o grafo: as decisões e os anexos novos são acrescentados, e marcá-los como
        // alterados falharia a gravação. A linha marcada entra no UPDATE com o xmin lido, o que serializa quem altera
        // o pedido (anexo, aprovação, cancelamento) contra quem o decide.
        _context.Entry(entity).State = EntityState.Modified;
    }

    public void Remover(PedidoDeConcessao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // O pedido é registro de auditoria: encerra-se por estado, nunca se exclui.
        throw new NotSupportedException("Pedido de concessão não é removido — ele se encerra por aprovação, recusa ou cancelamento.");
    }

    public async Task<PedidoDeConcessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Pedidos
            .Include(p => p.Decisoes)
            .Include(p => p.Anexos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PedidoDeConcessao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Decisoes)
            .Include(p => p.Anexos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<AnexoDoPedido?> ObterAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) =>
        await _context.AnexosDosPedidos.FirstOrDefaultAsync(a => a.Id == anexoId, cancellationToken).ConfigureAwait(false);

    public async Task AdicionarLeituraAsync(LeituraDoAnexoDoPedido leitura, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        await _context.LeiturasDosAnexos.AddAsync(leitura, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LeituraDoAnexoDoPedido>> ListarLeiturasAsync(Guid pedidoId, CancellationToken cancellationToken = default) =>
        await _context.LeiturasDosAnexos
            .AsNoTracking()
            .Where(l => l.PedidoId == pedidoId)
            .OrderBy(l => l.Instante)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<PedidoComPerfil> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        FiltroDePedidos filtro,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<PedidoDeConcessao> consulta = _context.Pedidos.AsNoTracking();

        string emissor = filtro.Pessoa.Emissor;
        string sub = filtro.Pessoa.Sub;
        string? email = filtro.EmailVerificado;
        consulta = filtro.Fila switch
        {
            FilaDePedidos.Gestor => consulta.Where(p => p.Situacao == SituacaoDoPedido.AguardandoGestor),
            FilaDePedidos.ProtecaoDeDados => consulta.Where(p => p.Situacao == SituacaoDoPedido.AguardandoProtecaoDeDados),
            FilaDePedidos.Todos => consulta,
            _ => consulta.Where(p =>
                (p.SolicitanteEmissor == emissor && p.SolicitanteSub == sub)
                || (p.BeneficiarioEmissor == emissor && p.BeneficiarioSub == sub)
                || (p.BeneficiarioSub == null && email != null && p.BeneficiarioEmail == email)),
        };

        if (filtro.Situacao is { } situacao)
        {
            consulta = consulta.Where(p => p.Situacao == situacao);
        }

        OrderedKeysetPage<PedidoDeConcessao> page = await OrderedKeysetCursor
            .ApplyAsync(
                consulta,
                b => b.Descending(p => p.CriadoEm).Descending(p => p.Id),
                SortKeyDoInstante,
                (sortKey, id) => new { CriadoEm = InstanteDaSortKey(sortKey), Id = id },
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        Guid[] perfilIds = [.. page.Items.Select(p => p.PerfilId).Distinct()];
        Dictionary<Guid, string> nomes = await _context.Perfis.AsNoTracking()
            .Where(perfil => perfilIds.Contains(perfil.Id))
            .ToDictionaryAsync(perfil => perfil.Id, perfil => perfil.Nome, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PedidoComPerfil> itens =
            [.. page.Items.Select(p => new PedidoComPerfil(p, nomes[p.PerfilId]) { Id = p.Id })];

        return (itens, page.Previous, page.Next);
    }

    // A sort key é o instante em ISO-8601 de ida e volta: ordem lexicográfica e cronológica coincidem e ele é estável entre
    // culturas. Nunca nula (ADR-0095): criado_em é coluna obrigatória.
    private static string SortKeyDoInstante(PedidoDeConcessao pedido) =>
        pedido.CriadoEm.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset InstanteDaSortKey(string sortKey) =>
        DateTimeOffset.Parse(sortKey, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
