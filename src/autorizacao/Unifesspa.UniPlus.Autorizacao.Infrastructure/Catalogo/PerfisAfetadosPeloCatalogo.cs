namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A parte da conciliação que cabe aos perfis comuns, já travados: para cada permissão em uso cujo valor no catálogo novo
/// difere do anterior, registra a reclassificação, acompanha a base de cada concessão que a tem e retoma o perfil suspenso
/// cuja classe mudou. O código que o catálogo novo não tem não suspende nem dá acesso: fica só no log.
/// </summary>
internal sealed partial class PerfisAfetadosPeloCatalogo
{
    private readonly UniPlusDbContext _db;
    private readonly ICatalogoDePermissoesConsultavel _consultavel;
    private readonly ILogger _logger;

    public PerfisAfetadosPeloCatalogo(UniPlusDbContext db, ICatalogoDePermissoesConsultavel consultavel, ILogger logger)
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
        IReadOnlyList<BaseEmVigor> bases = await BasesEmVigorAsync(perfil, comparacao.Instante, cancellationToken).ConfigureAwait(false);
        List<ReclassificacaoDaPermissao> registradas = [.. CodigosEmUso(emUso)
            .Select(codigo => ReclassificarSeMudou(perfil, new PermissaoEmUso(codigo, emUso, bases), comparacao))
            .OfType<ReclassificacaoDaPermissao>()];

        AcompanharAsBases(perfil, registradas, bases, comparacao);
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

    /// <summary>
    /// Sem o código no catálogo anterior, o valor anterior vem da versão e da base em vigor de cada concessão: a permissão que
    /// volta ao catálogo acima de uma base rebaixada suspende essa concessão, e a suspensão precisa de reclassificação vigente.
    /// </summary>
    private ReclassificacaoDaPermissao? Reclassificar(
        Perfil perfil, PermissaoEmUso permissao, LinhaDoRetratoDoCatalogo nova, ComparacaoDeCatalogos comparacao)
    {
        MudancaDaPermissao? mudanca = permissao.Anteriores
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

    private void AcompanharAsBases(
        Perfil perfil, List<ReclassificacaoDaPermissao> registradas, IReadOnlyList<BaseEmVigor> bases, ComparacaoDeCatalogos comparacao)
    {
        HashSet<string> alterados = [.. registradas.Select(static r => r.Codigo)];

        _db.BasesDasPermissoes.AddRange(bases
            .Where(b => alterados.Contains(b.Codigo))
            .Select(b => BaseAcompanhada(perfil.Id, b, comparacao))
            .OfType<BaseDaPermissaoNaConcessao>());
    }

    private static BaseDaPermissaoNaConcessao? BaseAcompanhada(Guid perfilId, BaseEmVigor emVigor, ComparacaoDeCatalogos comparacao)
    {
        MudancaDaPermissao? mudanca = ComparacaoComOCatalogo.MudancaDe(
            comparacao.AnteriorDe(emVigor.Codigo, emVigor.Base), comparacao.Atualizado[emVigor.Codigo]);

        return mudanca is null
            ? null
            : BaseDaPermissaoNaConcessao.Registrar(
                perfilId, emVigor.ConcessaoId, emVigor.Codigo, ComparacaoComOCatalogo.BaseDaConciliacao(emVigor.Base, mudanca),
                OrigemDaBase.Conciliacao, comparacao.ImplantacaoId, 0);
    }

    /// <summary>A base de cada código das concessões ativas do perfil: a gravada em vigor e, sem ela, o retrato da versão.</summary>
    private async Task<IReadOnlyList<BaseEmVigor>> BasesEmVigorAsync(Perfil perfil, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        List<CodigoDaConcessao> codigos = await _db.CodigosDasConcessoesDoPerfil(perfil.Id, instante)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<(Guid, string), BaseViva> vivas = (await _db.BasesVivas()
            .Where(b => _db.Set<Concessao>().Any(c => c.Id == b.ConcessaoId && c.PerfilId == perfil.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(static b => (b.ConcessaoId, b.Codigo));

        return [.. codigos.Select(k => new BaseEmVigor(k.ConcessaoId, k.Codigo, vivas.GetValueOrDefault((k.ConcessaoId, k.Codigo)) is { } viva
            ? new BaseDaConcessao(viva.Classificacao, viva.DeUnidade)
            : new BaseDaConcessao(k.RetratoDaClassificacao!.Value, k.RetratoDeUnidade)))];
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

/// <summary>A base de uma permissão numa concessão ativa, no momento da conciliação.</summary>
internal sealed record BaseEmVigor(Guid ConcessaoId, string Codigo, BaseDaConcessao Base);

/// <summary>Uma permissão em uso por um perfil: o código, as versões em uso que a reúnem e as bases das concessões.</summary>
internal sealed record PermissaoEmUso(string Codigo, IReadOnlyList<VersaoDoPerfil> Versoes, IReadOnlyList<BaseEmVigor> Bases)
{
    /// <summary>Os valores que a permissão tinha para o perfil: o retrato de cada versão em uso e a base de cada concessão.</summary>
    public IEnumerable<BaseDaConcessao> Anteriores => Versoes
        .SelectMany(v => v.Permissoes.Where(p => p.Codigo == Codigo))
        .Select(static p => new BaseDaConcessao(p.Classificacao, p.DeUnidade))
        .Concat(Bases.Where(b => b.Codigo == Codigo).Select(static b => b.Base));
}
