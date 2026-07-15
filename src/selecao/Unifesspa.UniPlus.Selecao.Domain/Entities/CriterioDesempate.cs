namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um critério de desempate ordenado do <see cref="ProcessoSeletivo"/> (Story
/// #774, modelagem P-B §2.6): referencia uma regra tipada do
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
    /// raiz, que tem acesso às etapas.
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
    public static Result<CriterioDesempate> Criar(
        int ordem,
        ReferenciaRegra regra,
        ArgsCriterioDesempate args,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos = null,
        IReadOnlySet<string>? fatosColetadosPeloProcesso = null)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(args);

        if (ordem <= 0)
        {
            return Result<CriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.OrdemInvalida", "A ordem do critério de desempate deve ser maior que zero."));
        }

        bool argsCompativeis = regra.Codigo switch
        {
            CriterioDesempateCodigo.MaiorNotaEtapa => args is ArgsDesempateMaiorNotaEtapa,
            CriterioDesempateCodigo.MaiorIdade => args is ArgsDesempateMaiorIdade,
            CriterioDesempateCodigo.Idoso => args is ArgsDesempateIdoso,
            CriterioDesempateCodigo.PredicadoFato => args is ArgsDesempatePredicadoFato,
            _ => false,
        };

        if (!argsCompativeis)
        {
            return Result<CriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.ArgsIncompativeisComRegra",
                $"Os args informados não correspondem à regra {regra.Codigo}."));
        }

        if (args is ArgsDesempateIdoso { IdadeMinima: <= 0 })
        {
            return Result<CriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.IdadeMinimaInvalida", "A idade mínima do critério IDOSO deve ser maior que zero."));
        }

        if (args is ArgsDesempatePredicadoFato predicadoFato && vocabularioFatos is not null)
        {
            Result<PredicadoDnf> predicadoResult = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, predicadoFato.Condicao)]);
            if (predicadoResult.IsFailure)
            {
                return Result<CriterioDesempate>.Failure(predicadoResult.Error!);
            }

            Result validacaoResult = PredicadoDnfValidador.Validar(predicadoResult.Value!, vocabularioFatos, fatosColetadosPeloProcesso);
            if (validacaoResult.IsFailure)
            {
                return Result<CriterioDesempate>.Failure(validacaoResult.Error!);
            }
        }

        return Result<CriterioDesempate>.Success(new CriterioDesempate
        {
            Ordem = ordem,
            Regra = regra,
            Args = args,
        });
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
