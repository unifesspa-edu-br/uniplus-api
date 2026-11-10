namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que cada concessão dá hoje, lido no banco: o ponto único das consultas que decidem acesso. No perfil de governança
/// são as permissões próprias do perfil. No perfil comum é o código que está na versão da concessão e em toda versão de
/// número maior que vigorou depois dela, até a vigente: a permissão removida numa versão não volta numa posterior. O perfil
/// comum sem vigência aberta, ou a concessão comum sem versão, não dá permissão alguma. Espelha <c>Perfil.ComposicaoEmVigor</c>.
/// </summary>
/// <remarks>
/// A composição (<c>CodigosDasConcessoes</c>) é o que a versão da concessão dá e não olha a suspensão; a janela de
/// migração a lê assim, porque o lote é aceito durante a suspensão. O acesso a compõe com a suspensão do perfil na
/// mesma instrução: o perfil comum suspenso não dá permissão alguma, nem às concessões migradas, mas a concessão continua
/// listada. O perfil de governança não se suspende, e o ramo dele não muda. Sem cache entre requisições; quando o cache da
/// ADR-0085 existir, a suspensão terá de invalidá-lo pela marca de revogação.
/// </remarks>
internal static class ComposicaoEmVigorNoBanco
{
    /// <summary>
    /// Cada concessão informada com cada código que a composição lhe dá, em uma só instrução SQL; a concessão sem
    /// permissão aparece uma vez, com o código nulo. Não olha a suspensão: é o que a janela de migração compara.
    /// </summary>
    public static IQueryable<LinhaDaConcessao> ComCodigosEmVigor(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        context.ComCodigos(concessoes, context.CodigosDasConcessoes(concessoes, agora));

    /// <summary>
    /// A mesma listagem pela decisão de acesso: a concessão do perfil suspenso continua listada, sem código algum.
    /// </summary>
    public static IQueryable<LinhaDaConcessao> ComCodigosDeAcesso(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        context.ComCodigos(concessoes, context.CodigosDeAcesso(concessoes, agora));

    private static IQueryable<LinhaDaConcessao> ComCodigos(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, IQueryable<CodigoDaConcessao> codigos) =>
        from c in concessoes
        join p in context.Perfis on c.PerfilId equals p.Id
        join k in codigos on c.Id equals k.ConcessaoId into doCodigo
        from k in doCodigo.DefaultIfEmpty()
        orderby c.ConcedidaEm, c.Id
        select new LinhaDaConcessao(
            new ConcessaoLida(c.Id, c.PerfilId, p.Nome, c.UnidadeId, c.EscopoTipo, c.ValidaAte, c.VersaoId),
            k == null ? null : k.Codigo);

    /// <summary>As concessões, entre todas, que dão o código.</summary>
    public static IQueryable<Guid> ConcessoesComPermissaoEmVigor(
        this AutorizacaoDbContext context, string codigo, DateTimeOffset agora) =>
        context.CodigosDeAcesso(context.Concessoes, agora)
            .Where(k => k.Codigo == codigo)
            .Select(k => k.ConcessaoId);

    /// <summary>Os códigos que a concessão dá no instante pela composição, sem olhar a suspensão do perfil.</summary>
    public static IQueryable<string> CodigosEmVigorDaConcessao(
        this AutorizacaoDbContext context, Guid concessaoId, DateTimeOffset agora) =>
        context.CodigosDasConcessoes(context.Concessoes.Where(c => c.Id == concessaoId), agora).Select(k => k.Codigo);

    // O acesso é a composição das concessões cujo perfil não está suspenso.
    private static IQueryable<CodigoDaConcessao> CodigosDeAcesso(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        context.CodigosDasConcessoes(
            concessoes.Where(c => !context.SuspensoesDosPerfis.Any(s => s.PerfilId == c.PerfilId && s.RetomadaEm == null)), agora);

    private static IQueryable<CodigoDaConcessao> CodigosDasConcessoes(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        context.CodigosDaGovernanca(concessoes).Concat(context.CodigosDasVersoes(concessoes, agora));

    private static IQueryable<CodigoDaConcessao> CodigosDaGovernanca(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes) =>
        from c in concessoes
        join p in context.Perfis on c.PerfilId equals p.Id
        where p.Governanca != null
        from x in p.Permissoes
        select new CodigoDaConcessao { ConcessaoId = c.Id, Codigo = x.Codigo };

    // O código vale enquanto nenhuma vigência já aberta, de versão mais nova que a da concessão, deixou de reuni-lo:
    // a permissão que saiu não volta pela versão antiga, mesmo que uma versão posterior a recoloque. A concessão que
    // a janela de migração do perfil deixou para trás, por prazo vencido ou encerramento, não dá código algum, sem
    // depender de a rotina já ter revogado.
    private static IQueryable<CodigoDaConcessao> CodigosDasVersoes(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        from c in concessoes
        join v in context.VersoesDosPerfis on c.VersaoId equals v.Id
        from x in v.Permissoes
        where context.VigenciasDosPerfis.Any(g => g.PerfilId == c.PerfilId && g.Ate == null)
            && !context.JanelasDeMigracao.Any(j => j.PerfilId == c.PerfilId
                && (j.EncerradaEm != null || j.Prazo <= agora) && v.Numero < j.NumeroDaVersaoNova)
            && !(from g in context.VigenciasDosPerfis
                 join w in context.VersoesDosPerfis on g.VersaoId equals w.Id
                 where g.PerfilId == c.PerfilId && w.Numero > v.Numero && !w.Permissoes.Any(y => y.Codigo == x.Codigo)
                 select g.Id).Any()
        select new CodigoDaConcessao { ConcessaoId = c.Id, Codigo = x.Codigo };

    private sealed class CodigoDaConcessao
    {
        public Guid ConcessaoId { get; init; }

        public string Codigo { get; init; } = string.Empty;
    }
}

/// <summary>A concessão como a decisão de acesso a lê.</summary>
internal sealed record ConcessaoLida(
    Guid Id, Guid PerfilId, string PerfilNome, Guid? UnidadeId, TipoDeEscopo EscopoTipo, DateTimeOffset? ValidaAte, Guid? VersaoId);

/// <summary>Uma linha da consulta da decisão: a concessão e um dos códigos que ela dá, ou nenhum.</summary>
internal sealed record LinhaDaConcessao(ConcessaoLida Concessao, string? Codigo);
