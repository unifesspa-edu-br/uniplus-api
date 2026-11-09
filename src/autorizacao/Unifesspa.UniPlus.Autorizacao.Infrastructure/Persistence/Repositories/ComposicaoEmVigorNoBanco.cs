namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que cada concessão dá hoje, lido no banco: o ponto único das consultas que decidem acesso. No perfil de governança
/// são as permissões próprias do perfil. No perfil comum é o código que está na versão da concessão e em toda versão de
/// número maior que vigorou depois dela, até a vigente: a permissão removida numa versão não volta numa posterior. O perfil
/// comum sem vigência aberta, ou a concessão comum sem versão, não dá permissão alguma. Espelha <c>Perfil.ComposicaoEmVigor</c>.
/// </summary>
internal static class ComposicaoEmVigorNoBanco
{
    /// <summary>
    /// Cada concessão informada com cada código que ela dá, em uma só instrução SQL; a concessão sem permissão
    /// aparece uma vez, com o código nulo.
    /// </summary>
    public static IQueryable<LinhaDaConcessao> ComCodigosEmVigor(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora)
    {
        IQueryable<CodigoDaConcessao> codigos = context.CodigosDasConcessoes(concessoes, agora);

        return
            from c in concessoes
            join p in context.Perfis on c.PerfilId equals p.Id
            join k in codigos on c.Id equals k.ConcessaoId into doCodigo
            from k in doCodigo.DefaultIfEmpty()
            orderby c.ConcedidaEm, c.Id
            select new LinhaDaConcessao(
                new ConcessaoLida(c.Id, c.PerfilId, p.Nome, c.UnidadeId, c.EscopoTipo, c.ValidaAte, c.VersaoId),
                k == null ? null : k.Codigo);
    }

    /// <summary>As concessões, entre todas, que dão o código.</summary>
    public static IQueryable<Guid> ConcessoesComPermissaoEmVigor(
        this AutorizacaoDbContext context, string codigo, DateTimeOffset agora) =>
        context.CodigosDasConcessoes(context.Concessoes, agora)
            .Where(k => k.Codigo == codigo)
            .Select(k => k.ConcessaoId);

    /// <summary>Os códigos que a concessão dá no instante, pela mesma regra da decisão de acesso.</summary>
    public static IQueryable<string> CodigosEmVigorDaConcessao(
        this AutorizacaoDbContext context, Guid concessaoId, DateTimeOffset agora) =>
        context.CodigosDasConcessoes(context.Concessoes.Where(c => c.Id == concessaoId), agora).Select(k => k.Codigo);

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
