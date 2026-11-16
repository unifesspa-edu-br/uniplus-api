namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using ProcessosSeletivos.Bancas;
using ProcessosSeletivos.Etapas;

using Services;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Etapa <em>pontuada</em> do <see cref="ProcessoSeletivo"/> (peso, caráter, nota
/// mínima e escala do lançamento) — distinta de fase do cronograma, que é o eixo temporal do
/// certame. Entidade interna do agregado: criada, substituída e persistida
/// exclusivamente pela raiz.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete) de propósito:
/// a configuração em rascunho é substituível por inteiro (comandos
/// <c>Definir*</c>) e a trilha auditável do que valeu em cada publicação é o
/// snapshot de congelamento dos parâmetros na publicação (UNI-REQ-0019) da Story #759 — não faz sentido acumular linhas logicamente
/// excluídas de rascunho. A <c>Etapa</c> ligada a <c>Edital</c> continua
/// existindo para a conformidade da Story #460 e é aposentada junto com o
/// CRUD de Edital na #759.
/// </remarks>
public sealed class EtapaProcesso : EntityBase, IParteDoProcesso
{
    /// <summary>Alinhado a <c>EtapaProcessoConfiguration</c> (varchar(300)).</summary>
    public const int NomeMaxLength = 300;

    /// <summary>
    /// Caráter declarado que o tipo de etapa não admite — ver
    /// <see cref="CaraterDaEtapa.ValidarAdmitido"/>.
    /// </summary>
    public const string CaraterNaoAdmitidoPeloTipo = "EtapaProcesso.CaraterNaoAdmitidoPeloTipo";

    public const string NotaMaximaInvalida = "EtapaProcesso.NotaMaximaInvalida";
    public const string PassoDoLancamentoInvalido = "EtapaProcesso.PassoDoLancamentoInvalido";
    public const string NotaMinimaAcimaDaNotaMaxima = "EtapaProcesso.NotaMinimaAcimaDaNotaMaxima";
    public const string NotaMaximaForaDoPasso = "EtapaProcesso.NotaMaximaForaDoPasso";
    public const string AvaliacaoNaEtapaDoEnem = "EtapaProcesso.AvaliacaoNaEtapaDoEnem";
    public const string RegraDeConsolidacaoForaDaFormaHumana = "EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana";
    public const string RegraDeConsolidacaoInvalida = "EtapaProcesso.RegraDeConsolidacaoInvalida";
    public const string PassoNaFormaObjetiva = "EtapaProcesso.PassoNaFormaObjetiva";
    public const string FormaDeAvaliacaoDesconhecida = "EtapaProcesso.FormaDeAvaliacaoDesconhecida";
    public const string ItensNaEtapaDoEnem = "EtapaProcesso.ItensNaEtapaDoEnem";
    public const string EscalaSemItens = "EtapaProcesso.EscalaSemItens";
    public const string EscalaNaFormaObjetiva = "EtapaProcesso.EscalaNaFormaObjetiva";
    public const string PassoComEscalaDeAtendimento = "EtapaProcesso.PassoComEscalaDeAtendimento";

    public Guid ProcessoSeletivoId { get; private set; }


    /// <summary>
    /// Código canônico da fase declarada pelo cliente (ex.: <c>"AVALIACAO"</c>), e a única
    /// forma de a etapa dizer onde acontece. É por código, e não por id, porque o id da fase
    /// não sobrevive à reconciliação da restauração — mesmo motivo de
    /// <c>FaseCronograma.FaseConcluinteCodigo</c>. A raiz resolve o código contra o cronograma
    /// sempre que precisa da fase; nada guarda o resultado dessa resolução.
    /// </summary>
    public string? FaseCodigo { get; private set; }

    public string Nome { get; private set; } = string.Empty;
    public CaraterEtapa Carater { get; private set; }

    /// <summary>
    /// Cópia por valor do tipo de etapa resolvido em Configuração (ADR-0061). Fonte
    /// estável de identidade para avaliação de conformidade legal — o rótulo editorial
    /// (<see cref="Nome"/>) pode divergir do nome do tipo e não participa da avaliação.
    /// </summary>
    public TipoEtapaSnapshot TipoEtapa { get; private set; } = null!;

    public Guid TipoEtapaOrigemId => TipoEtapa.OrigemId;
    public decimal? Peso { get; private set; }
    public decimal? NotaMinima { get; private set; }

    /// <summary>
    /// Teto da escala em que a nota da etapa é lançada. Sem ele a nota lançada não tem como ser
    /// recusada por estar fora da escala, e a nota mínima de uma regra não tem contra o que ser
    /// conferida. Ausente na etapa sem nota e na de nota do ENEM, cuja escala vem do INEP.
    /// </summary>
    public decimal? NotaMaxima { get; private set; }

    /// <summary>De quanto em quanto a nota pode ser lançada; vale só para a nota digitada.</summary>
    public decimal? PassoDoLancamento { get; private set; }

    /// <summary>
    /// Como a nota da etapa é obtida. Declarada pela etapa, e não pelo tipo de etapa: o edital
    /// decide, e o tipo não restringe a forma. Ausente em rascunho, na etapa sem nota e na de nota
    /// do ENEM.
    /// </summary>
    public FormaDeAvaliacao? FormaDeAvaliacao { get; private set; }

    /// <summary>
    /// Como as notas dos avaliadores viram a nota da etapa; só existe na forma humana. É referência
    /// ao catálogo (código, versão e hash), congelada no snapshot como as regras da classificação.
    /// </summary>
    public ReferenciaRegra? RegraConsolidacao { get; private set; }

    /// <summary>
    /// Os níveis entre os quais a banca escolhe em cada item, em vez de digitar a nota; ausente
    /// quando a etapa lança nota digitada. Do nível de maior fração ao de menor.
    /// </summary>
    public IReadOnlyList<NivelDeAtendimento>? EscalaDeAtendimento { get; private set; }

    public int? Ordem { get; private set; }

    /// <summary>Início da janela própria da etapa, em UTC; ausente quando ela herda a da fase.</summary>
    public DateTimeOffset? Inicio { get; private set; }

    /// <summary>Fim da janela própria, em UTC.</summary>
    public DateTimeOffset? Fim { get; private set; }

    /// <summary>
    /// Se a etapa promete parecer individual por candidato. É a promessa de que existirá; o
    /// parecer sai na execução, e é dele que o candidato tira fundamento para recorrer.
    /// </summary>
    public bool EmiteParecerIndividual { get; private set; }

    private readonly List<ProdutoDaEtapa> _produtos = [];
    private readonly List<BancaDaEtapa> _bancas = [];
    private readonly List<RecursoDaEtapa> _recursos = [];
    private readonly List<ItemDeAvaliacao> _itensDeAvaliacao = [];

    /// <summary>
    /// Os itens de avaliação (critérios ou blocos) em que a nota da etapa se divide (0..*), em
    /// ordem canônica: pelo conteúdo, nunca pelo id.
    /// </summary>
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
    /// Uma etapa compõe a nota final quando o seu caráter pontua
    /// (classificatória ou ambas) e ela declara peso — é o critério que a
    /// inclui no divisor da média (<see cref="ProcessoSeletivo.CalcularDivisorMedia"/>).
    /// </summary>
    public bool ComponeNota => CaraterDaEtapa.Pontua(Carater) && Peso.HasValue;

    /// <summary>
    /// A etapa declara que sua nota vem do ENEM — é o atributo do tipo congelado que diz
    /// isso, e é essa declaração que o motor de classificação consome.
    /// </summary>
    public bool DeclaraNotaDoEnem => TipoEtapa.NotaDeOrigemNoEnem;

    private EtapaProcesso() { }

    /// <summary>
    /// Cria a etapa acumulando toda violação independente em vez de retornar na primeira (ADR-0125), com
    /// as mesmas recusas e a mesma ordem de <see cref="AtualizarDados"/>. A identidade da etapa nova é do
    /// sistema, não do cliente, e os itens declarados entram todos como itens novos. Precisão/escala de
    /// <see cref="Peso"/>, <see cref="NotaMinima"/>, <see cref="NotaMaxima"/> e
    /// <see cref="PassoDoLancamento"/> (<c>numeric(18,4)</c>) permanecem só no validator: não há ainda um
    /// helper de domínio para limite decimal, então checar aqui duplicaria uma regra de forma de
    /// wire/coluna sem trazer nenhum ganho de correção.
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
    /// Nome, caráter, ordem, peso, nota mínima e a escala (nota máxima e passo) não dependem de nenhum
    /// cadastro cross-módulo — diferente de <see cref="TipoEtapa"/> (resolvido à parte, contra o cadastro
    /// vivo de Configuração, por quem chama). Compartilhada entre <see cref="Criar"/> e
    /// <see cref="AtualizarDados"/> (mesmas checagens) e exposta para o handler poder confirmar a forma de
    /// TODAS as etapas do payload numa primeira passada, antes de resolver o tipo de etapa no cadastro
    /// (mesmo padrão de <c>FormaDoItem.ValidarFormaBasica</c>, PR #1214).
    /// </summary>
    /// <remarks>
    /// O nome, o caráter e a ordem chegam soltos porque o handler confere a forma antes de ter o tipo, e
    /// <see cref="IdentificacaoDaEtapa"/> só existe depois dele. A forma de avaliação e a regra de
    /// consolidação de <paramref name="avaliacao"/> não são conferidas aqui: a coerência entre elas é de
    /// <see cref="CoerenciaDaAvaliacaoDaEtapa"/>.
    /// </remarks>
    public static List<FieldError> ValidarFormaBasica(
        string? nome,
        CaraterEtapa carater,
        int? ordem,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        IReadOnlyList<ItemDeAvaliacaoDeclarado?>? itens)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);

        List<FieldError> erros = ValidarNome(nome);
        erros.AddRange(CaraterDaEtapa.ValidarDeclarado(carater, avaliacao.Peso, avaliacao.NotaMinima));
        decimal? notaMinimaAceita = erros.Exists(static e => e.Field == "notaMinima") ? null : avaliacao.NotaMinima;
        erros.AddRange(EscalaDoLancamento.Validar(notaMinimaAceita, avaliacao.NotaMaxima, avaliacao.PassoDoLancamento));
        erros.AddRange(ValidarOrdem(ordem));
        erros.AddRange(ItemDeAvaliacao.Validar(itens, avaliacao.PassoDoLancamento));
        erros.AddRange(NiveisDeAtendimento.Validar(avaliacao.EscalaDeAtendimento));

        return erros;
    }

    private static List<FieldError> ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return [new("nome", new DomainError("EtapaProcesso.NomeObrigatorio", "O nome da etapa é obrigatório."))];
        }

        return nome.Trim().Length > NomeMaxLength
            ? [new("nome", new DomainError(
                "EtapaProcesso.NomeTamanho", $"O nome da etapa deve ter no máximo {NomeMaxLength} caracteres."))]
            : [];
    }

    private static List<FieldError> ValidarOrdem(int? ordem) =>
        ordem is <= 0
            ? [new("ordem", new DomainError(
                "EtapaProcesso.OrdemInvalida", "A ordem da etapa, quando informada, deve ser maior que zero."))]
            : [];

    /// <summary>
    /// Reidrata uma etapa a partir de uma <see cref="VersaoConfiguracao"/> congelada,
    /// <b>preservando o <see cref="EntityBase.Id"/></b> — que <see cref="Criar"/> não
    /// aceita, por decisão (a identidade de uma etapa nova é do sistema, não do cliente).
    /// </summary>
    /// <remarks>
    /// <para>
    /// O <c>id</c> da etapa é congelado no envelope (ADR-0110 D2), porque
    /// <c>criteriosDesempate.args.etapaRef</c> e <c>regrasEliminacao.args.etapaRef</c>
    /// apontam para ele: sem preservá-lo, o snapshot reidratado teria referências que
    /// não resolvem, e o desempate e a eliminação do certame ficariam inexecutáveis. O
    /// mesmo vale para o id de cada item de avaliação (ADR-0150), a que o lançamento e o
    /// recurso apontam, e que chega por <paramref name="itens"/>. Os ids das
    /// demais filhas são regenerados — nenhuma referência de negócio exige estabilidade
    /// deles, e as FKs internas são reconstruídas junto com o grafo.
    /// </para>
    /// <para>
    /// Reidratar não é criar: os dados vêm de um documento com peso jurídico, já
    /// validado quando foi congelado. As guardas aqui são a última linha contra erro de
    /// programação — o decoder do envelope é quem recusa bytes inválidos, e o faz com
    /// <c>Result</c>, nunca com exceção.
    /// </para>
    /// </remarks>
    public static EtapaProcesso Reidratar(
        Guid id,
        IdentificacaoDaEtapa identificacao,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        IReadOnlyList<ItemDeAvaliacao>? itens)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ExigirIdentidadeCongelada(id, identificacao.Nome, identificacao.Carater, identificacao.TipoEtapa);

        EtapaProcesso etapa = new() { Id = id };
        etapa.DefinirIdentificacaoECarater(identificacao);
        etapa.DefinirLancamentoDaNota(avaliacao);
        etapa.ReporItensCongelados(itens ?? []);

        return etapa;
    }

    private static void ExigirTipoResolvido(TipoEtapaSnapshot tipoEtapa) => ArgumentNullException.ThrowIfNull(tipoEtapa);

    private static void ExigirIdentidadeCongelada(Guid id, string nome, CaraterEtapa carater, TipoEtapaSnapshot tipoEtapa)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ExigirTipoResolvido(tipoEtapa);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A etapa reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).", nameof(carater));
        }
    }

    /// <summary>
    /// Atualiza os dados da MESMA etapa (mesmo <see cref="EntityBase.Id"/>) em
    /// vez de recriá-la — permite que <c>DefinirEtapasCommandHandler</c>
    /// reconcilie o payload de <c>PUT /etapas</c> com o agregado tracked
    /// preservando a identidade de uma etapa já referenciada por critério de
    /// desempate ou regra de eliminação da classificação (sem isso, qualquer
    /// reconfiguração de etapas quebraria essas referências por construção).
    /// Recusa sem mutar nada quando a declaração viola alguma regra.
    /// </summary>
    public Result AtualizarDados(
        IdentificacaoDaEtapa identificacao,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens = null)
    {
        List<FieldError> erros = ViolacoesDaDeclaracao(identificacao, avaliacao, itens);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        DefinirIdentificacaoECarater(identificacao);
        DefinirLancamentoDaNota(avaliacao);
        ReconciliarItens(itens);

        return Result.Success();
    }

    private static List<FieldError> ViolacoesDaDeclaracao(
        IdentificacaoDaEtapa identificacao, DeclaracaoDaAvaliacaoDaEtapa avaliacao, IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ExigirTipoResolvido(identificacao.TipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(identificacao.Nome, identificacao.Carater, identificacao.Ordem, avaliacao, itens);
        erros.AddRange(CoerenciaDaAvaliacaoDaEtapa.Validar(
            identificacao.TipoEtapa.NotaDeOrigemNoEnem, avaliacao.PassoDoLancamento, avaliacao.FormaDeAvaliacao,
            avaliacao.RegraConsolidacao?.Codigo, itens?.Count > 0, avaliacao.EscalaDeAtendimento?.Count > 0));

        return erros;
    }

    private void DefinirIdentificacaoECarater(IdentificacaoDaEtapa identificacao)
    {
        Nome = identificacao.Nome.Trim();
        Carater = identificacao.Carater;
        TipoEtapa = identificacao.TipoEtapa;
        Ordem = identificacao.Ordem;
        FaseCodigo = string.IsNullOrWhiteSpace(identificacao.FaseCodigo) ? null : identificacao.FaseCodigo.Trim();
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

    /// <summary>
    /// Substitui os itens reconciliando pelo id: o item declarado com o id de um existente é a
    /// mesma linha com rótulo e pontuação novos, e é isso que mantém o id estável para quem aponta
    /// para ele. Sem id, ou com um que a etapa não conhece, é item novo.
    /// </summary>
    private void ReconciliarItens(IReadOnlyList<ItemDeAvaliacaoDeclarado>? declarados)
    {
        List<ItemDeAvaliacao> vivos = [.. _itensDeAvaliacao];

        SubstituirItens((declarados ?? []).Select(declarado =>
            ReaproveitarItemPeloId(vivos, declarado.Id, declarado.Rotulo, declarado.PontuacaoMaxima)
                ?? ItemDeAvaliacao.Criar(declarado)));
    }

    /// <summary>
    /// Repõe os itens congelados preservando o id: o item vivo de mesmo id é reaproveitado, e o
    /// congelado que a linha viva não tem entra com o id do envelope.
    /// </summary>
    private void ReporItensCongelados(IReadOnlyList<ItemDeAvaliacao> congelados)
    {
        List<ItemDeAvaliacao> vivos = [.. _itensDeAvaliacao];

        SubstituirItens(congelados.Select(congelado =>
            ReaproveitarItemPeloId(vivos, congelado.Id, congelado.Rotulo, congelado.PontuacaoMaxima) ?? congelado));
    }

    private static ItemDeAvaliacao? ReaproveitarItemPeloId(
        List<ItemDeAvaliacao> vivos, Guid? id, string rotulo, decimal pontuacaoMaxima)
    {
        ItemDeAvaliacao? existente = id is { } procurado ? vivos.Find(i => i.Id == procurado) : null;
        if (existente is null)
        {
            return null;
        }

        vivos.Remove(existente);
        existente.Atualizar(rotulo, pontuacaoMaxima);

        return existente;
    }

    /// <summary>Declara a janela própria da etapa, normalizada para UTC, e o parecer individual.</summary>
    public Result DefinirJanelaEParecer(DateTimeOffset? inicio, DateTimeOffset? fim, bool emiteParecerIndividual)
    {
        if (inicio is not null && fim is not null && fim < inicio)
        {
            return Result.Failure(new DomainError(
                "EtapaProcesso.JanelaInvertida",
                "O fim da janela da etapa não pode ser anterior ao início."));
        }

        AplicarJanelaEParecer(inicio, fim, emiteParecerIndividual);

        return Result.Success();
    }

    private void AplicarJanelaEParecer(DateTimeOffset? inicio, DateTimeOffset? fim, bool emiteParecerIndividual)
    {
        Inicio = inicio?.ToUniversalTime();
        Fim = fim?.ToUniversalTime();
        EmiteParecerIndividual = emiteParecerIndividual;
    }

    /// <summary>
    /// Substitui os produtos da etapa por inteiro, recusando o mesmo par ato e papel
    /// declarado duas vezes — dois registros idênticos na mesma etapa tornariam ambíguo o
    /// que o recurso ancora.
    ///
    /// A chave é o par, e não o ato sozinho: o catálogo nomeia a matéria, e a mesma matéria
    /// é publicada uma vez como preliminar e outra como definitiva. Ver
    /// <see cref="FaseCronograma"/>, onde a mesma chave governa os produtos da fase.
    /// </summary>
    /// <remarks>
    /// O ato é comparado em NFC, como o documento canônico o emite: sem normalizar, o mesmo
    /// código escrito uma vez com acento composto e outra com acento decomposto atravessaria a
    /// guarda e produziria duas linhas com <c>atoCodigo</c> e papel idênticos no documento. A
    /// reconciliação pelo par (<see cref="ProdutosPublicados"/>) preserva a linha do que não mudou
    /// em vez de trocar a coleção: o cliente devolve o que leu, e recriar a linha giraria o Id, que
    /// entra nos bytes canônicos — o hash da publicação mudaria a cada gravação idêntica.
    /// </remarks>
    public Result DefinirProdutos(IReadOnlyList<ProdutoDaEtapa> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        if (ProdutosPublicados.PrimeiroParRepetido(produtos, HashCanonicalComputer.NormalizeNfc) is { } duplicado)
        {
            return AtoDuplicadoNaEtapa(duplicado);
        }

        SubstituirProdutos(ProdutosPublicados
            .ReconciliarPreservandoIdentidade(_produtos, produtos, HashCanonicalComputer.NormalizeNfc).Produtos);

        return Result.Success();
    }

    private static Result AtoDuplicadoNaEtapa(ProdutoDaEtapa duplicado) =>
        Result.Failure(new DomainError(
            "EtapaProcesso.AtoDuplicadoNaEtapa",
            $"A etapa declara o ato '{duplicado.AtoCodigo}' mais de uma vez no mesmo papel — o par ato e papel é declarado uma única vez por etapa."));

    /// <summary>Substitui as bancas da etapa por inteiro, recusando o mesmo tipo duas vezes.</summary>
    public Result DefinirBancas(IReadOnlyList<BancaDaEtapa> bancas)
    {
        ArgumentNullException.ThrowIfNull(bancas);

        if (AlinhamentoDasBancasDaEtapa.CodigoRepetido(bancas))
        {
            return Result.Failure(new DomainError(
                "EtapaProcesso.BancaDuplicadaNaEtapa",
                "Cada tipo de banca pode ser requerido uma única vez por etapa."));
        }

        SubstituirBancas(bancas);

        return Result.Success();
    }

    /// <summary>Os tipos de ato cujo preliminar a etapa publica sem o definitivo do mesmo tipo.</summary>
    internal IEnumerable<string> TiposComCicloAberto() =>
        CicloRecursal.TiposComCicloAberto(_produtos.Select(static p => (p.AtoCodigo, p.Papel)));

    /// <summary>
    /// Substitui as janelas recursais por inteiro. Prova o que só a etapa enxerga: a âncora
    /// em ato referencia um produto preliminar desta etapa; a âncora em ciência exige a
    /// promessa de parecer individual, porque é dele que corre o prazo; e não há duas
    /// janelas para o mesmo fato.
    /// </summary>
    public Result DefinirRecursos(IReadOnlyList<RecursoDaEtapa> recursos)
    {
        ArgumentNullException.ThrowIfNull(recursos);

        DomainError? recusa = JanelasRecursaisDaEtapa.ViolacaoDaAncora(this, recursos) ?? JanelasRecursaisDaEtapa.FatoRepetido(recursos);
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        SubstituirRecursos(recursos);

        return Result.Success();
    }

    /// <summary>
    /// Repõe numa etapa viva os dados da sua versão congelada, preservando o
    /// <see cref="EntityBase.Id"/> e o <c>CreatedAt</c> da instância rastreada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Não passa por <see cref="ValidarFormaBasica"/>, e é a diferença que importa em relação a
    /// <see cref="AtualizarDados"/>: repor não é declarar. Os dados vêm de um envelope com peso
    /// jurídico, válidos quando foram congelados, e uma regra de forma criada depois não pode
    /// tornar a reposição impossível — isso trancaria o certame num estado do qual o descarte da
    /// retificação nunca sairia. É a mesma postura de <see cref="Reidratar"/>, que decodifica o
    /// envelope sem revalidar o que ele já provou.
    /// </para>
    /// <para>
    /// Chamada apenas pela reconciliação da restauração, em <c>ProcessoSeletivo.AplicarGrafo</c>.
    /// Lê tudo da etapa <paramref name="congelada"/> decodificada do envelope.
    /// </para>
    /// </remarks>
    internal void ReporDe(EtapaProcesso congelada)
    {
        ArgumentNullException.ThrowIfNull(congelada);

        ReporDadosDe(congelada);
        ReporFilhasDe(congelada);
    }

    private void ReporDadosDe(EtapaProcesso congelada)
    {
        DefinirIdentificacaoECarater(congelada.IdentificacaoAtual());
        DefinirLancamentoDaNota(congelada.AvaliacaoAtual());
        AplicarJanelaEParecer(congelada.Inicio, congelada.Fim, congelada.EmiteParecerIndividual);
    }

    private void ReporFilhasDe(EtapaProcesso congelada)
    {
        ReporItensCongelados(congelada.ItensDeAvaliacao);
        IReadOnlyDictionary<Guid, Guid> produtoVivoPorCongelado = ReporProdutosDe(congelada);
        SubstituirBancas(congelada.Bancas);
        SubstituirRecursos(JanelasRecursaisDaEtapa.Repor(_recursos, congelada.Recursos, produtoVivoPorCongelado));
    }

    /// <summary>
    /// Produtos em duas passadas, como na fase: a linha que só trocou de papel mantém o Id, senão
    /// sairia como órfã e voltaria como inserção, com DELETE e INSERT disputando o mesmo slot do
    /// índice único (etapa, ato, papel) dentro da mesma transação. O ato é comparado em NFC: o
    /// envelope congela o ato em NFC e a linha viva guarda o que o cliente escreveu.
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
        SubstituirFilhas(_produtos, produtos, produto => produto.VincularEtapa(Id));

    private void SubstituirBancas(IEnumerable<BancaDaEtapa> declaradas) =>
        SubstituirFilhas(_bancas, AlinhamentoDasBancasDaEtapa.Reconciliar(_bancas, declaradas), banca => banca.VincularEtapa(Id));

    private void SubstituirRecursos(IEnumerable<RecursoDaEtapa> recursos) =>
        SubstituirFilhas(_recursos, recursos, recurso => recurso.VincularEtapa(Id));

    private void SubstituirItens(IEnumerable<ItemDeAvaliacao> itens) =>
        SubstituirFilhas(_itensDeAvaliacao, itens, item => item.VincularEtapa(Id));

    // Materializa antes de limpar: as novas podem ser derivadas da própria coleção que se esvazia.
    private static void SubstituirFilhas<T>(List<T> atuais, IEnumerable<T> novas, Action<T> vincularEtapa)
    {
        List<T> substitutas = [.. novas];
        atuais.Clear();
        foreach (T filha in substitutas)
        {
            vincularEtapa(filha);
            atuais.Add(filha);
        }
    }
}
