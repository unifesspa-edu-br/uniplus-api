namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using ProcessosSeletivos.Bancas;
using ProcessosSeletivos.Etapas;

using Services;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Etapa <em>pontuada</em> do <see cref="ProcessoSeletivo"/> (peso, caráter, nota mínima e escala do
/// lançamento), distinta da fase do cronograma, que é o eixo temporal do certame. Entidade interna do
/// agregado: criada, substituída e persistida exclusivamente pela raiz.
/// </summary>
/// <remarks>
/// Sem soft delete: a configuração em rascunho é substituível por inteiro, e a trilha do que valeu em
/// cada publicação é o snapshot de congelamento (UNI-REQ-0019).
/// </remarks>
public sealed class EtapaProcesso : EntityBase, IParteDoProcesso
{
    /// <summary>Alinhado a <c>EtapaProcessoConfiguration</c> (varchar(300)).</summary>
    public const int NomeMaxLength = 300;

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>
    /// Código canônico da fase onde a etapa acontece (ex.: <c>"AVALIACAO"</c>); é por código, e não por
    /// id, porque o id da fase não sobrevive à reconciliação da restauração.
    /// </summary>
    public string? FaseCodigo { get; private set; }

    public string Nome { get; private set; } = string.Empty;
    public CaraterEtapa Carater { get; private set; }

    /// <summary>Cópia por valor do tipo de etapa resolvido em Configuração (ADR-0061), a identidade que a conformidade avalia.</summary>
    public TipoEtapaSnapshot TipoEtapa { get; private set; } = null!;

    public Guid TipoEtapaOrigemId => TipoEtapa.OrigemId;
    public decimal? Peso { get; private set; }
    public decimal? NotaMinima { get; private set; }

    /// <summary>Teto da escala do lançamento; ausente na etapa sem nota e na de nota do ENEM (escala do INEP).</summary>
    public decimal? NotaMaxima { get; private set; }

    /// <summary>De quanto em quanto a nota pode ser lançada; vale só para a nota digitada.</summary>
    public decimal? PassoDoLancamento { get; private set; }

    /// <summary>
    /// Como a nota da etapa é obtida, declarada pela etapa e não pelo tipo. Ausente em rascunho, na
    /// etapa sem nota e na de nota do ENEM.
    /// </summary>
    public FormaDeAvaliacao? FormaDeAvaliacao { get; private set; }

    /// <summary>
    /// Como as notas dos avaliadores viram a nota da etapa (só na forma humana): referência ao catálogo
    /// (código, versão e hash), congelada no snapshot como as regras da classificação.
    /// </summary>
    public ReferenciaRegra? RegraConsolidacao { get; private set; }

    /// <summary>
    /// Os níveis entre os quais a banca escolhe em cada item, do de maior fração ao de menor; ausente
    /// quando a etapa lança nota digitada.
    /// </summary>
    public IReadOnlyList<NivelDeAtendimento>? EscalaDeAtendimento { get; private set; }

    public int? Ordem { get; private set; }

    /// <summary>Início da janela própria da etapa, em UTC; ausente quando ela herda a da fase.</summary>
    public DateTimeOffset? Inicio { get; private set; }

    /// <summary>Fim da janela própria, em UTC.</summary>
    public DateTimeOffset? Fim { get; private set; }

    /// <summary>Se a etapa promete parecer individual por candidato, de que o candidato tira fundamento para recorrer.</summary>
    public bool EmiteParecerIndividual { get; private set; }

    private readonly List<ProdutoDaEtapa> _produtos = [];
    private readonly List<BancaDaEtapa> _bancas = [];
    private readonly List<RecursoDaEtapa> _recursos = [];
    private readonly List<ItemDeAvaliacao> _itensDeAvaliacao = [];
    private readonly List<string> _tiposDeDecisao = [];

    /// <summary>Os tipos de decisão que a etapa declara produzir, em ordem alfabética ordinal e sem repetição.</summary>
    public IReadOnlyList<string> TiposDeDecisao => _tiposDeDecisao.AsReadOnly();

    /// <summary>Os itens em que a nota da etapa se divide, em ordem canônica: pelo conteúdo, nunca pelo id.</summary>
    public IReadOnlyList<ItemDeAvaliacao> ItensDeAvaliacao => [.. _itensDeAvaliacao
        .OrderBy(static i => HashCanonicalComputer.NormalizeNfc(i.Rotulo), StringComparer.Ordinal)
        .ThenBy(static i => i.PontuacaoMaxima)
        .ThenBy(static i => i.Id)];

    /// <summary>As bancas que a etapa requer (0..*).</summary>
    public IReadOnlyCollection<BancaDaEtapa> Bancas => _bancas.AsReadOnly();

    /// <summary>As janelas recursais que a etapa abre (0..*).</summary>
    public IReadOnlyCollection<RecursoDaEtapa> Recursos => _recursos.AsReadOnly();

    /// <summary>Tudo o que a etapa publica, com o papel de cada publicação (0..*).</summary>
    public IReadOnlyCollection<ProdutoDaEtapa> Produtos => _produtos.AsReadOnly();

    /// <summary>A etapa produz resultado quando declara ao menos um produto com papel.</summary>
    public bool ProduzResultado => _produtos.Exists(static p => p.Papel is not null);

    /// <summary>
    /// A etapa compõe a nota final quando o caráter pontua e ela declara peso: é o critério que a inclui
    /// no divisor da média (<see cref="ProcessoSeletivo.CalcularDivisorMedia"/>).
    /// </summary>
    public bool ComponeNota => CaraterDaEtapa.Pontua(Carater) && Peso.HasValue;

    /// <summary>A etapa declara que a nota vem do ENEM, atributo do tipo congelado que o motor de classificação consome.</summary>
    public bool DeclaraNotaDoEnem => TipoEtapa.NotaDeOrigemNoEnem;

    private EtapaProcesso() { }

    /// <summary>
    /// Cria a etapa acumulando toda violação independente (ADR-0125), com as mesmas recusas e a mesma
    /// ordem de <see cref="AtualizarDados"/>. A identidade é do sistema e os itens entram todos como novos.
    /// A precisão decimal de peso e notas (<c>numeric(18,4)</c>) fica só no validator.
    /// </summary>
    public static Result<EtapaProcesso> Criar(
        IdentificacaoDaEtapa identificacao,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens = null)
    {
        EtapaProcesso etapa = new();
        Result declarada = etapa.AtualizarDados(identificacao, avaliacao, itens);

        return declarada.IsSuccess
            ? Result<EtapaProcesso>.Success(etapa)
            : Result<EtapaProcesso>.ValidationFailure(declarada.Errors);
    }

    /// <summary>
    /// Reidrata uma etapa de uma <see cref="VersaoConfiguracao"/> congelada, <b>preservando o
    /// <see cref="EntityBase.Id"/></b>, que <see cref="Criar"/> não aceita.
    /// </summary>
    /// <remarks>
    /// O id da etapa e o de cada item de avaliação (<paramref name="itens"/>) são congelados no
    /// envelope porque o desempate, a eliminação, o lançamento e o recurso apontam para eles; os ids das
    /// demais filhas são regenerados. Os dados vêm de um documento já validado, e o decoder recusa
    /// bytes inválidos com <c>Result</c>: as guardas aqui são só contra erro de programação.
    /// </remarks>
    public static EtapaProcesso Reidratar(
        Guid id,
        IdentificacaoDaEtapa identificacao,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        IReadOnlyList<ItemDeAvaliacao>? itens)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ConferenciaDaDeclaracaoDaEtapa.ExigirIdentidadeCongelada(id, identificacao.Nome, identificacao.Carater, identificacao.TipoEtapa);

        EtapaProcesso etapa = new() { Id = id };
        etapa.DefinirIdentificacaoECarater(identificacao);
        etapa.DefinirLancamentoDaNota(avaliacao);
        etapa.SubstituirItens(ReconciliacaoDosItensDeAvaliacao.ReporCongelados(etapa._itensDeAvaliacao, itens ?? []));

        return etapa;
    }

    /// <summary>
    /// Atualiza os dados da MESMA etapa (mesmo <see cref="EntityBase.Id"/>) em vez de recriá-la, para
    /// que o desempate e a eliminação continuem apontando para ela. Recusa sem mutar nada quando a
    /// declaração viola alguma regra.
    /// </summary>
    public Result AtualizarDados(
        IdentificacaoDaEtapa identificacao,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens = null)
    {
        List<FieldError> erros = ConferenciaDaDeclaracaoDaEtapa.Violacoes(identificacao, avaliacao, itens);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        DefinirIdentificacaoECarater(identificacao);
        DefinirLancamentoDaNota(avaliacao);
        SubstituirItens(ReconciliacaoDosItensDeAvaliacao.Reconciliar(_itensDeAvaliacao, itens));

        return Result.Success();
    }

    private void DefinirIdentificacaoECarater(IdentificacaoDaEtapa identificacao)
    {
        IdentificacaoDaEtapa normalizada = identificacao.Normalizada();
        Nome = normalizada.Nome;
        Carater = normalizada.Carater;
        TipoEtapa = normalizada.TipoEtapa;
        Ordem = normalizada.Ordem;
        FaseCodigo = normalizada.FaseCodigo;
    }

    private void DefinirLancamentoDaNota(DeclaracaoDaAvaliacaoDaEtapa avaliacao)
    {
        Peso = avaliacao.Peso;
        NotaMinima = avaliacao.NotaMinima;
        NotaMaxima = avaliacao.NotaMaxima;
        PassoDoLancamento = avaliacao.PassoDoLancamento;
        FormaDeAvaliacao = avaliacao.FormaDeAvaliacao;
        RegraConsolidacao = avaliacao.RegraConsolidacao;
        EscalaDeAtendimento = NiveisDeAtendimento.EmOrdemCanonica(avaliacao.EscalaDeAtendimento);
    }

    private IdentificacaoDaEtapa IdentificacaoAtual() => new(Nome, Carater, TipoEtapa, Ordem, FaseCodigo);

    private DeclaracaoDaAvaliacaoDaEtapa AvaliacaoAtual() =>
        new(Peso, NotaMinima, NotaMaxima, PassoDoLancamento, FormaDeAvaliacao, RegraConsolidacao, EscalaDeAtendimento);

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>Declara a janela própria da etapa, normalizada para UTC, e o parecer individual.</summary>
    public Result DefinirJanelaEParecer(DateTimeOffset? inicio, DateTimeOffset? fim, bool emiteParecerIndividual)
    {
        if (ConferenciaDaDeclaracaoDaEtapa.JanelaInvertida(new JanelaDeTempo(inicio, fim)) is { } invertida)
        {
            return Result.Failure(invertida);
        }

        AplicarJanelaEParecer(inicio, fim, emiteParecerIndividual);

        return Result.Success();
    }

    /// <summary>Declara por inteiro os tipos de decisão da etapa, acumulando toda recusa; recusada, a etapa fica como estava.</summary>
    public Result DefinirTiposDeDecisao(IReadOnlyList<string?>? bruto)
    {
        IReadOnlyList<FieldError> erros = TiposDeDecisaoDeclarados.Validar(bruto, TiposDeDecisaoDeclarados.Campo);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        FilhasDoAgregado.Substituir(_tiposDeDecisao, TiposDeDecisaoDeclarados.Canonicos(bruto));

        return Result.Success();
    }

    private void AplicarJanelaEParecer(DateTimeOffset? inicio, DateTimeOffset? fim, bool emiteParecerIndividual)
    {
        Inicio = JanelaDeTempo.EmUtc(inicio);
        Fim = JanelaDeTempo.EmUtc(fim);
        EmiteParecerIndividual = emiteParecerIndividual;
    }

    /// <summary>
    /// Substitui os produtos da etapa por inteiro, recusando o mesmo par ato e papel duas vezes: dois
    /// registros idênticos tornariam ambíguo o que o recurso ancora. A chave é o par, e não o ato, porque
    /// a mesma matéria é publicada como preliminar e como definitiva (a fase usa a mesma chave).
    /// </summary>
    /// <remarks>
    /// O ato é comparado em NFC, como o documento canônico o emite. A reconciliação pelo par
    /// (<see cref="ProdutosPublicados"/>) preserva a linha do que não mudou: recriá-la giraria o Id, que
    /// entra nos bytes canônicos, e o hash da publicação mudaria a cada gravação idêntica.
    /// </remarks>
    public Result DefinirProdutos(IReadOnlyList<ProdutoDaEtapa> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        if (ConferenciaDaDeclaracaoDaEtapa.AtoRepetido(produtos) is { } repetido)
        {
            return Result.Failure(repetido);
        }

        SubstituirProdutos(ProdutosPublicados
            .ReconciliarPreservandoIdentidade(_produtos, produtos, HashCanonicalComputer.NormalizeNfc).Produtos);

        return Result.Success();
    }

    /// <summary>Substitui as bancas da etapa por inteiro, recusando o mesmo tipo duas vezes.</summary>
    public Result DefinirBancas(IReadOnlyList<BancaDaEtapa> bancas)
    {
        ArgumentNullException.ThrowIfNull(bancas);

        if (AlinhamentoDasBancasDaEtapa.BancaRepetida(bancas) is { } repetida)
        {
            return Result.Failure(repetida);
        }

        SubstituirBancas(bancas);

        return Result.Success();
    }

    /// <summary>
    /// A etapa como está agora, lida como se acabasse de ser declarada: a âncora de uma janela recursal pode
    /// ter ficado sem o que a sustentava, e a hidratação do EF materializa estado violado. O conceito não é
    /// parte da entidade e fica ausente.
    /// </summary>
    public EtapaDeclarada Declaracao() => EtapaDeclarada.Declarar(
        Id,
        IdentificacaoAtual(),
        new AvaliacaoDeclaradaDaEtapa(
            AvaliacaoAtual(),
            [.. ItensDeAvaliacao.Select(static item => new ItemDeAvaliacaoDeclarado(item.Id, item.Rotulo, item.PontuacaoMaxima))],
            null),
        new JanelaDeTempo(Inicio, Fim),
        new PublicacoesDaEtapa([.. _produtos], EmiteParecerIndividual, [.. _recursos], [.. _tiposDeDecisao]),
        [.. _bancas]);

    /// <summary>O que da etapa entra no resultado preliminar que ela publica (ver <see cref="ValueObjects.EntradaDoPreliminar"/>).</summary>
    public EntradaDoPreliminar EntradaDoPreliminar() => EntradaDoPreliminarDaEtapa.De(this);

    /// <summary>Os tipos de ato cujo preliminar a etapa publica sem o definitivo do mesmo tipo.</summary>
    internal IEnumerable<string> TiposComCicloAberto() =>
        CicloRecursal.TiposComCicloAberto(_produtos.Select(static p => (p.AtoCodigo, p.Papel)));

    /// <summary>
    /// Substitui as janelas recursais por inteiro. Prova o que só a etapa enxerga: a âncora em ato
    /// referencia um produto preliminar desta etapa, a âncora em ciência exige a promessa de parecer
    /// individual, e não há duas janelas para o mesmo fato.
    /// </summary>
    public Result DefinirRecursos(IReadOnlyList<RecursoDaEtapa> recursos)
    {
        ArgumentNullException.ThrowIfNull(recursos);

        DomainError? recusa = JanelasRecursaisDaEtapa.Recusa(_produtos, DeclaraNotaDoEnem, EmiteParecerIndividual, recursos);
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        SubstituirRecursos(recursos);

        return Result.Success();
    }

    /// <summary>
    /// Repõe numa etapa viva os dados da sua versão congelada, preservando o <see cref="EntityBase.Id"/>
    /// e o <c>CreatedAt</c> da instância rastreada. Só a reconciliação da restauração
    /// (<c>ProcessoSeletivo.AplicarGrafo</c>) chama.
    /// </summary>
    /// <remarks>
    /// Não passa pela conferência da declaração: repor não é declarar. Os dados vêm de um envelope com
    /// peso jurídico, e uma regra de forma criada depois não pode tornar a reposição impossível, o que
    /// trancaria o certame num estado do qual o descarte da retificação nunca sairia.
    /// </remarks>
    internal void ReporDe(EtapaProcesso congelada)
    {
        ArgumentNullException.ThrowIfNull(congelada);

        DefinirIdentificacaoECarater(congelada.IdentificacaoAtual());
        DefinirLancamentoDaNota(congelada.AvaliacaoAtual());
        AplicarJanelaEParecer(congelada.Inicio, congelada.Fim, congelada.EmiteParecerIndividual);
        FilhasDoAgregado.Substituir(_tiposDeDecisao, congelada._tiposDeDecisao);
        SubstituirItens(ReconciliacaoDosItensDeAvaliacao.ReporCongelados(_itensDeAvaliacao, congelada.ItensDeAvaliacao));
        IReadOnlyDictionary<Guid, Guid> produtoVivoPorCongelado = ReporProdutosDe(congelada);
        SubstituirBancas(congelada.Bancas);
        SubstituirRecursos(JanelasRecursaisDaEtapa.Repor(_recursos, congelada.Recursos, produtoVivoPorCongelado));
    }

    /// <summary>
    /// Produtos em duas passadas, como na fase: a linha que só trocou de papel mantém o Id, senão voltaria
    /// como inserção disputando o slot do índice único (etapa, ato, papel) com o DELETE da órfã. O ato é
    /// comparado em NFC: o envelope o congela assim e a linha viva guarda o que o cliente escreveu.
    /// </summary>
    /// <returns>O Id da linha que ficou, por Id do produto congelado.</returns>
    private IReadOnlyDictionary<Guid, Guid> ReporProdutosDe(EtapaProcesso congelada)
    {
        ProdutosReconciliados<ProdutoDaEtapa> reconciliados = ProdutosPublicados
            .ReconciliarPreservandoIdentidadeMesmoComTrocaDePapel(
                _produtos, congelada.Produtos, HashCanonicalComputer.NormalizeNfc);

        SubstituirProdutos(reconciliados.Produtos);

        return reconciliados.IdentidadeVivaPorIdentidadeChegada;
    }

    private void SubstituirProdutos(IEnumerable<ProdutoDaEtapa> produtos) =>
        FilhasDoAgregado.Substituir(_produtos, produtos, produto => produto.VincularEtapa(Id));

    private void SubstituirBancas(IEnumerable<BancaDaEtapa> declaradas) =>
        FilhasDoAgregado.Substituir(_bancas, AlinhamentoDasBancasDaEtapa.Reconciliar(_bancas, declaradas), banca => banca.VincularEtapa(Id));

    private void SubstituirRecursos(IEnumerable<RecursoDaEtapa> recursos) =>
        FilhasDoAgregado.Substituir(_recursos, recursos, recurso => recurso.VincularEtapa(Id));

    private void SubstituirItens(IEnumerable<ItemDeAvaliacao> itens) =>
        FilhasDoAgregado.Substituir(_itensDeAvaliacao, itens, item => item.VincularEtapa(Id));
}
