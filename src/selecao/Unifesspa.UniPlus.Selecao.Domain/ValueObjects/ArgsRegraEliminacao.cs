namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>
/// Args aplicados de uma <see cref="Entities.RegraEliminacao"/> — os
/// parâmetros que o admin preenche ao aplicar uma regra do
/// <c>rol_de_regras</c> (<c>tipo=regra_eliminacao</c>), tipados por variante
/// conforme o <c>esquema_args</c> de cada código (modelagem P-B §2.5).
/// Discriminated union no molde de <c>PredicadoObrigatoriedade</c> (ADR-0058)
/// e de <c>ArgsCriterioDesempate</c> (Story #774).
/// </summary>
/// <remarks>
/// Forma fechada por design: as 3 variantes espelham as 3 regras semeadas em
/// <c>rol_de_regras</c> (Story #772) — <c>ELIM-NOTA-MINIMA-ETAPA</c>,
/// <c>ELIM-CORTE-REDACAO</c>, <c>ELIM-ZERO-EM-AREA</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(ArgsElimNotaMinimaEtapa), "notaMinimaEtapa")]
[JsonDerivedType(typeof(ArgsElimCorteRedacao), "corteRedacao")]
[JsonDerivedType(typeof(ArgsElimZeroEmArea), "zeroEmArea")]
public abstract record ArgsRegraEliminacao;

/// <summary>Elimina quando a nota da etapa referenciada é menor que <see cref="NotaMinima"/>. A etapa deve existir no mesmo processo (INV-B4).</summary>
public sealed record ArgsElimNotaMinimaEtapa(Guid EtapaRef, decimal NotaMinima) : ArgsRegraEliminacao;

/// <summary>Elimina quando a nota de redação do ENEM é menor que <see cref="Minimo"/> (ex.: 400 — Res. 805 Anexo I).</summary>
public sealed record ArgsElimCorteRedacao(decimal Minimo) : ArgsRegraEliminacao;

/// <summary>Elimina quando há nota zero em qualquer área do ENEM (Res. 805 art. 5º). Sem args.</summary>
public sealed record ArgsElimZeroEmArea : ArgsRegraEliminacao;
