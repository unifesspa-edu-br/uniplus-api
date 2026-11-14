namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A conferência do catálogo da aplicação com o último catálogo gravado, na implantação, depois das migrations. Numa
/// transação só e sob uma trava de sessão do banco: se a impressão é a do catálogo atual, termina sem gravar; senão grava
/// o catálogo novo, trava todos os perfis comuns antes de selecionar, registra o que mudou e retoma os perfis suspensos
/// cuja classe mudou. A governança nunca se trava nem se suspende: só se compara. O portão do catálogo vira junto com as
/// reclassificações, porque nada disso é visível a outras transações antes do commit.
/// </summary>
public sealed partial class ConciliacaoDoCatalogo
{
    private const string NomeDaTrava = "autorizacao.conciliacao_do_catalogo";

    private readonly UniPlusDbContext _db;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly ICatalogoDePermissoesConsultavel _consultavel;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ConciliacaoDoCatalogo> _logger;

    public ConciliacaoDoCatalogo(
        UniPlusDbContext db,
        CatalogoDaAplicacao catalogo,
        ICatalogoDePermissoesConsultavel consultavel,
        TimeProvider relogio,
        ILogger<ConciliacaoDoCatalogo> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(consultavel);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);

        _db = db;
        _catalogo = catalogo;
        _consultavel = consultavel;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>Confere o catálogo e grava o que mudou; sem mudança, não grava.</summary>
    public async Task ExecutarAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transacao = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transacao.ConfigureAwait(false))
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext({0}))", [NomeDaTrava], cancellationToken).ConfigureAwait(false);

            ImplantacaoDasPermissoes? atual = await ImplantacaoAtualAsync(cancellationToken).ConfigureAwait(false);
            if (atual is null || !string.Equals(atual.Impressao, _catalogo.Impressao, StringComparison.Ordinal))
            {
                await ConciliarAsync(atual, cancellationToken).ConfigureAwait(false);
            }

            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<ImplantacaoDasPermissoes?> ImplantacaoAtualAsync(CancellationToken cancellationToken) =>
        _db.ImplantacoesDasPermissoes.AsNoTracking()
            .Include(c => c.Permissoes)
            .OrderByDescending(c => c.Sequencia)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task ConciliarAsync(ImplantacaoDasPermissoes? atual, CancellationToken cancellationToken)
    {
        await TravarPerfisComunsAsync(cancellationToken).ConfigureAwait(false);

        // O instante vale o de depois das travas: a espera por elas pode atravessar o prazo de uma janela.
        DateTimeOffset agora = _relogio.GetUtcNow();
        ImplantacaoDasPermissoes novo = await GravarAImplantacaoAsync(agora, cancellationToken).ConfigureAwait(false);

        ComparacaoDeCatalogos comparacao = new(novo.Id, atual is null ? null : Linhas(atual.Permissoes), Linhas(novo.Permissoes), agora);
        await new PerfisAfetadosPeloCatalogo(_db, _consultavel, _logger).ConciliarAsync(comparacao, cancellationToken).ConfigureAwait(false);
        await RegistrarDivergenciasDaGovernancaAsync(comparacao, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImplantacaoDasPermissoes> GravarAImplantacaoAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ImplantacaoDasPermissoes novo = ImplantacaoDasPermissoes.Implantar(
            _catalogo.Impressao,
            _catalogo.Linhas.Select(l => new LinhaDoRetratoDoCatalogo(l.Codigo, l.Modulo, l.Classificacao, l.DeUnidade, l.Reservada)),
            agora);
        _db.ImplantacoesDasPermissoes.Add(novo);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return novo;
    }

    private async Task TravarPerfisComunsAsync(CancellationToken cancellationToken) =>
        await _db.Database.ExecuteSqlRawAsync(
            $"SELECT id FROM {AutorizacaoContribuicao.NomeDoEsquema}.perfis WHERE governanca IS NULL ORDER BY id FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

    private async Task RegistrarDivergenciasDaGovernancaAsync(ComparacaoDeCatalogos comparacao, CancellationToken cancellationToken)
    {
        List<PermissaoDeGovernanca> gravada = await _db.Perfis.AsNoTracking()
            .Where(p => p.Governanca != null)
            .SelectMany(p => p.Permissoes.Select(x => new PermissaoDeGovernanca(p.Id, x.Codigo)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _db.DivergenciasDaGovernanca.AddRange(ConferenciaDaGovernanca.DaComposicao(comparacao.ImplantacaoId, gravada, comparacao.Instante));
        _db.DivergenciasDaGovernanca.AddRange(
            ConferenciaDaGovernanca.DoCatalogo(comparacao.ImplantacaoId, comparacao.Anterior ?? new Dictionary<string, LinhaDoRetratoDoCatalogo>(), comparacao.Atualizado, comparacao.Instante));
    }

    private static Dictionary<string, LinhaDoRetratoDoCatalogo> Linhas(IEnumerable<PermissaoImplantada>? permissoes) =>
        (permissoes ?? []).ToDictionary(
            static p => p.Codigo,
            static p => new LinhaDoRetratoDoCatalogo(p.Codigo, p.Modulo, p.Classificacao, p.DeUnidade, p.Reservada),
            StringComparer.Ordinal);
}

/// <summary>O catálogo que entra, o anterior (nulo na primeira conferência) e o instante da conciliação.</summary>
internal sealed record ComparacaoDeCatalogos(
    Guid ImplantacaoId,
    IReadOnlyDictionary<string, LinhaDoRetratoDoCatalogo>? Anterior,
    IReadOnlyDictionary<string, LinhaDoRetratoDoCatalogo> Atualizado,
    DateTimeOffset Instante)
{
    /// <summary>O valor que a permissão tinha: o do catálogo anterior e, sem ele, o do retrato dado.</summary>
    public BaseDaConcessao AnteriorDe(string codigo, BaseDaConcessao retrato) =>
        Anterior is not null && Anterior.TryGetValue(codigo, out LinhaDoRetratoDoCatalogo? linha)
            ? new BaseDaConcessao(linha.Classificacao, linha.DeUnidade)
            : retrato;
}
