namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>
/// Args aplicados de um <see cref="Entities.CriterioDesempate"/> — os
/// parâmetros que o admin preenche ao aplicar uma regra do
/// <c>rol_de_regras</c> (<c>tipo=criterio_desempate</c>), tipados por
/// variante conforme o <c>esquema_args</c> de cada código. Discriminated union
/// no molde de <c>PredicadoObrigatoriedade</c> (ADR-0058).
/// </summary>
/// <remarks>
/// <para>
/// Forma fechada por design: as 5 variantes espelham as 5 regras semeadas em
/// <c>rol_de_regras</c> — <c>DESEMPATE-MAIOR-NOTA-ETAPA</c>,
/// <c>DESEMPATE-MAIOR-IDADE</c>, <c>DESEMPATE-IDOSO</c>,
/// <c>DESEMPATE-PREDICADO-FATO</c> e <c>DESEMPATE-MAIOR-NOTA-AREA-ENEM</c>. Uma
/// regra de desempate nova exige nova variante tipada explícita.
/// </para>
/// <para>
/// <see cref="ArgsDesempatePredicadoFato"/> reusa literalmente
/// <see cref="CondicaoDnf"/> (ADR-0111, Story #847) em vez de carregar três
/// strings soltas — fecha o gap que a modelagem da classificação documentava
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
[JsonDerivedType(typeof(ArgsDesempateMaiorNotaAreaEnem), "maiorNotaAreaEnem")]
public abstract record ArgsCriterioDesempate;

/// <summary>Ordena o subgrupo pela nota da etapa referenciada (deve existir no mesmo processo — INV-B6).</summary>
public sealed record ArgsDesempateMaiorNotaEtapa(Guid EtapaRef) : ArgsCriterioDesempate;

/// <summary>Ordena por data de nascimento — nascido mais cedo vence. Sem args.</summary>
public sealed record ArgsDesempateMaiorIdade : ArgsCriterioDesempate;

/// <summary>Prioriza quem satisfaz <c>FAIXA_ETARIA ≥ IdadeMinima</c> (Lei 10.741/2003 art. 27).</summary>
public sealed record ArgsDesempateIdoso(int IdadeMinima) : ArgsCriterioDesempate;

/// <summary>Prioriza quem satisfaz a <see cref="CondicaoDnf"/> sobre um fato do candidato (ADR-0111).</summary>
public sealed record ArgsDesempatePredicadoFato(CondicaoDnf Condicao) : ArgsCriterioDesempate;

/// <summary>
/// Ordena pela maior nota numa área do ENEM, percorrendo <see cref="Areas"/> na ordem
/// declarada: o empate que resta numa área passa à seguinte.
/// </summary>
/// <remarks>
/// <para>
/// Guarda só o código de cada área. O rótulo vem da cópia do Peso por Área congelada na
/// classificação do processo, a única fonte de área no Seleção.
/// </para>
/// <para>
/// A lista é copiada na construção: validada a ordem, quem a entregou não pode mais mudá-la
/// por baixo do critério. A igualdade é pelo conteúdo, na ordem, como a das variantes de
/// valor escalar.
/// </para>
/// </remarks>
public sealed record ArgsDesempateMaiorNotaAreaEnem : ArgsCriterioDesempate
{
    public ArgsDesempateMaiorNotaAreaEnem(IReadOnlyList<string> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        Areas = Array.AsReadOnly([.. areas]);
    }

    public IReadOnlyList<string> Areas { get; }

    public bool Equals(ArgsDesempateMaiorNotaAreaEnem? other) =>
        other is not null && Areas.SequenceEqual(other.Areas, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (string area in Areas)
        {
            hash.Add(area, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
