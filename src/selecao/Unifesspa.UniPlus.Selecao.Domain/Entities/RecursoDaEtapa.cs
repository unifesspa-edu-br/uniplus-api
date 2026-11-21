namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Collections.Frozen;

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

    /// <summary>Os prazos, o efeito suspensivo da 1ª instância e a guarda da vaga já conferidos.</summary>
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
    /// Cria a configuração do recurso da etapa. Que a regra exista no catálogo é I/O, de Application (ADR-0042); que a publicação de referência pertença à
    /// etapa e seja preliminar é conferido pela etapa, que é quem enxerga os dois lados. Cada recusa de parâmetro traz o campo
    /// relativo ao recurso.
    /// </summary>
    public static Result<RecursoDaEtapa> Criar(
        AncoraDoRecurso ancora,
        ReferenciaRegra regra,
        ParametrosDeclaradosDoRecurso declarados,
        Guid produtoAncoraId,
        ProcedimentosDeRecurso? procedimentos = null)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(declarados);

        (ParametrosRegraPrazoRecurso? conferidos, IReadOnlyList<RecusaDosParametrosDoRecurso> recusas) = declarados.Conferir();
        List<FieldError> erros = Validar(ancora, regra, recusas);
        return conferidos is null || erros.Count > 0
            ? Result<RecursoDaEtapa>.ValidationFailure(erros)
            : Result<RecursoDaEtapa>.Success(Novo(ancora, regra, conferidos, produtoAncoraId, procedimentos));
    }

    private static RecursoDaEtapa Novo(AncoraDoRecurso ancora, ReferenciaRegra regra,
        ParametrosRegraPrazoRecurso parametros, Guid produtoAncoraId, ProcedimentosDeRecurso? procedimentos) => new()
        {
            Ancora = ancora,
            Regra = regra,
            Parametros = parametros,
            ProdutoAncoraId = produtoAncoraId,
            Procedimentos = procedimentos ?? ProcedimentosDeRecurso.Nenhum,
        };

    private static List<FieldError> Validar(
        AncoraDoRecurso ancora, ReferenciaRegra regra, IReadOnlyList<RecusaDosParametrosDoRecurso> recusas)
    {
        List<FieldError> erros = [];
        AdicionarRecusa(erros, "ancora", ValidarAncora(ancora));
        AdicionarRecusa(erros, null, ValidarRegra(regra));
        erros.AddRange(recusas.Select(static recusa => new FieldError(recusa.Campo, ErroDosParametros(recusa))));
        return erros;
    }

    private static void AdicionarRecusa(List<FieldError> erros, string? campo, DomainError? recusa)
    {
        if (recusa is not null)
        {
            erros.Add(new(campo, recusa));
        }
    }

    private static DomainError? ValidarAncora(AncoraDoRecurso ancora) =>
        Enum.IsDefined(ancora) && ancora != AncoraDoRecurso.Nenhuma ? null : new DomainError(
            "RecursoDaEtapa.AncoraObrigatoria",
            "Declare de que instante o prazo corre: da publicação do ato ou da ciência do candidato.");

    private static DomainError? ValidarRegra(ReferenciaRegra regra) =>
        regra.Codigo == RegraPrazoRecursoCodigo.AncoradoEmAto ? null : new DomainError(
            "RecursoDaEtapa.RegraCatalogoInvalida",
            "A regra indicada não permite contar o prazo de recurso a partir de uma publicação.");

    // Os códigos são literais, e não montados a partir do motivo: é assim que o registro de erros enxerga cada um, porque um código
    // interpolado nunca apareceria na varredura, e um erro sem registro vira 500 em vez de 422.
    private static readonly FrozenDictionary<MotivoDaRecusaDosParametros, string> CodigosDosParametros =
        new Dictionary<MotivoDaRecusaDosParametros, string>
        {
            [MotivoDaRecusaDosParametros.PrazoNaoPositivo] = "RecursoDaEtapa.PrazoNaoPositivo",
            [MotivoDaRecusaDosParametros.PrazoEmDiasCorridos] = "RecursoDaEtapa.PrazoEmDiasCorridos",
            [MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel] = "RecursoDaEtapa.PrazoSemUnidadeDeclaravel",
            [MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil] = "RecursoDaEtapa.PrazoEmFracaoDeDiaUtil",
            [MotivoDaRecusaDosParametros.EfeitoSuspensivoNaoDeclarado] = "RecursoDaEtapa.EfeitoSuspensivoNaoDeclarado",
            [MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel] = "RecursoDaEtapa.GuardaDaVagaNaoDeclaravel",
            [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaNaoDeclarado] = "RecursoDaEtapa.PrazoSegundaInstanciaNaoDeclarado",
            [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaSemInstanciaSuperior] = "RecursoDaEtapa.PrazoSegundaInstanciaSemInstanciaSuperior",
            [MotivoDaRecusaDosParametros.NaoGuardaComInstanciaSuperior] = "RecursoDaEtapa.NaoGuardaComInstanciaSuperior",
        }.ToFrozenDictionary();

    private static DomainError ErroDosParametros(RecusaDosParametrosDoRecurso recusa) =>
        new(CodigosDosParametros[recusa.Motivo], recusa.Mensagem);

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
        ParametrosDeclaradosDoRecurso declarados,
        Guid produtoAncoraId,
        ProcedimentosDeRecurso procedimentos)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(declarados);
        ArgumentNullException.ThrowIfNull(procedimentos);
        ExigirId(id);
        Result<RecursoDaEtapa> validada = Criar(ancora, regra, declarados, produtoAncoraId, procedimentos);
        if (validada.IsFailure)
        {
            return validada;
        }

        return Result<RecursoDaEtapa>.Success(ComId(validada.Value!, id));
    }

    private static void ExigirId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O recurso reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }
    }

    private static RecursoDaEtapa ComId(RecursoDaEtapa recurso, Guid id) => new()
    {
        Id = id,
        Ancora = recurso.Ancora,
        Regra = recurso.Regra,
        Parametros = recurso.Parametros,
        ProdutoAncoraId = recurso.ProdutoAncoraId,
        Procedimentos = recurso.Procedimentos,
    };

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;
}
