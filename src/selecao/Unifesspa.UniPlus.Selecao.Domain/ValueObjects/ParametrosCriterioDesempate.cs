namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Parâmetros aplicados de um <see cref="Entities.CriterioDesempate"/> — os
/// parâmetros que quem configura o processo preenche ao aplicar uma regra do
/// <c>regras_do_catalogo</c> (<c>tipo=criterio_desempate</c>), tipados por
/// variante conforme o esquema do documento da configuração. Conjunto fechado de variantes
/// no molde de <c>PredicadoObrigatoriedade</c> (ADR-0058).
/// </summary>
/// <remarks>
/// <para>
/// Forma fechada por design: as 5 variantes espelham as 5 regras semeadas em
/// <c>regras_do_catalogo</c> — <c>DESEMPATE_MAIOR_NOTA_ETAPA</c>,
/// <c>DESEMPATE_MAIOR_IDADE</c>, <c>DESEMPATE_IDOSO</c>,
/// <c>DESEMPATE_CONDICAO_SOBRE_DADO</c> e <c>DESEMPATE_MAIOR_NOTA_AREA_ENEM</c>. Uma
/// regra de desempate nova exige nova variante tipada explícita.
/// </para>
/// <para>
/// <see cref="ParametrosDesempatePredicadoFato"/> reusa literalmente
/// <see cref="CondicaoDnf"/> (ADR-0111, Story #847) em vez de carregar três
/// strings soltas — fecha o gap que a modelagem da classificação documentava
/// (<c>Fato</c> tipado mas não validado contra o vocabulário fechado de
/// fatos do candidato): <c>CriterioDesempate.Criar</c> confere a
/// condição contra o vocabulário resolvido do catálogo <c>dados_do_candidato</c>
/// (#846), fechando a integridade da referência à etapa.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(ParametrosDesempateMaiorNotaEtapa), "maiorNotaEtapa")]
[JsonDerivedType(typeof(ParametrosDesempateMaiorIdade), "maiorIdade")]
[JsonDerivedType(typeof(ParametrosDesempateIdoso), "idoso")]
[JsonDerivedType(typeof(ParametrosDesempatePredicadoFato), "predicadoFato")]
[JsonDerivedType(typeof(ParametrosDesempateMaiorNotaAreaEnem), "maiorNotaAreaEnem")]
public abstract record ParametrosCriterioDesempate
{
    public abstract RegraDeDesempate RegraCorrespondente();
    internal virtual IEnumerable<FieldError> RecusasDosValores() => [];
}

/// <summary>Ordena o subgrupo pela nota da etapa referenciada (deve existir no mesmo processo).</summary>
public sealed record ParametrosDesempateMaiorNotaEtapa(Guid EtapaRef) : ParametrosCriterioDesempate
{
    public override RegraDeDesempate RegraCorrespondente() => RegraDeDesempate.MaiorNotaNaEtapa;
}

/// <summary>Ordena por data de nascimento — nascido mais cedo vence. Sem parâmetros.</summary>
public sealed record ParametrosDesempateMaiorIdade : ParametrosCriterioDesempate
{
    public override RegraDeDesempate RegraCorrespondente() => RegraDeDesempate.MaiorIdade;
}

/// <summary>Prioriza quem satisfaz <c>FAIXA_ETARIA ≥ IdadeMinima</c> (Lei 10.741/2003 art. 27).</summary>
public sealed record ParametrosDesempateIdoso(int IdadeMinima) : ParametrosCriterioDesempate
{
    public override RegraDeDesempate RegraCorrespondente() => RegraDeDesempate.Idoso;
    internal override IEnumerable<FieldError> RecusasDosValores() => IdadeMinima > 0 ? [] :
        [new("idadeMinima", new DomainError("CriterioDesempate.IdadeMinimaInvalida", "A idade mínima do critério IDOSO deve ser maior que zero."))];
}

/// <summary>Prioriza quem satisfaz a <see cref="CondicaoDnf"/> sobre um fato do candidato (ADR-0111).</summary>
public sealed record ParametrosDesempatePredicadoFato(CondicaoDnf Condicao) : ParametrosCriterioDesempate
{
    public override RegraDeDesempate RegraCorrespondente() => RegraDeDesempate.CondicaoSobreDado;
    internal IEnumerable<FieldError> ConferirContraOVocabulario(IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario, IReadOnlySet<string>? coletados)
    {
        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, Condicao)]);
        DomainError? recusa = predicado.IsFailure ? predicado.Error : PredicadoDnfValidador.Validar(predicado.Value!, vocabulario, coletados).Error;
        return recusa is null ? [] : [new(CampoDaRecusa(recusa), recusa)];
    }
    private static string CampoDaRecusa(DomainError erro) => erro.Code switch
    {
        PredicadoDnfErrorCodes.OperadorIncompativelComDominio => "operador",
        PredicadoDnfErrorCodes.ValorIncompativelComTipo or PredicadoDnfErrorCodes.ValorForaDoDominio => "valor",
        _ => "fato",
    };
}

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
public sealed record ParametrosDesempateMaiorNotaAreaEnem : ParametrosCriterioDesempate
{
    public const int AreasMaximo = 20;
    public override RegraDeDesempate RegraCorrespondente() => RegraDeDesempate.MaiorNotaPorAreaDoEnem;
    internal override IEnumerable<FieldError> RecusasDosValores() => ConferirAreas().Erros;
    internal (List<FieldError> Erros, List<(int Indice, string Codigo)> BemFormadas) ConferirAreas() => Areas.Count switch
    {
        0 => ([RecusaDaOrdem(DesempatePorAreaEnemErrorCodes.AreasObrigatorias, "O critério de desempate por área do ENEM exige ao menos uma área na ordem.")], []),
        > AreasMaximo => ([RecusaDaOrdem(DesempatePorAreaEnemErrorCodes.AreasEmExcesso, $"A ordem de desempate por área do ENEM admite no máximo {AreasMaximo} áreas.")], []),
        _ => ConferirCadaArea(),
    };
    private (List<FieldError> Erros, List<(int Indice, string Codigo)> BemFormadas) ConferirCadaArea()
    {
        List<FieldError> erros = [];
        List<(int Indice, string Codigo)> bemFormadas = [];
        for (int indice = 0; indice < Areas.Count; indice++)
        {
            string codigo = Areas[indice];
            FieldError? recusa = RecusaDaArea(indice, codigo);
            if (recusa is not null) { erros.Add(recusa); }
            else { bemFormadas.Add((indice, codigo)); }
        }
        return (erros, bemFormadas);
    }
    private FieldError? RecusaDaArea(int indice, string codigo) => !FormaDoCodigoDeAreaCitada.Aceita(codigo)
        ? new($"areas[{indice}]", new DomainError(DesempatePorAreaEnemErrorCodes.AreaInvalida, $"Cada área da ordem de desempate é {FormaDoCodigoDeAreaCitada.Descricao}."))
        : Areas.Take(indice).Contains(codigo, StringComparer.Ordinal)
            ? new($"areas[{indice}]", new DomainError(DesempatePorAreaEnemErrorCodes.AreaRepetida, $"A área {codigo} aparece mais de uma vez na ordem de desempate.")) : null;
    private static FieldError RecusaDaOrdem(string codigo, string mensagem) => new("areas", new DomainError(codigo, mensagem));

    public ParametrosDesempateMaiorNotaAreaEnem(IReadOnlyList<string> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        Areas = Array.AsReadOnly([.. areas]);
    }

    public IReadOnlyList<string> Areas { get; }

    public bool Equals(ParametrosDesempateMaiorNotaAreaEnem? other) =>
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
