namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um critério de desempate ordenado do <see cref="ProcessoSeletivo"/>:
/// referencia uma regra tipada do
/// <c>regras_do_catalogo</c> (<c>tipo=criterio_desempate</c>) e seus parâmetros
/// aplicados. A <see cref="Ordem"/> carrega a semântica de refinamento
/// sequencial — cada critério desempata só o subgrupo ainda empatado; o
/// seguinte resolve o resíduo.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>: a configuração em rascunho é substituível por
/// inteiro (<see cref="ProcessoSeletivo.DefinirCriteriosDesempate"/>).
/// </remarks>
public sealed class CriterioDesempate : EntityBase, IParteDoProcesso
{
    public Guid ProcessoSeletivoId { get; private set; }
    public int Ordem { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public ParametrosCriterioDesempate Parametros { get; private set; } = null!;

    private CriterioDesempate() { }

    /// <summary>
    /// Cria o critério validando que <paramref name="parametros"/> é a variante
    /// correta para o <see cref="ReferenciaRegra.Codigo"/> referenciado — a
    /// única invariante que esta entidade consegue garantir sozinha; a
    /// existência do <c>etapa_ref</c> no processo é validada pela
    /// raiz, que tem acesso às etapas, e a das áreas do ENEM citadas no quadro
    /// de pesos por área, pela raiz, que tem acesso à classificação.
    /// </summary>
    /// <param name="vocabularioFatos">
    /// O vocabulário fechado de fatos do candidato (ADR-0111),
    /// já resolvido por quem chama (Application, via o leitor cross-módulo).
    /// Só é consultado quando <paramref name="parametros"/> é
    /// <see cref="ParametrosDesempatePredicadoFato"/>. Omitido (<see langword="null"/>)
    /// no caminho de <b>reidratação</b> do envelope congelado — o congelamento dos parâmetros na publicação (UNI-REQ-0019) proíbe
    /// revalidar um predicado já publicado contra o vocabulário vivo.
    /// </param>
    /// <param name="fatosColetadosPeloProcesso">
    /// Repassado a <see cref="PredicadoDnfValidador.Validar"/> — ver
    /// documentação lá. Só tem efeito quando <paramref name="vocabularioFatos"/>
    /// é informado.
    /// </param>
    public static Result<CriterioDesempate> Criar(
        int ordem,
        ReferenciaRegra regra,
        ParametrosCriterioDesempate parametros,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos = null,
        IReadOnlySet<string>? fatosColetadosPeloProcesso = null)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(parametros);

        List<FieldError> erros = [.. ValidarOrdem(ordem), .. RecusaDaCorrespondencia(regra, parametros),
            .. parametros.RecusasDosValores(), .. RecusasDoPredicadoNaGravacao(parametros, vocabularioFatos, fatosColetadosPeloProcesso)];
        return erros.Count > 0 ? Result<CriterioDesempate>.ValidationFailure(erros)
            : Result<CriterioDesempate>.Success(new CriterioDesempate { Ordem = ordem, Regra = regra, Parametros = parametros });
    }

    private static IEnumerable<FieldError> RecusaDaCorrespondencia(ReferenciaRegra regra, ParametrosCriterioDesempate parametros) =>
        regra.Codigo == parametros.RegraCorrespondente().Codigo() ? [] :
        [new("regraCodigo", new DomainError("CriterioDesempate.ParametrosIncompativeisComRegra", "Os parâmetros informados não correspondem à regra referenciada."))];

    private static IEnumerable<FieldError> RecusasDoPredicadoNaGravacao(ParametrosCriterioDesempate parametros,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabulario, IReadOnlySet<string>? coletados) =>
        parametros is ParametrosDesempatePredicadoFato predicado && vocabulario is not null
            ? predicado.ConferirContraOVocabulario(vocabulario, coletados) : [];

    /// <summary>
    /// A ordem não depende do catálogo de regras nem do vocabulário de fatos — ao contrário das
    /// demais checagens de <see cref="Criar"/>, que só fazem sentido depois de a regra e os parâmetros
    /// terem sido resolvidos. Existe separada para o handler, que recusa o critério cuja regra
    /// ou cujos parâmetros não se resolvem antes de chegar a <see cref="Criar"/>, ainda acusar a ordem
    /// junto (ADR-0125).
    /// </summary>
    public static List<FieldError> ValidarOrdem(int ordem)
    {
        List<FieldError> erros = [];

        if (ordem <= 0)
        {
            erros.Add(new("ordem", new DomainError(
                "CriterioDesempate.OrdemInvalida", "A ordem do critério de desempate deve ser maior que zero.")));
        }

        return erros;
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
