namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Uma entrada do <c>regras_do_catalogo</c> como o cliente administrativo a enxerga: a identidade
/// <c>(codigo, versao)</c> que ele referencia numa configuração, o tipo, a declaração da versão da regra, as invariantes declaradas, a base legal e o hash.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que o frontend pare de manter constantes paralelas de fórmula, precisão,
/// eliminação, bônus, desempate, distribuição, ajuste, remanejamento, prazo recursal, ordem
/// de alocação e algoritmo de contagem. Cada uma dessas listas duplicada no cliente é uma
/// cópia que envelhece quando o catálogo ganha uma versão nova.
/// </para>
/// <para>
/// <see cref="DeclaracaoDaRegra"/> e <see cref="Invariantes"/> são <see cref="JsonElement"/> e
/// atravessam verbatim. Reserializar como texto os entregaria escapados dentro de uma string,
/// e o cliente teria de desserializar de novo o que já era JSON.
/// </para>
/// <para>
/// <see cref="ModalidadesAdmitidas"/> é projeção pura de
/// <c>declaracao_da_regra.modalidades_admitidas</c> (<see cref="Application.Services.ModalidadesAdmitidasDaDeclaracaoDaRegra"/>),
/// não campo novo do agregado — o rol continua na declaração porque ele já compõe o hash da
/// definição, e um campo próprio em <c>RegraCatalogo</c> mudaria a fórmula do hash das versões já
/// publicadas. O campo tipado poupa o cliente de ler uma chave nomeada de dentro da declaração.
/// </para>
/// <para>
/// <see cref="RamoDaDistribuicao"/> diz, para uma regra de distribuição de vagas, se ela monta o quadro pelo
/// cálculo da Lei de Cotas (<c>LEI_DE_COTAS</c>) ou se o edital o fixa (<c>QUADRO_FIXADO_NO_EDITAL</c>); é nulo
/// nas regras de outros tipos. O cliente deixa de manter no código a lista de códigos de cada ramo.
/// </para>
/// </remarks>
public sealed record RegraCatalogoDto(
    string Codigo,
    string Versao,
    CodigoComRotulo<TipoRegra> Tipo,
    JsonElement DeclaracaoDaRegra,
    JsonElement Invariantes,
    string BaseLegal,
    string Hash,
    IReadOnlyList<string>? ModalidadesAdmitidas,
    string? RamoDaDistribuicao)
{
    public string? Rotulo { get; init; }

    /// <summary>
    /// Hypermedia links (HATEOAS Level 1, ADR-0029) — opt-in, populado no boundary HTTP para
    /// respostas single. Coleções navegam pelo header <c>Link</c> (ADR-0026).
    /// </summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
