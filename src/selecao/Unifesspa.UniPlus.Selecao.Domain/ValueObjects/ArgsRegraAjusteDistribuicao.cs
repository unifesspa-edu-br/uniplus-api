namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>
/// Args aplicados de uma regra do <c>rol_de_regras</c>
/// (<c>tipo=regra_ajuste_distribuicao_vagas</c>) — os motores que reconciliam um quadro
/// fixado pelo edital cuja soma passa do <c>VO_base</c>. Discriminated union no molde de
/// <c>PredicadoObrigatoriedade</c> (ADR-0058) e de <see cref="ArgsRegraEliminacao"/>.
/// </summary>
/// <remarks>
/// <para>
/// As variantes espelham os motores que <c>RECONCILIACAO-VAGAS-ART11-PU</c> declara em
/// <c>esquema_args.motores_nao_art10</c>, e a vedação que ela também declara vale aqui: são
/// motores de distribuição institucional, nunca da Lei 12.711 — lá a reconciliação está
/// embutida na calculadora, com cap no VO e prioridade da reserva de baixa renda.
/// </para>
/// <para>
/// Só reduzem. Um quadro que soma menos que o <c>VO_base</c> não é reconciliado: acrescer
/// seria o sistema criar vaga que ninguém autorizou, e a quantidade de vagas do certame é
/// ato administrativo.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(ArgsReduzirDe), "reduzirDe")]
[JsonDerivedType(typeof(ArgsReduzirProporcionalEm), "reduzirProporcionalEm")]
public abstract record ArgsRegraAjusteDistribuicao;

/// <summary>
/// Tira o excesso inteiro de uma única modalidade, que precisa estar entre as selecionadas.
/// </summary>
public sealed record ArgsReduzirDe(string ModalidadeCodigo) : ArgsRegraAjusteDistribuicao;

/// <summary>
/// Reparte o excesso entre as modalidades nomeadas, proporcionalmente ao que cada uma
/// declarou.
/// </summary>
public sealed record ArgsReduzirProporcionalEm(IReadOnlyList<string> ModalidadeCodigos) : ArgsRegraAjusteDistribuicao;
