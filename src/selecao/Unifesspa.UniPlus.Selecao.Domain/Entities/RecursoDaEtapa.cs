namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma janela recursal aberta por uma <see cref="EtapaProcesso"/> (0..*).
/// </summary>
/// <remarks>
/// <para>
/// É coleção, e não 0..1 como na fase, porque a etapa pode abrir mais de um ciclo: a prova
/// objetiva divulga o gabarito preliminar e, depois, o resultado preliminar da prova — cada
/// um com o seu prazo, correndo de instantes diferentes. Uma regra por fase tornaria o
/// certame inexprimível.
/// </para>
/// <para>
/// A <see cref="Ancora"/> diz de que instante o prazo corre. Contra <b>ato publicado</b>, é
/// o instante da publicação, e a regra referencia o produto preliminar que a abre. Contra
/// <b>ciência individual</b>, é o instante em que o candidato toma ciência da decisão que o
/// alcança — o parecer de indeferimento de um documento, por exemplo —, e aí não há produto
/// a referenciar: a decisão não foi publicada a ninguém além dele.
/// </para>
/// </remarks>
public sealed class RecursoDaEtapa : EntityBase
{
    public Guid EtapaProcessoId { get; private set; }

    /// <summary>De que instante o prazo de interposição corre.</summary>
    public AncoraDoRecurso Ancora { get; private set; }

    /// <summary>Referência simbólica à regra de prazo no catálogo institucional.</summary>
    public ReferenciaRegra Regra { get; private set; } = null!;

    /// <summary>Prazo de interposição e suspensividade, como o catálogo os tipa.</summary>
    public ParametrosRegraPrazoRecurso Parametros { get; private set; } = null!;

    /// <summary>
    /// O produto preliminar em que o prazo ancora — obrigatório na âncora de ato publicado,
    /// vazio na de ciência individual, que não tem publicação a referenciar.
    /// </summary>
    public Guid ProdutoAncoraId { get; private set; }

    /// <summary>Como o recurso de cada matéria que a etapa produz é revisado; vazio quando nenhum foi declarado.</summary>
    public ProcedimentosDeRecurso Procedimentos { get; private set; } = ProcedimentosDeRecurso.Nenhum;

    private RecursoDaEtapa() { }

    /// <summary>
    /// Cria a janela recursal. Que a regra exista no catálogo é I/O — Application (ADR-0042);
    /// que o produto âncora pertença à etapa e seja preliminar é conferido pela etapa, que é
    /// quem enxerga os dois lados.
    /// </summary>
    public static Result<RecursoDaEtapa> Criar(
        AncoraDoRecurso ancora,
        ReferenciaRegra regra,
        ParametrosRegraPrazoRecurso parametros,
        Guid produtoAncoraId,
        ProcedimentosDeRecurso? procedimentos = null)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(parametros);

        if (!Enum.IsDefined(ancora) || ancora == AncoraDoRecurso.Nenhuma)
        {
            return Result<RecursoDaEtapa>.ValidationFailure([new("ancora", new DomainError(
                "RecursoDaEtapa.AncoraObrigatoria",
                "Declare de que instante o prazo corre: da publicação do ato ou da ciência do candidato."))]);
        }

        // Só a regra de prazo ancorado em ato, como na fase — e não qualquer entrada do catálogo
        // cujo tipo seja "regra de prazo de recurso", que é a checagem que o handler faz. O
        // decodificador do envelope exige este código exato ao reidratar a janela recursal da
        // etapa: aceitar outro aqui produziria um certame publicável cujo envelope não volta,
        // e a retificação não conseguiria lê-lo. A defesa fica no domínio para
        // valer também fora do caminho do handler, como a reidratação.
        if (regra.Codigo != RegraPrazoRecursoCodigo.AncoradoEmAto)
        {
            return Result<RecursoDaEtapa>.Failure(new DomainError(
                "RecursoDaEtapa.RegraCatalogoInvalida",
                $"A janela recursal da etapa só referencia a regra {RegraPrazoRecursoCodigo.AncoradoEmAto} — recebido '{regra.Codigo}'."));
        }

