namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Collections.Frozen;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de recurso de uma <see cref="FaseCronograma"/> (0..1): a <b>presença</b> desta entidade é o que faz a fase
/// admitir recurso, sem enum, sem flag e sem lista de fases recorríveis em código.
/// </summary>
/// <remarks>
/// <para>
/// Deriva de <see cref="EntityBase"/> puro (sem soft delete), como <see cref="EtapaProcesso"/>. A invariante que depende da
/// fase-mãe, existir o produto preliminar de que o prazo conta, é conferida por <see cref="FaseCronograma.Criar"/>, que tem
/// acesso aos dois lados. As que esta entidade prova sozinha ficam aqui: a coerência da regra referenciada e a declaração do
/// recurso, que <see cref="ParametrosDeclaradosDoRecurso.Conferir"/> confere (prazos, efeito suspensivo, guarda da vaga e
/// instância superior).
/// </para>
/// <para>
/// Essas invariantes vivem aqui, e não só no validator da porta HTTP, porque há caminho de construção que não passa por ela: a
/// leitura do documento da configuração e a do registro da versão publicada chamam <see cref="Criar"/> e <see cref="Reidratar"/>.
/// O validator recusa antes só a forma; quem chega por outro caminho encontra estas recusas, cada uma com erro nomeado próprio.
/// </para>
/// <para>
/// A convenção de contagem que o certame usa <b>não</b> é verificada aqui. Ela é uma por processo, e esta entidade não enxerga a
/// raiz: a conferência correspondente é invariante de <see cref="ProcessoSeletivo"/>, aplicada nas transições que geram versão.
/// </para>
/// </remarks>
public sealed class RegraRecursoFase : EntityBase
{
    public Guid FaseCronogramaId { get; private set; }

    public ReferenciaRegra Regra { get; private set; } = null!;

    public ParametrosRegraPrazoRecurso Parametros { get; private set; } = null!;

    /// <summary>
    /// O <c>ProdutoDaFase</c> em que o prazo de interposição ancora: sempre o resultado
    /// preliminar publicado pela PRÓPRIA fase, referenciado pela identidade da linha e não
    /// pelo código do tipo de ato.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Duas fases do mesmo cronograma podem publicar o mesmo tipo de ato, e o código
    /// sozinho não distingue qual das duas publicações abre a janela do candidato. A
    /// identidade do produto distingue, e o <c>UNI-REQ-0115</c> continua satisfeito: o
    /// prazo segue contando do instante absoluto de publicação daquele ato.
    /// </para>
    /// <para>
    /// Quem configura declara o tipo de ato; Application o resolve <b>dentro da coleção de
    /// produtos daquela fase</b>, e é o identificador resolvido que chega aqui — por isso
    /// ancorar na publicação de outra fase deixa de ser exprimível. Que o produto exista na
    /// fase e tenha papel preliminar é conferido por <see cref="FaseCronograma.Criar"/>, o
    /// único ponto que enxerga os dois lados.
    /// </para>
    /// </remarks>
    public Guid ProdutoAncoraId { get; private set; }

    /// <summary>Como o recurso de cada matéria que a fase produz é revisado; vazio quando nenhum foi declarado.</summary>
    public ProcedimentosDeRecurso Procedimentos { get; private set; } = ProcedimentosDeRecurso.Nenhum;

    private RegraRecursoFase() { }

    /// <summary>
    /// Cria a regra de recurso da fase. Não resolve nem confere a existência da <paramref name="regra"/> no catálogo (isso é
    /// I/O, de Application, via <c>IRegraCatalogoReader</c>, ADR-0042) nem os atributos do tipo de ato cuja publicação inicia o prazo (Application,
    /// via <c>ITipoAtoPublicadoReader</c>): só as invariantes puras, a referência de catálogo por símbolo e a declaração do
    /// recurso (UNI-REQ-0081, 0113 e 0348). Cada recusa de parâmetro traz o campo relativo à regra.
    /// </summary>
    /// <remarks>
    /// <paramref name="produtoAncoraId"/> vazio é o estado em que Application chega quando o tipo de ato declarado não
    /// corresponde a produto nenhum da fase. Ele não é recusado aqui: quem tem os dois lados para dizer o que falta é
    /// <see cref="FaseCronograma"/>, e antecipar a recusa devolveria uma mensagem que não sabe nomear os produtos disponíveis.
    /// </remarks>
    public static Result<RegraRecursoFase> Criar(
        ReferenciaRegra regra,
        ParametrosDeclaradosDoRecurso declarados,
        Guid produtoAncoraId,
        ProcedimentosDeRecurso? procedimentos = null)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(declarados);

