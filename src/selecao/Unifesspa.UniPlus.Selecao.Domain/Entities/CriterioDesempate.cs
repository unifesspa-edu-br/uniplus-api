namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um critério de desempate ordenado do <see cref="ProcessoSeletivo"/> (Story
/// #774): referencia uma regra tipada do
/// <c>rol_de_regras</c> (<c>tipo=criterio_desempate</c>) e seus args
/// aplicados. A <see cref="Ordem"/> carrega a semântica de refinamento
/// sequencial — cada critério desempata só o subgrupo ainda empatado; o
/// seguinte resolve o resíduo.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>: a configuração em rascunho é substituível por
/// inteiro (<see cref="ProcessoSeletivo.DefinirCriteriosDesempate"/>).
/// </remarks>
public sealed class CriterioDesempate : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }
    public int Ordem { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public ArgsCriterioDesempate Args { get; private set; } = null!;

    private CriterioDesempate() { }

    /// <summary>
    /// Cria o critério validando que <paramref name="args"/> é a variante
    /// correta para o <see cref="ReferenciaRegra.Codigo"/> referenciado — a
    /// única invariante que esta entidade consegue garantir sozinha; a
    /// existência do <c>etapa_ref</c> no processo (INV-B6) é validada pela
    /// raiz, que tem acesso às etapas, e a das áreas do ENEM citadas no quadro
    /// de pesos por área, pela raiz, que tem acesso à classificação.
    /// </summary>
    /// <param name="vocabularioFatos">
    /// O vocabulário fechado de fatos do candidato (ADR-0111, Story #847),
    /// já resolvido por quem chama (Application, via o leitor cross-módulo
    /// do #846). Só é consultado quando <paramref name="args"/> é
    /// <see cref="ArgsDesempatePredicadoFato"/>. Omitido (<see langword="null"/>)
    /// no caminho de <b>reidratação</b> do envelope congelado — RN08 proíbe
    /// revalidar um predicado já publicado contra o vocabulário vivo.
    /// </param>
    /// <param name="fatosColetadosPeloProcesso">
    /// Repassado a <see cref="PredicadoDnfValidador.Validar"/> — ver
    /// documentação lá. Só tem efeito quando <paramref name="vocabularioFatos"/>
    /// é informado.
    /// </param>
    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — ordem,
    /// compatibilidade de args, idade mínima e a forma da ordem de áreas do ENEM não dependem
    /// umas das outras.
    /// </summary>
    public static Result<CriterioDesempate> Criar(
        int ordem,
        ReferenciaRegra regra,
        ArgsCriterioDesempate args,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos = null,
        IReadOnlySet<string>? fatosColetadosPeloProcesso = null)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(args);

        List<FieldError> erros = ValidarOrdem(ordem);

        bool argsCompativeis = regra.Codigo switch
        {
            CriterioDesempateCodigo.MaiorNotaEtapa => args is ArgsDesempateMaiorNotaEtapa,
            CriterioDesempateCodigo.MaiorIdade => args is ArgsDesempateMaiorIdade,
            CriterioDesempateCodigo.Idoso => args is ArgsDesempateIdoso,
            CriterioDesempateCodigo.PredicadoFato => args is ArgsDesempatePredicadoFato,
            CriterioDesempateCodigo.MaiorNotaAreaEnem => args is ArgsDesempateMaiorNotaAreaEnem,
            _ => false,
        };

        if (!argsCompativeis)
        {
            erros.Add(new("regraCodigo", new DomainError(
                "CriterioDesempate.ArgsIncompativeisComRegra",
                "Os args informados não correspondem à regra referenciada.")));
        }

        if (args is ArgsDesempateIdoso { IdadeMinima: <= 0 })
        {
            erros.Add(new("idadeMinima", new DomainError(
                "CriterioDesempate.IdadeMinimaInvalida", "A idade mínima do critério IDOSO deve ser maior que zero.")));
        }

        if (args is ArgsDesempateMaiorNotaAreaEnem areaEnem)
        {
            erros.AddRange(ConferirAreas(areaEnem.Areas).Erros);
        }

        if (args is ArgsDesempatePredicadoFato predicadoFato && vocabularioFatos is not null)
        {
            Result<PredicadoDnf> predicadoResult = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, predicadoFato.Condicao)]);
            if (predicadoResult.IsFailure)
            {
                erros.Add(new(CampoDaRecusaDoPredicado(predicadoResult.Error!), predicadoResult.Error!));
            }
            else
            {
                Result validacaoResult = PredicadoDnfValidador.Validar(predicadoResult.Value!, vocabularioFatos, fatosColetadosPeloProcesso);
                if (validacaoResult.IsFailure)
                {
                    erros.Add(new(CampoDaRecusaDoPredicado(validacaoResult.Error!), validacaoResult.Error!));
                }
            }
        }

        if (erros.Count > 0)
        {
            return Result<CriterioDesempate>.ValidationFailure(erros);
        }

        return Result<CriterioDesempate>.Success(new CriterioDesempate
        {
            Ordem = ordem,
            Regra = regra,
            Args = args,
        });
    }

    /// <summary>
    /// A ordem não depende do catálogo de regras nem do vocabulário de fatos — ao contrário das
    /// demais checagens de <see cref="Criar"/>, que só fazem sentido depois de a regra e os args
    /// terem sido resolvidos. Existe separada para o handler, que recusa o critério cuja regra
    /// ou cujos args não se resolvem antes de chegar a <see cref="Criar"/>, ainda acusar a ordem
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

    /// <summary>
    /// Quantas áreas a ordem de desempate por área do ENEM admite. O quadro de pesos por área
    /// tem cinco áreas; o teto só impede uma lista sem limite enquanto a classificação, contra
    /// a qual as áreas são conferidas, ainda não foi definida.
    /// </summary>
    public const int AreasMaximo = 20;

    /// <summary>
    /// O que a ordem de áreas consegue provar sozinha: há ao menos uma área, cada código tem
    /// forma de código (letras maiúsculas sem acento, algarismos e sublinhado, até o tamanho da
    /// coluna) e nenhum se repete. A forma é regra do critério, e não do cadastro: lá as áreas
    /// são uma lista fechada de cinco códigos, todos nessa forma. Ela recusa texto livre
    /// enquanto a classificação não existe e não há quadro contra o qual conferir; se o código
    /// existe no quadro congelado é conferido pela raiz, que conhece a classificação.
    /// </summary>
    /// <returns>
    /// As recusas e as áreas bem formadas, com a posição de cada uma: só estas a raiz confere
    /// contra os outros critérios e contra o quadro, para o mesmo campo não receber duas recusas.
    /// </returns>
    internal static (List<FieldError> Erros, List<(int Indice, string Codigo)> BemFormadas) ConferirAreas(IReadOnlyList<string>? areas)
    {
        List<FieldError> erros = [];
        List<(int Indice, string Codigo)> bemFormadas = [];

        if (areas is null || areas.Count == 0)
        {
            erros.Add(new("areas", new DomainError(
                DesempatePorAreaEnemErrorCodes.AreasObrigatorias,
                "O critério de desempate por área do ENEM exige ao menos uma área na ordem.")));
            return (erros, bemFormadas);
        }

        if (areas.Count > AreasMaximo)
        {
            erros.Add(new("areas", new DomainError(
                DesempatePorAreaEnemErrorCodes.AreasEmExcesso,
                $"A ordem de desempate por área do ENEM admite no máximo {AreasMaximo} áreas.")));
            return (erros, bemFormadas);
        }

        HashSet<string> vistas = new(StringComparer.Ordinal);
        for (int indice = 0; indice < areas.Count; indice++)
        {
            string? codigo = areas[indice];
            if (!CodigoDeAreaValido(codigo))
            {
                erros.Add(new($"areas[{indice}]", new DomainError(
                    DesempatePorAreaEnemErrorCodes.AreaInvalida,
                    $"Cada área da ordem de desempate é um código de até {GrupoPesoAreaEnemCongelado.AreaCodigoMaxLength} caracteres, com letras maiúsculas sem acento, algarismos e sublinhado.")));
                continue;
            }

            if (!vistas.Add(codigo!))
            {
                erros.Add(new($"areas[{indice}]", new DomainError(
                    DesempatePorAreaEnemErrorCodes.AreaRepetida,
                    $"A área {codigo} aparece mais de uma vez na ordem de desempate.")));
                continue;
            }

            bemFormadas.Add((indice, codigo!));
        }

        return (erros, bemFormadas);
    }

    /// <summary>A recusa do predicado aponta o campo do critério que a corrige.</summary>
    private static string CampoDaRecusaDoPredicado(DomainError erro) => erro.Code switch
    {
        PredicadoDnfErrorCodes.OperadorIncompativelComDominio => "operador",
        PredicadoDnfErrorCodes.ValorIncompativelComTipo or PredicadoDnfErrorCodes.ValorForaDoDominio => "valor",
        _ => "fato",
    };

    internal static bool CodigoDeAreaValido(string? codigo) =>
        !string.IsNullOrEmpty(codigo)
        && codigo.Length <= GrupoPesoAreaEnemCongelado.AreaCodigoMaxLength
        && codigo.All(static c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
