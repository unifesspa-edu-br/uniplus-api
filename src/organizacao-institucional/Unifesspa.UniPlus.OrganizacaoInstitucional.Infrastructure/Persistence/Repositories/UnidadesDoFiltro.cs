namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>O recorte das Unidades pelos critérios da listagem: busca textual e tipos.</summary>
internal static class UnidadesDoFiltro
{
    /// <summary>As Unidades que atendem a todos os critérios informados no filtro; o critério ausente não recorta.</summary>
    public static IQueryable<Unidade> Filtradas(this IQueryable<Unidade> unidades, FiltroListagemUnidades filtro)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<Unidade> comBusca = filtro.TemBusca ? unidades.ComBusca(filtro.Termo!) : unidades;
        return filtro.TemTipos ? comBusca.ComTipos(filtro.Tipos) : comBusca;
    }

    /// <summary>
    /// Busca acento/caixa-insensível via immutable_unaccent + ILIKE.
    /// </summary>
    /// <remarks>
    /// O termo é normalizado em C# (NFD + remoção de NonSpacingMark) e passado
    /// como parâmetro SQL simples; a função immutable_unaccent() é aplicada
    /// server-side apenas nos campos da entidade, onde os índices GIN trigram
    /// permitem que o planejador faça varredura trigrama antes do recheck ILIKE.
    /// ILIKE cuida da caixa; ambos os lados ficam sem diacríticos → acento-insensível.
    /// Slug é value object (SlugValueConverter): o EF não compõe <c>u.Slug.Valor</c>
    /// dentro de uma DB function, então referenciamos a coluna via
    /// <c>EF.Property&lt;string&gt;(u, "Slug")</c> — traduz para immutable_unaccent(u.slug)
    /// e ainda aproveita o índice idx_unidades_slug_trgm.
    /// Os curingas do LIKE (% _ \) no termo são escapados para serem tratados
    /// como texto literal; só os % das pontas atuam como wildcard de "contém".
    /// O escape char precisa ser passado explicitamente: a sobrecarga de 2 args
    /// do ILike no Npgsql emite ESCAPE '' (escape desligado), ignorando o \.
    /// </remarks>
    private static IQueryable<Unidade> ComBusca(this IQueryable<Unidade> unidades, string termoDaBusca)
    {
        string termo = EscaparCuringasLike(RemoverDiacriticos(termoDaBusca.Trim()));
        string pattern = "%" + termo + "%";
        const string escape = @"\";
        return unidades.Where(u =>
            EF.Functions.ILike(FuncoesDeTextoDaOrganizacao.ImmutableUnaccent(u.Nome), pattern, escape) ||
            EF.Functions.ILike(FuncoesDeTextoDaOrganizacao.ImmutableUnaccent(u.Sigla), pattern, escape) ||
            EF.Functions.ILike(FuncoesDeTextoDaOrganizacao.ImmutableUnaccent(u.Codigo), pattern, escape) ||
            EF.Functions.ILike(FuncoesDeTextoDaOrganizacao.ImmutableUnaccent(EF.Property<string>(u, "Slug")), pattern, escape) ||
            (u.Alias != null && EF.Functions.ILike(FuncoesDeTextoDaOrganizacao.ImmutableUnaccent(u.Alias), pattern, escape)));
    }

    /// <summary>
    /// Tipos: tipo = ANY(@tipos). EF aplica o value converter (enum→texto)
    /// aos elementos do array — Npgsql gera <c>tipo = ANY(...)</c>.
    /// </summary>
    private static IQueryable<Unidade> ComTipos(this IQueryable<Unidade> unidades, IReadOnlyList<TipoUnidade> tipos) =>
        unidades.Where(u => tipos.Contains(u.Tipo));

    // Aplica a mesma transformação que immutable_unaccent faz no banco:
    // decompõe em NFD e descarta NonSpacingMark (diacríticos). Usado para
    // normalizar o TERMO de busca em C# antes de montá-lo como padrão ILIKE,
    // evitando uma chamada de DB function no lado do parâmetro. Os campos da
    // entidade passam por immutable_unaccent() server-side via FuncoesDeTextoDaOrganizacao.
    private static string RemoverDiacriticos(string texto)
    {
        string decomposto = texto.Normalize(NormalizationForm.FormD);
        return new string([.. decomposto.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }

    // Escapa os curingas do LIKE/ILIKE (\ % _) para que metacaracteres digitados
    // pelo usuário sejam comparados como texto literal, não como wildcards — sem
    // isso, q="_" ou q="%" casaria quase qualquer registro. O escape char é a
    // barra invertida (padrão do PostgreSQL); a barra é escapada primeiro para não
    // duplicar as inseridas adiante em % e _.
    private static string EscaparCuringasLike(string termo) =>
        termo
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
