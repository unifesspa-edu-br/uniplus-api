namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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

    /// <summary>
    /// A listagem da decisão de acesso, em uma só instrução, junto da impressão do catálogo gravado no banco. A instrução
    /// parte de uma linha fixa e junta por fora o catálogo atual e as linhas da composição: sempre volta ao menos uma linha,
    /// com a impressão nula se o banco não tem catálogo, com ou sem concessões. A impressão, as bases, as reclassificações
    /// e as suspensões saem do mesmo retrato.
    /// </summary>
    public static IQueryable<LinhaDoAcesso> LinhasDeAcessoComImpressao(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora)
    {
        IQueryable<LinhaPlana> linhas = context.LinhasPlanas(concessoes, context.CodigosDeAcesso(concessoes, agora));
        IQueryable<string> impressao = context.ImplantacoesDasPermissoes.OrderByDescending(c => c.Sequencia).Select(c => c.Impressao);

        return from s in context.Database.SqlQuery<int>($"SELECT 1 AS \"Value\"")
               from l in linhas.DefaultIfEmpty()
               orderby l.Concessao.ConcedidaEm, l.Concessao.Id
               select new LinhaDoAcesso
               {
                   Impressao = impressao.FirstOrDefault(),
                   Linha = l.PerfilNome == null ? null : new LinhaDaConcessao(
                       new ConcessaoLida(l.Concessao.Id, l.Concessao.PerfilId, l.PerfilNome, l.Concessao.UnidadeId, l.Concessao.EscopoTipo, l.Concessao.ValidaAte, l.Concessao.VersaoId), l.Codigo),
               };
    }

    private static IQueryable<LinhaDaConcessao> ComCodigos(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, IQueryable<CodigoDaConcessao> codigos) =>
        from l in context.LinhasPlanas(concessoes, codigos)
        orderby l.Concessao.ConcedidaEm, l.Concessao.Id
        select new LinhaDaConcessao(
            new ConcessaoLida(l.Concessao.Id, l.Concessao.PerfilId, l.PerfilNome, l.Concessao.UnidadeId, l.Concessao.EscopoTipo, l.Concessao.ValidaAte, l.Concessao.VersaoId), l.Codigo);

    private static IQueryable<LinhaPlana> LinhasPlanas(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, IQueryable<CodigoDaConcessao> codigos) =>
        from c in concessoes
        join p in context.Perfis on c.PerfilId equals p.Id
        join k in codigos on c.Id equals k.ConcessaoId into doCodigo
        from k in doCodigo.DefaultIfEmpty()
        select new LinhaPlana { Concessao = c, PerfilNome = p.Nome, Codigo = k == null ? null : k.Codigo };

    /// <summary>As concessões, entre todas, que dão o código.</summary>
    public static IQueryable<Guid> ConcessoesComPermissaoEmVigor(
        this AutorizacaoDbContext context, string codigo, DateTimeOffset agora) =>
        context.CodigosDeAcesso(context.Concessoes, agora)
            .Where(k => k.Codigo == codigo)
            .Select(k => k.ConcessaoId);

    /// <summary>Os códigos que as concessões não revogadas do perfil dão no instante pela composição, com o retrato de cada um, sem olhar suspensão.</summary>
    public static IQueryable<CodigoDaConcessao> CodigosDasConcessoesDoPerfil(
        this AutorizacaoDbContext context, Guid perfilId, DateTimeOffset agora) =>
        context.CodigosDasConcessoes(context.Concessoes.Where(c => c.PerfilId == perfilId && c.RevogadaEm == null), agora);

    /// <summary>Os códigos que a concessão dá no instante pela composição, sem olhar a suspensão do perfil.</summary>
    public static IQueryable<string> CodigosEmVigorDaConcessao(
        this AutorizacaoDbContext context, Guid concessaoId, DateTimeOffset agora) =>
        context.CodigosDasConcessoes(context.Concessoes.Where(c => c.Id == concessaoId), agora).Select(k => k.Codigo);

    // O acesso é a composição das concessões cujo perfil não está suspenso, sem a permissão que a reclassificação do
    // catálogo suspendeu na concessão.
    private static IQueryable<CodigoDaConcessao> CodigosDeAcesso(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        context.SemSuspensasPorReclassificacao(context.CodigosDasConcessoes(
            concessoes.Where(c => !context.SuspensoesDosPerfis.Any(s => s.PerfilId == c.PerfilId && s.RetomadaEm == null)), agora));

    private static IQueryable<CodigoDaConcessao> CodigosDasConcessoes(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        context.CodigosDasVersoes(concessoes, agora).Concat(context.CodigosDaGovernanca(concessoes));

    private static IQueryable<CodigoDaConcessao> CodigosDaGovernanca(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes) =>
        from c in concessoes
        join p in context.Perfis on c.PerfilId equals p.Id
        where p.Governanca != null
        from x in p.Permissoes
        select new CodigoDaConcessao
        {
            ConcessaoId = c.Id,
            Codigo = x.Codigo,
            DeVersao = false,
            RetratoDaClassificacao = null,
            RetratoDeUnidade = false,
        };

    // O código vale enquanto nenhuma vigência já aberta, de versão mais nova que a da concessão, deixou de reuni-lo:
    // a permissão que saiu não volta pela versão antiga, mesmo que uma versão posterior a recoloque. A concessão que
    // a janela de migração do perfil deixou para trás, por prazo vencido ou encerramento, não dá código algum, sem
    // depender de a rotina já ter revogado.
    private static IQueryable<CodigoDaConcessao> CodigosDasVersoes(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        from c in context.ComVersaoEmVigor(concessoes, agora)
        join v in context.VersoesDosPerfis on c.VersaoId equals v.Id
        from x in v.Permissoes
        where !(from g in context.VigenciasDosPerfis
                join w in context.VersoesDosPerfis on g.VersaoId equals w.Id
                where g.PerfilId == c.PerfilId && w.Numero > v.Numero && !w.Permissoes.Any(y => y.Codigo == x.Codigo)
                select g.Id).Any()
        select new CodigoDaConcessao
        {
            ConcessaoId = c.Id,
            Codigo = x.Codigo,
            DeVersao = true,
            RetratoDaClassificacao = (ClassificacaoDeProtecao?)x.Classificacao,
            RetratoDeUnidade = x.DeUnidade,
        };

    // A concessão cujo perfil tem vigência aberta e que a janela de migração do perfil não deixou para trás, por prazo
    // vencido ou encerramento, sem depender de a rotina já ter revogado.
    private static IQueryable<Concessao> ComVersaoEmVigor(
        this AutorizacaoDbContext context, IQueryable<Concessao> concessoes, DateTimeOffset agora) =>
        concessoes.Where(c => context.VigenciasDosPerfis.Any(g => g.PerfilId == c.PerfilId && g.Ate == null)
            && !context.JanelasDeMigracao.Any(j => j.PerfilId == c.PerfilId && (j.EncerradaEm != null || j.Prazo <= agora)
                && context.VersoesDosPerfis.Any(v => v.Id == c.VersaoId && v.Numero < j.NumeroDaVersaoNova)));
}

/// <summary>A concessão como a decisão de acesso a lê.</summary>
internal sealed record ConcessaoLida(
    Guid Id, Guid PerfilId, string PerfilNome, Guid? UnidadeId, TipoDeEscopo EscopoTipo, DateTimeOffset? ValidaAte, Guid? VersaoId);

/// <summary>Uma linha da consulta da decisão: a concessão e um dos códigos que ela dá, ou nenhum.</summary>
internal sealed record LinhaDaConcessao(ConcessaoLida Concessao, string? Codigo);

/// <summary>A linha da decisão de acesso e a impressão do catálogo que a mesma instrução leu; a linha é nula se não há concessão.</summary>
internal sealed class LinhaDoAcesso
{
    public string? Impressao { get; init; }

    public LinhaDaConcessao? Linha { get; init; }
}

/// <summary>A concessão e um dos seus códigos, achatados para a instrução SQL.</summary>
internal sealed class LinhaPlana
{
    public Concessao Concessao { get; init; } = null!;

    public string PerfilNome { get; init; } = string.Empty;

    public string? Codigo { get; init; }
}
