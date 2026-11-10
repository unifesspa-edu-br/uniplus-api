namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

/// <summary>
/// A parte da conciliação que cabe aos perfis comuns, já travados: para cada permissão em uso cujo valor no catálogo novo
/// difere do anterior, registra a reclassificação, acompanha a base de cada concessão que a tem e retoma o perfil suspenso
/// cuja classe mudou. O código que o catálogo novo não tem não suspende nem dá acesso: fica só no log.
/// </summary>
internal sealed partial class PerfisAfetadosPeloCatalogo
{
    private readonly AutorizacaoDbContext _db;
    private readonly ICatalogoDePermissoesConsultavel _consultavel;
    private readonly ILogger _logger;

    public PerfisAfetadosPeloCatalogo(AutorizacaoDbContext db, ICatalogoDePermissoesConsultavel consultavel, ILogger logger)
    {
        _db = db;
        _consultavel = consultavel;
        _logger = logger;
    }

    public async Task ConciliarAsync(ComparacaoDeCatalogos comparacao, CancellationToken cancellationToken)
    {
        List<Perfil> perfis = await _db.Perfis.Where(p => p.Governanca == null)
            .ComComposicao().OrderBy(p => p.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (Perfil perfil in perfis)
        {
            await ConciliarOPerfilAsync(perfil, comparacao, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConciliarOPerfilAsync(Perfil perfil, ComparacaoDeCatalogos comparacao, CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoDoPerfil> emUso = await VersoesEmUsoAsync(perfil, cancellationToken).ConfigureAwait(false);
        List<ReclassificacaoDaPermissao> registradas = [.. CodigosEmUso(emUso)
            .Select(codigo => ReclassificarSeMudou(perfil, new PermissaoEmUso(codigo, emUso), comparacao))
            .OfType<ReclassificacaoDaPermissao>()];

        await AcompanharAsBasesAsync(perfil, registradas, comparacao, cancellationToken).ConfigureAwait(false);
        RetomarSeAClasseMudou(perfil, registradas, comparacao.Instante);
    }

    private static IEnumerable<string> CodigosEmUso(IReadOnlyList<VersaoDoPerfil> emUso) =>
        emUso.SelectMany(static v => v.Permissoes).Select(static p => p.Codigo).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private ReclassificacaoDaPermissao? ReclassificarSeMudou(Perfil perfil, PermissaoEmUso permissao, ComparacaoDeCatalogos comparacao)
    {
        if (comparacao.Atualizado.TryGetValue(permissao.Codigo, out LinhaDoRetratoDoCatalogo? nova))
        {
            return Reclassificar(perfil, permissao, nova, comparacao);
        }

        LogCodigoForaDoCatalogo(_logger, perfil.Id, permissao.Codigo);

        return null;
    }

    private ReclassificacaoDaPermissao? Reclassificar(
        Perfil perfil, PermissaoEmUso permissao, LinhaDoRetratoDoCatalogo nova, ComparacaoDeCatalogos comparacao)
    {
        MudancaDaPermissao? mudanca = permissao.Retratos
            .Select(r => ComparacaoComOCatalogo.MudancaDe(comparacao.AnteriorDe(permissao.Codigo, r), nova))
            .FirstOrDefault(static m => m is not null);
        if (mudanca is null)
        {
            return null;
        }

        ReclassificacaoDaPermissao reclassificacao = ReclassificacaoDaPermissao.Registrar(
            comparacao.ImplantacaoId, perfil.Id, permissao.Codigo, mudanca, comparacao.Instante, 0);
        _db.Reclassificacoes.Add(reclassificacao);

        return reclassificacao;
    }

    private async Task AcompanharAsBasesAsync(
        Perfil perfil, List<ReclassificacaoDaPermissao> registradas, ComparacaoDeCatalogos comparacao, CancellationToken cancellationToken)
    {
        if (registradas.Count == 0)
        {
            return;
        }

        string[] alterados = [.. registradas.Select(static r => r.Codigo)];
        List<CodigoDaConcessao> codigos = await _db.CodigosDasConcessoesDoPerfil(perfil.Id, comparacao.Instante)
            .Where(k => alterados.Contains(k.Codigo)).ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<(Guid, string), BaseViva> vivas = (await _db.BasesVivas()
            .Where(b => _db.Concessoes.Any(c => c.Id == b.ConcessaoId && c.PerfilId == perfil.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(static b => (b.ConcessaoId, b.Codigo));

        _db.BasesDasPermissoes.AddRange(codigos
            .Select(k => BaseAcompanhada(perfil.Id, k, vivas.GetValueOrDefault((k.ConcessaoId, k.Codigo)), comparacao))
            .OfType<BaseDaPermissaoNaConcessao>());
    }

    private static BaseDaPermissaoNaConcessao? BaseAcompanhada(Guid perfilId, CodigoDaConcessao codigo, BaseViva? viva, ComparacaoDeCatalogos comparacao)
    {
        BaseDaConcessao atual = viva is null
            ? new BaseDaConcessao(codigo.RetratoDaClassificacao!.Value, codigo.RetratoDeUnidade)
            : new BaseDaConcessao(viva.Classificacao, viva.DeUnidade);
        MudancaDaPermissao? mudanca = ComparacaoComOCatalogo.MudancaDe(
            comparacao.AnteriorDe(codigo.Codigo, atual), comparacao.Atualizado[codigo.Codigo]);

        return mudanca is null
            ? null
            : BaseDaPermissaoNaConcessao.Registrar(
                perfilId, codigo.ConcessaoId, codigo.Codigo, ComparacaoComOCatalogo.BaseDaConciliacao(atual, mudanca),
                OrigemDaBase.Conciliacao, comparacao.ImplantacaoId, 0);
    }

    private void RetomarSeAClasseMudou(Perfil perfil, List<ReclassificacaoDaPermissao> registradas, DateTimeOffset instante)
    {
        foreach (ReclassificacaoDaPermissao reclassificacao in registradas.Where(static r => r.Subiu || r.Desceu))
        {
            perfil.ReavaliarRetomada(new OrigemDaReavaliacao.PelaReclassificacao(reclassificacao.Codigo), _consultavel, instante);
        }
    }

    private async Task<IReadOnlyList<VersaoDoPerfil>> VersoesEmUsoAsync(Perfil perfil, CancellationToken cancellationToken)
    {
        List<Guid> emUso = await _db.VersoesEmUso(perfil.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        return [.. perfil.Versoes.Where(v => emUso.Contains(v.Id)).OrderByDescending(static v => v.Numero)];
    }

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "O perfil {PerfilId} reúne o código {Codigo}, que o catálogo da aplicação não conhece; nada é suspenso por ele.")]
    private static partial void LogCodigoForaDoCatalogo(ILogger logger, Guid perfilId, string codigo);
}

/// <summary>Uma permissão em uso por um perfil: o código e as versões em uso que a reúnem.</summary>
internal sealed record PermissaoEmUso(string Codigo, IReadOnlyList<VersaoDoPerfil> Versoes)
{
    public IEnumerable<BaseDaConcessao> Retratos => Versoes
        .SelectMany(v => v.Permissoes.Where(p => p.Codigo == Codigo))
        .Select(static p => new BaseDaConcessao(p.Classificacao, p.DeUnidade));
}