        // As mesmas invariantes que a regra da fase prova sobre os parâmetros — prazo estritamente
        // positivo, em dia útil ou hora, sem fração de dia útil, e cada par de suspensividade
        // inteiro ou inteiramente ausente. A janela da etapa recorre nas mesmas condições; o
        // que muda entre as duas é a ÂNCORA, e ela não alcança estas regras.
        if (ValidacaoDosParametrosDoRecurso.Validar(parametros) is { } recusa)
        {
            return Result<RecursoDaEtapa>.Failure(ErroDosParametros(recusa));
        }

        return Result<RecursoDaEtapa>.Success(new RecursoDaEtapa
        {
            Ancora = ancora,
            Regra = regra,
            Parametros = parametros,
            ProdutoAncoraId = produtoAncoraId,
            Procedimentos = procedimentos ?? ProcedimentosDeRecurso.Nenhum,
        });
    }

    /// <summary>
    /// Traduz o motivo da recusa no código de erro desta entidade — literal, para que a
    /// cobertura do registro de erros o alcance.
    /// </summary>
    private static DomainError ErroDosParametros(RecusaDosParametrosDoRecurso recusa) => recusa.Motivo switch
    {
        MotivoDaRecusaDosParametros.PrazoNaoPositivo => new("RecursoDaEtapa.PrazoNaoPositivo", recusa.Mensagem),
        MotivoDaRecusaDosParametros.PrazoEmDiasCorridos => new("RecursoDaEtapa.PrazoEmDiasCorridos", recusa.Mensagem),
        MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel => new("RecursoDaEtapa.PrazoSemUnidadeDeclaravel", recusa.Mensagem),
        MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil => new("RecursoDaEtapa.PrazoEmFracaoDeDiaUtil", recusa.Mensagem),
        MotivoDaRecusaDosParametros.SuspensividadeNaoPositiva => new("RecursoDaEtapa.SuspensividadeNaoPositiva", recusa.Mensagem),
        MotivoDaRecusaDosParametros.SuspensividadeUnidadeNaoDeclaravel => new("RecursoDaEtapa.SuspensividadeUnidadeNaoDeclaravel", recusa.Mensagem),
        MotivoDaRecusaDosParametros.SuspensividadeIncompleta => new("RecursoDaEtapa.SuspensividadeIncompleta", recusa.Mensagem),
        MotivoDaRecusaDosParametros.SuspensividadeSuperiorSemInstanciaSuperior => new("RecursoDaEtapa.SuspensividadeSuperiorSemInstanciaSuperior", recusa.Mensagem),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa.Motivo, "Motivo de recusa desconhecido."),
    };

    /// <summary>
    /// Reidrata a janela preservando o <see cref="EntityBase.Id"/> congelado, sob as mesmas
    /// invariantes de <see cref="Criar"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reidratar não é caminho de confiança. O argumento de que "o envelope já provou a forma
    /// quando foi congelado" vale para envelope que este código gerou, e é exatamente o que não
    /// se pode supor de um envelope truncado, corrompido ou editado — e a retificação lê a
    /// configuração a partir dele. Sem estas conferências, quem controlasse os bytes
    /// reidrataria um certame com prazo recursal zero: uma janela que abre e nunca fecha.
    /// </para>
    /// <para>
    /// A validação é <b>delegada a <see cref="Criar"/></b> em vez de repetida aqui, para que
    /// não exista a chance de as duas divergirem — é o que a irmã da fase já faz. A instância
    /// que ele devolve é descartada porque o <see cref="EntityBase.Id"/> é <c>init</c>, e o
    /// congelado tem de entrar na construção; o custo é um objeto, e a alternativa seria manter
    /// duas listas de invariantes em sincronia à mão.
    /// </para>
    /// </remarks>
    public static Result<RecursoDaEtapa> Reidratar(
        Guid id,
        AncoraDoRecurso ancora,
        ReferenciaRegra regra,
        ParametrosRegraPrazoRecurso parametros,
        Guid produtoAncoraId,
        ProcedimentosDeRecurso procedimentos)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(parametros);
        ArgumentNullException.ThrowIfNull(procedimentos);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O recurso reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }

        Result<RecursoDaEtapa> validada = Criar(ancora, regra, parametros, produtoAncoraId, procedimentos);
        if (validada.IsFailure)
        {
            return validada;
        }

        return Result<RecursoDaEtapa>.Success(new RecursoDaEtapa
        {
            Id = id,
            Ancora = ancora,
            Regra = regra,
            Parametros = parametros,
            ProdutoAncoraId = produtoAncoraId,
            Procedimentos = procedimentos,
        });
    }

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;
}
