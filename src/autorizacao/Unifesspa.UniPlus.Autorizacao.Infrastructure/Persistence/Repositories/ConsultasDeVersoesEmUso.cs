namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>As versões de um perfil que ainda determinam acesso: o fragmento nomeado que a conciliação e a revisão compartilham.</summary>
internal static class ConsultasDeVersoesEmUso
{
    /// <summary>A vigente, as das concessões não revogadas e a de destino de janela aberta.</summary>
    public static IQueryable<Guid> VersoesEmUso(this AutorizacaoDbContext context, Guid perfilId) =>
        context.VersoesDosPerfis.Where(v => v.PerfilId == perfilId && v.Situacao == SituacaoDaVersao.Vigente).Select(v => v.Id)
            .Union(context.Concessoes
                .Where(c => c.PerfilId == perfilId && c.RevogadaEm == null && c.VersaoId != null)
                .Select(c => c.VersaoId!.Value))
            .Union(context.JanelasDeMigracao.Where(j => j.PerfilId == perfilId && j.EncerradaEm == null).Select(j => j.VersaoAlvoId));
}
