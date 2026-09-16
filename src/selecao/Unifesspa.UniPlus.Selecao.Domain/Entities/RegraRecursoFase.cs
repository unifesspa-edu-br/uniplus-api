namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de recurso de uma <see cref="FaseCronograma"/> (0..1, Story #851 §3.6): a
/// <b>presença</b> desta entidade é o que faz a fase admitir recurso — sem enum, sem
/// flag, sem lista de fases recorríveis em código.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>. A invariante que depende da fase-mãe — existir o produto
/// preliminar em que o prazo ancora — é validada por <see cref="FaseCronograma.Criar"/>,
/// que tem acesso aos dois lados; as que esta entidade consegue provar sozinha ficam aqui:
/// a coerência da regra referenciada, as unidades declaráveis e a magnitude do prazo de
/// interposição, e a completude dos pares de suspensividade.
/// </remarks>
/// <remarks>
/// Essas invariantes vivem aqui, e não só no validator da porta HTTP, porque existe
/// caminho de construção que não passa por ela: a reidratação do envelope chama
/// <see cref="Reidratar"/> ao restaurar configuração congelada. O validator continua
/// existindo e recusa antes, campo a campo, com mensagem por campo — quem chega pelo HTTP
/// recebe o 400 detalhado dele; quem chega por outro caminho encontra estas recusas, cada
/// uma com erro nomeado próprio.
/// </remarks>
/// <remarks>
/// A convenção de contagem que o certame usa <b>não</b> é verificada aqui. Ela é uma por
/// processo, e esta entidade não enxerga a raiz — o gate correspondente é invariante de
/// <see cref="ProcessoSeletivo"/>, aplicado nas transições que geram versão.
/// </remarks>
public sealed class RegraRecursoFase : EntityBase
{
    public Guid FaseCronogramaId { get; private set; }

    public ReferenciaRegra Regra { get; private set; } = null!;

    public ArgsRegraPrazoRecurso Args { get; private set; } = null!;

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

    private RegraRecursoFase() { }

    /// <summary>
    /// Cria a regra de recurso da fase. Não resolve nem confere a existência da
    /// <paramref name="regra"/> no catálogo (isso é I/O — Application, via
    /// <c>IRegraCatalogoReader</c>, ADR-0042) nem os atributos do tipo de ato âncora
    /// (Application, via <c>ITipoAtoPublicadoReader</c>) — só as invariantes puras que este
    /// VO consegue provar sozinho: a referência de catálogo por símbolo, as unidades
    /// declaráveis e a magnitude estritamente positiva do prazo de interposição
    /// (UNI-REQ-0081/0113), e a completude de cada par de suspensividade (UNI-REQ-0080).
    /// </summary>
    /// <remarks>
    /// <paramref name="produtoAncoraId"/> vazio é o estado em que Application chega quando o
    /// tipo de ato declarado não corresponde a produto nenhum da fase. Ele não é recusado
    /// aqui: quem tem os dois lados para dizer o que falta é <see cref="FaseCronograma"/>,
    /// e antecipar a recusa devolveria uma mensagem que não sabe nomear os produtos
    /// disponíveis.
    /// </remarks>
    public static Result<RegraRecursoFase> Criar(
        ReferenciaRegra regra,
        ArgsRegraPrazoRecurso args,
        Guid produtoAncoraId)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(args);

        // CA-01/CA-02: a regra referenciada só pode ser RECURSO-PRAZO-ANCORADO-EM-ATO —
        // qualquer outra (inclusive de outro TipoRegra) é recusada. A checagem completa
        // (regra existe, TipoRegra == RegraPrazoRecurso, hash bate) é do handler
        // (IRegraCatalogoReader, D9); esta é a defesa de forma, com o MESMO código de
        // erro, para que uma RegraRecursoFase nunca exista com uma referência incoerente
        // mesmo se construída fora do caminho do handler (ex.: reidratação do envelope).
        if (regra.Codigo != RegraPrazoRecursoCodigo.AncoradoEmAto)
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.RegraCatalogoInvalida",
                $"RegraRecursoFase só referencia a regra {RegraPrazoRecursoCodigo.AncoradoEmAto} — recebido '{regra.Codigo}'."));
        }

        // As invariantes puras sobre os args — prazo e os dois pares de suspensividade — são
        // do próprio value object: ele é o mesmo que a janela recursal da etapa carrega, e
        // enquanto elas viviam aqui a etapa aceitava prazo zero e par pela metade.
        if (ValidacaoDeArgsDeRecurso.Validar(args) is { } recusa)
        {
            return Result<RegraRecursoFase>.Failure(ErroDosArgs(recusa));
        }

        return Result<RegraRecursoFase>.Success(
            new RegraRecursoFase { Regra = regra, Args = args, ProdutoAncoraId = produtoAncoraId });
    }

    /// <summary>
    /// Traduz o motivo da recusa no código de erro desta entidade.
    ///
    /// Os códigos são literais, e não montados a partir do motivo: é assim que a cobertura do
    /// registro de erros consegue enxergá-los — um código interpolado nunca apareceria na
    /// varredura, e um erro sem registro vira 500 em vez de 422.
    /// </summary>
    private static DomainError ErroDosArgs(RecusaDeArgsDeRecurso recusa) => recusa.Motivo switch
    {
        MotivoDeRecusaDeArgs.PrazoNaoPositivo => new("RegraRecursoFase.PrazoNaoPositivo", recusa.Mensagem),
        MotivoDeRecusaDeArgs.PrazoEmDiasCorridos => new("RegraRecursoFase.PrazoEmDiasCorridos", recusa.Mensagem),
        MotivoDeRecusaDeArgs.PrazoSemUnidadeDeclaravel => new("RegraRecursoFase.PrazoSemUnidadeDeclaravel", recusa.Mensagem),
        MotivoDeRecusaDeArgs.PrazoEmFracaoDeDiaUtil => new("RegraRecursoFase.PrazoEmFracaoDeDiaUtil", recusa.Mensagem),
        MotivoDeRecusaDeArgs.SuspensividadeNaoPositiva => new("RegraRecursoFase.SuspensividadeNaoPositiva", recusa.Mensagem),
        MotivoDeRecusaDeArgs.SuspensividadeUnidadeNaoDeclaravel => new("RegraRecursoFase.SuspensividadeUnidadeNaoDeclaravel", recusa.Mensagem),
        MotivoDeRecusaDeArgs.SuspensividadeIncompleta => new("RegraRecursoFase.SuspensividadeIncompleta", recusa.Mensagem),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa.Motivo, "Motivo de recusa desconhecido."),
    };

    /// <summary>
    /// Reidrata a regra a partir de uma <c>VersaoConfiguracao</c> congelada, preservando a
    /// âncora que estava vigente quando o snapshot foi produzido — é contra ela que o ato
    /// publicado resolverá, de volta, a configuração de recurso que lhe corresponde
    /// (UNI-REQ-0093).
    /// </summary>
    /// <remarks>
    /// As invariantes puras continuam valendo na reidratação, pelo mesmo motivo de
    /// <see cref="Criar"/>: o envelope é o caminho de construção que não passa pela porta
    /// HTTP, e uma regra restaurada com prazo não positivo seria tão inutilizável quanto
    /// uma escrita assim pela primeira vez.
    /// </remarks>
    public static Result<RegraRecursoFase> Reidratar(
        ReferenciaRegra regra,
        ArgsRegraPrazoRecurso args,
        Guid produtoAncoraId)
    {
        if (produtoAncoraId == Guid.Empty)
        {
            throw new ArgumentException(
                "A regra de recurso reidratada deve declarar o produto âncora congelado no envelope.",
                nameof(produtoAncoraId));
        }

        return Criar(regra, args, produtoAncoraId);
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;

    /// <summary>
    /// Reaponta a âncora para o produto que sobreviveu à reconciliação de
    /// <see cref="FaseCronograma.AtualizarSnapshot"/> — ver o <c>&lt;remarks&gt;</c> de lá.
    /// </summary>
    internal void RemapearAncora(Guid produtoAncoraId) => ProdutoAncoraId = produtoAncoraId;
}
