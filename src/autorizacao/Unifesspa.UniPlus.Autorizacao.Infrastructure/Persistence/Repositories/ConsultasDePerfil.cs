namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>A leitura do perfil por inteiro: as versões, os documentos e as vigências de que a composição em vigor depende.</summary>
internal static class ConsultasDePerfil
{
    /// <summary>
    /// Traz o perfil com as permissões próprias, as versões (com permissões e documentos) e as vigências, em consultas
    /// separadas para não multiplicar linhas.
    /// </summary>
    public static IQueryable<Perfil> ComComposicao(this IQueryable<Perfil> perfis) =>
        perfis
            .Include(p => p.Permissoes)
            .Include(p => p.Versoes).ThenInclude(v => v.Permissoes)
            .Include(p => p.Versoes).ThenInclude(v => v.Documentos)
            .Include(p => p.Vigencias)
            .AsSplitQuery();
}
