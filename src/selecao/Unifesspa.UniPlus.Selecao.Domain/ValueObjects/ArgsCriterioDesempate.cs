namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>
/// Args aplicados de um <see cref="Entities.CriterioDesempate"/> — os
/// parâmetros que o admin preenche ao aplicar uma regra do
/// <c>rol_de_regras</c> (<c>tipo=criterio_desempate</c>), tipados por
/// variante conforme o <c>esquema_args</c> de cada código (modelagem P-B
/// §2.6). Discriminated union no molde de <c>PredicadoObrigatoriedade</c>
/// (ADR-0058).
/// </summary>
/// <remarks>
/// <para>
/// Forma fechada por design: as 4 variantes espelham as 4 regras semeadas em
/// <c>rol_de_regras</c> (Story #772) — <c>DESEMPATE-MAIOR-NOTA-ETAPA</c>,
/// <c>DESEMPATE-MAIOR-IDADE</c>, <c>DESEMPATE-IDOSO</c>,
/// <c>DESEMPATE-PREDICADO-FATO</c>. Uma 5ª regra de desempate exige nova
/// variante tipada explícita.
/// </para>
/// <para>
/// <see cref="ArgsDesempatePredicadoFato"/> reusa literalmente
/// <see cref="CondicaoDnf"/> (ADR-0111, Story #847) em vez de carregar três
/// strings soltas — fecha o gap que a modelagem P-B documentava
/// (<c>Fato</c> tipado mas não validado contra o vocabulário fechado de
/// fatos do candidato): <c>CriterioDesempate.Criar</c> agora confere a
/// condição contra o vocabulário resolvido do catálogo <c>rol_de_fatos_candidato</c>
/// (#846), fechando o INV-B6.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(ArgsDesempateMaiorNotaEtapa), "maiorNotaEtapa")]
[JsonDerivedType(typeof(ArgsDesempateMaiorIdade), "maiorIdade")]
[JsonDerivedType(typeof(ArgsDesempateIdoso), "idoso")]
[JsonDerivedType(typeof(ArgsDesempatePredicadoFato), "predicadoFato")]
public abstract record ArgsCriterioDesempate;

/// <summary>Ordena o subgrupo pela nota da etapa referenciada (deve existir no mesmo processo — INV-B6).</summary>
public sealed record ArgsDesempateMaiorNotaEtapa(Guid EtapaRef) : ArgsCriterioDesempate;

/// <summary>Ordena por data de nascimento — nascido mais cedo vence. Sem args.</summary>
public sealed record ArgsDesempateMaiorIdade : ArgsCriterioDesempate;

/// <summary>Prioriza quem satisfaz <c>FAIXA_ETARIA ≥ IdadeMinima</c> (Lei 10.741/2003 art. 27).</summary>
public sealed record ArgsDesempateIdoso(int IdadeMinima) : ArgsCriterioDesempate;

/// <summary>Prioriza quem satisfaz a <see cref="CondicaoDnf"/> sobre um fato do candidato (ADR-0111).</summary>
public sealed record ArgsDesempatePredicadoFato(CondicaoDnf Condicao) : ArgsCriterioDesempate;
