namespace Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;

using System.ComponentModel;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Os filtros do histórico das decisões da proteção de dados do catálogo, lidos da query string. Combinam-se entre si; sem
/// nenhum, vêm todas as decisões.
/// </summary>
public sealed record FiltroDasDecisoesDaProtecaoRequest
{
    [FromQuery(Name = "ato")]
    [Description("Decisão: APROVACAO, AJUSTE_RECOMENDADO ou REPROVACAO. Repetível; sem o parâmetro, as três.")]
    public string[]? Ato { get; init; }

    [FromQuery(Name = "cadastro")]
    [Description("Cadastro do item: TIPO_DOCUMENTO ou FATO.")]
    public string? Cadastro { get; init; }

    [FromQuery(Name = "de")]
    [Description("Data inicial da decisão, inclusive (AAAA-MM-DD), no fuso de Belém.")]
    public DateOnly? De { get; init; }

    [FromQuery(Name = "ate")]
    [Description("Data final da decisão, inclusive (AAAA-MM-DD), no fuso de Belém.")]
    public DateOnly? Ate { get; init; }

    [FromQuery(Name = "busca")]
    [Description("Trecho do nome ou do código do item. Insensível a caixa e a acentuação. Até 200 caracteres.")]
    public string? Busca { get; init; }

    [FromQuery(Name = "minhas")]
    [Description("Quando verdadeiro, só as decisões de quem consulta.")]
    [DefaultValue(false)]
    public bool Minhas { get; init; }

    /// <summary>A consulta da página pedida com estes filtros.</summary>
    public ListarDecisoesDaProtecaoQuery ParaConsulta(PageRequest page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new(Ato ?? [], Cadastro, De, Ate, Busca, Minhas, page.AfterSortKey, page.AfterId, page.Limit, page.Direction);
    }
}
