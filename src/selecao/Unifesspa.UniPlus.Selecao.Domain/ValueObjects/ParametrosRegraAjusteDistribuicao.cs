namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>
/// Parâmetros aplicados de uma regra do <c>regras_do_catalogo</c>
/// (<c>tipo=regra_ajuste_distribuicao_vagas</c>) — os motores que reconciliam um quadro
/// fixado pelo edital cuja soma passa das vagas ofertadas base. Discriminated union no molde de
/// <c>PredicadoObrigatoriedade</c> (ADR-0058) e de <see cref="ParametrosRegraEliminacao"/>.
/// </summary>
/// <remarks>
/// <para>
/// As variantes espelham os motores que <c>RECONCILIACAO-VAGAS-ARTIGO-11-PARAGRAFO-UNICO</c> declara em
/// <c>declaracao_da_regra.motores_nao_art10</c>, e a vedação que ela também declara vale aqui: são
/// motores de distribuição institucional, nunca da Lei 12.711 — lá a reconciliação está
/// embutida na calculadora, com teto nas vagas ofertadas base e prioridade da reserva de baixa renda.
/// </para>
/// <para>
/// Só reduzem. Um quadro que soma menos que as vagas ofertadas base não é reconciliado: acrescer
/// seria o sistema criar vaga que ninguém autorizou, e a quantidade de vagas do certame é
/// ato administrativo.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(ParametrosReduzirDe), "reduzirDe")]
[JsonDerivedType(typeof(ParametrosReduzirProporcionalEm), "reduzirProporcionalEm")]
public abstract record ParametrosRegraAjusteDistribuicao;

/// <summary>
/// Tira o excesso inteiro de uma única modalidade, que precisa estar entre as selecionadas.
/// </summary>
public sealed record ParametrosReduzirDe(string ModalidadeCodigo) : ParametrosRegraAjusteDistribuicao;

/// <summary>
/// Reparte o excesso entre as modalidades nomeadas, proporcionalmente ao que cada uma
/// declarou.
/// </summary>
public sealed record ParametrosReduzirProporcionalEm(IReadOnlyList<string> ModalidadeCodigos) : ParametrosRegraAjusteDistribuicao;