        (ParametrosRegraPrazoRecurso? conferidos, IReadOnlyList<RecusaDosParametrosDoRecurso> recusas) = declarados.Conferir();
        List<FieldError> erros = [.. RecusaDaRegra(regra), .. recusas.Select(ComoErro)];
        return conferidos is null || erros.Count > 0
            ? Result<RegraRecursoFase>.ValidationFailure(erros)
            : Result<RegraRecursoFase>.Success(new RegraRecursoFase
            {
                Regra = regra,
                Parametros = conferidos,
                ProdutoAncoraId = produtoAncoraId,
                Procedimentos = procedimentos ?? ProcedimentosDeRecurso.Nenhum,
            });
    }

    private static IEnumerable<FieldError> RecusaDaRegra(ReferenciaRegra regra) =>
        regra.Codigo == RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo()
            ? []
            : [new FieldError(string.Empty, new DomainError(
                "RegraRecursoFase.RegraCatalogoInvalida",
                "A regra indicada não permite contar o prazo de recurso a partir de uma publicação."))];

    /// <summary>
    /// As recusas dos parâmetros declarados, no campo relativo à regra, sem a conferência da referência de catálogo. Serve a
    /// quem acumula as recusas da regra recusada por outro motivo.
    /// </summary>
    internal static IReadOnlyList<FieldError> RecusasDosParametros(ParametrosDeclaradosDoRecurso declarados) =>
        [.. declarados.Conferir().Recusas.Select(ComoErro)];

    private static FieldError ComoErro(RecusaDosParametrosDoRecurso recusa) => new(recusa.Campo, ErroDosParametros(recusa));

    // Os códigos são literais, e não montados a partir do motivo: é assim que o registro de erros enxerga cada um, porque um código
    // interpolado nunca apareceria na varredura, e um erro sem registro vira 500 em vez de 422.
    private static readonly FrozenDictionary<MotivoDaRecusaDosParametros, string> CodigosDosParametros =
        new Dictionary<MotivoDaRecusaDosParametros, string>
        {
            [MotivoDaRecusaDosParametros.PrazoNaoPositivo] = "RegraRecursoFase.PrazoNaoPositivo",
            [MotivoDaRecusaDosParametros.PrazoEmDiasCorridos] = "RegraRecursoFase.PrazoEmDiasCorridos",
            [MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel] = "RegraRecursoFase.PrazoSemUnidadeDeclaravel",
            [MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil] = "RegraRecursoFase.PrazoEmFracaoDeDiaUtil",
            [MotivoDaRecusaDosParametros.EfeitoSuspensivoNaoDeclarado] = "RegraRecursoFase.EfeitoSuspensivoNaoDeclarado",
            [MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel] = "RegraRecursoFase.GuardaDaVagaNaoDeclaravel",
            [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaNaoDeclarado] = "RegraRecursoFase.PrazoSegundaInstanciaNaoDeclarado",
            [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaSemInstanciaSuperior] = "RegraRecursoFase.PrazoSegundaInstanciaSemInstanciaSuperior",
            [MotivoDaRecusaDosParametros.NaoGuardaComInstanciaSuperior] = "RegraRecursoFase.NaoGuardaComInstanciaSuperior",
        }.ToFrozenDictionary();

    private static DomainError ErroDosParametros(RecusaDosParametrosDoRecurso recusa) =>
        new(recusa.Motivo == MotivoDaRecusaDosParametros.PrazoPrecisaoInvalida ? CodigosDeValidacao.Formato : CodigosDosParametros[recusa.Motivo], recusa.Mensagem);

    /// <summary>
    /// Reconstrói a regra a partir do registro da versão publicada, preservando a publicação de referência que estava vigente quando a versão foi
    /// registrada: é contra ela que o ato publicado resolverá, de volta, a configuração de recurso que lhe corresponde
    /// (UNI-REQ-0093).
    /// </summary>
    /// <remarks>
    /// As invariantes puras continuam valendo na reconstrução, pelo mesmo motivo de <see cref="Criar"/>: o registro é caminho de
    /// construção que não passa pela porta HTTP, e uma regra reconstruída com prazo não positivo seria tão inutilizável quanto
    /// uma escrita assim pela primeira vez.
    /// </remarks>
    public static Result<RegraRecursoFase> Reidratar(
        ReferenciaRegra regra,
        ParametrosDeclaradosDoRecurso declarados,
        Guid produtoAncoraId,
        ProcedimentosDeRecurso procedimentos)
    {
        ArgumentNullException.ThrowIfNull(procedimentos);
        if (produtoAncoraId == Guid.Empty)
        {
            throw new ArgumentException(
                "A regra de recurso reidratada deve declarar a publicação de referência registrada na versão publicada.",
                nameof(produtoAncoraId));
        }

        return Criar(regra, declarados, produtoAncoraId, procedimentos);
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;

    /// <summary>
    /// Reaponta a âncora para o produto que sobreviveu à reconciliação de
    /// <see cref="FaseCronograma.AtualizarDe"/> — ver o <c>&lt;remarks&gt;</c> de
    /// <see cref="ProcessosSeletivos.Cronograma.AncoraDoRecursoDaFase.Reancorar"/>.
    /// </summary>
    internal void RemapearAncora(Guid produtoAncoraId) => ProdutoAncoraId = produtoAncoraId;
}
