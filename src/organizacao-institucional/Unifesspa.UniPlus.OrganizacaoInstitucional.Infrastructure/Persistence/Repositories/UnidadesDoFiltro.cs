namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

/// <summary>O recorte das Unidades pelos critérios da listagem: busca textual e tipos.</summary>
internal static class UnidadesDoFiltro
{
    /// <summary>Caractere de escape do <c>LIKE</c>; a sobrecarga de dois argumentos do <c>ILike</c> desliga o escape.</summary>
    private const string EscapeDoLike = @"\";

    /// <summary>As Unidades que atendem a todos os critérios informados no filtro; o critério ausente não recorta.</summary>
    public static IQueryable<Unidade> Filtradas(this IQueryable<Unidade> unidades, FiltroListagemUnidades filtro)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(filtro);

        string? termoPreparado = NormalizacaoTextual.PrepararTermoDeBusca(filtro.Termo);
        IQueryable<Unidade> comBusca = termoPreparado is null ? unidades : unidades.ComBusca(termoPreparado);
        return filtro.TemTipos ? comBusca.ComTipos(filtro.Tipos) : comBusca;
    }

    /// <summary>
    /// Busca sem acento e sem caixa: o termo, já normalizado e com os curingas do <c>LIKE</c> escapados, é
    /// comparado como texto contido com as formas normalizadas que o banco gera para nome, sigla, código, slug e
    /// alias. Os dois lados seguem a mesma normalização da plataforma, e cada coluna tem o seu índice trigrama.
    /// </summary>
    /// <remarks>Alias ausente deixa a coluna nula, e o <c>ILIKE</c> sobre nulo não casa.</remarks>
    private static IQueryable<Unidade> ComBusca(this IQueryable<Unidade> unidades, string termoPreparado)
    {
        string padrao = "%" + termoPreparado + "%";
        return unidades.Where(u =>
            EF.Functions.ILike(EF.Property<string>(u, UnidadeConfiguration.NomeParaBusca), padrao, EscapeDoLike)
            || EF.Functions.ILike(EF.Property<string>(u, UnidadeConfiguration.SiglaParaBusca), padrao, EscapeDoLike)
            || EF.Functions.ILike(EF.Property<string>(u, UnidadeConfiguration.CodigoParaBusca), padrao, EscapeDoLike)
            || EF.Functions.ILike(EF.Property<string>(u, UnidadeConfiguration.SlugParaBusca), padrao, EscapeDoLike)
            || EF.Functions.ILike(EF.Property<string>(u, UnidadeConfiguration.AliasParaBusca), padrao, EscapeDoLike));
    }

    /// <summary>
    /// Tipos: tipo = ANY(@tipos). EF aplica o value converter (enum→texto)
    /// aos elementos do array — Npgsql gera <c>tipo = ANY(...)</c>.
    /// </summary>
    private static IQueryable<Unidade> ComTipos(this IQueryable<Unidade> unidades, IReadOnlyList<TipoUnidade> tipos) =>
        unidades.Where(u => tipos.Contains(u.Tipo));
}
