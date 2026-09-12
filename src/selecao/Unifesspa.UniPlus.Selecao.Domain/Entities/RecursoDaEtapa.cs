namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
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
    public ArgsRegraPrazoRecurso Args { get; private set; } = null!;

    /// <summary>
    /// O produto preliminar em que o prazo ancora — obrigatório na âncora de ato publicado,
    /// vazio na de ciência individual, que não tem publicação a referenciar.
    /// </summary>
    public Guid ProdutoAncoraId { get; private set; }

    private RecursoDaEtapa() { }

    /// <summary>
    /// Cria a janela recursal. Que a regra exista no catálogo é I/O — Application (ADR-0042);
    /// que o produto âncora pertença à etapa e seja preliminar é conferido pela etapa, que é
    /// quem enxerga os dois lados.
    /// </summary>
    public static Result<RecursoDaEtapa> Criar(
        AncoraDoRecurso ancora,
        ReferenciaRegra regra,
        ArgsRegraPrazoRecurso args,
        Guid produtoAncoraId)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(args);

        if (!Enum.IsDefined(ancora) || ancora == AncoraDoRecurso.Nenhuma)
        {
            return Result<RecursoDaEtapa>.ValidationFailure([new("ancora", new DomainError(
                "RecursoDaEtapa.AncoraObrigatoria",
                "Declare de que instante o prazo corre: da publicação do ato ou da ciência do candidato."))]);
        }

        return Result<RecursoDaEtapa>.Success(new RecursoDaEtapa
        {
            Ancora = ancora,
            Regra = regra,
            Args = args,
            ProdutoAncoraId = produtoAncoraId,
        });
    }

    /// <summary>Reidrata a regra preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static RecursoDaEtapa Reidratar(
        Guid id, AncoraDoRecurso ancora, ReferenciaRegra regra, ArgsRegraPrazoRecurso args, Guid produtoAncoraId)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(args);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O recurso reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }

        return new RecursoDaEtapa
        {
            Id = id, Ancora = ancora, Regra = regra, Args = args, ProdutoAncoraId = produtoAncoraId,
        };
    }

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;
}
