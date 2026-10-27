namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

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
public sealed class EtapaProcesso : EntityBase
{
    /// <summary>Alinhado a <c>EtapaProcessoConfiguration</c> (varchar(300)).</summary>
    public const int NomeMaxLength = 300;

    /// <summary>
    /// Caráter declarado que o tipo de etapa não admite — ver
    /// <see cref="ValidarCaraterAdmitido"/>.
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

    private EtapaProcesso() { }

    /// <summary>
    /// Substitui os produtos da etapa por inteiro, recusando o mesmo par ato e papel
    /// declarado duas vezes — dois registros idênticos na mesma etapa tornariam ambíguo o
    /// que o recurso ancora.
    ///
    /// A chave é o par, e não o ato sozinho: o catálogo nomeia a matéria, e a mesma matéria
    /// é publicada uma vez como preliminar e outra como definitiva. Ver
    /// <see cref="FaseCronograma"/>, onde a mesma chave governa os produtos da fase.
    /// </summary>
    public Result DefinirProdutos(IReadOnlyList<ProdutoDaEtapa> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        // A chave da duplicidade é a mesma sob a qual a reconciliação abaixo e o documento
        // canônico comparam o ato: sem normalizar aqui, o mesmo código escrito uma vez com
        // acento composto e outra com acento decomposto atravessa a guarda e produz duas
        // linhas que o documento emite com atoCodigo e papel idênticos — duas âncoras
        // indistinguíveis para a janela recursal resolver.
        ProdutoDaEtapa? duplicado = produtos
            .GroupBy(static p => (Ato: HashCanonicalComputer.NormalizeNfc(p.AtoCodigo), p.Papel))
            .FirstOrDefault(static g => g.Count() > 1)?.First();
        if (duplicado is not null)
        {
            return Result.Failure(new DomainError(
                "EtapaProcesso.AtoDuplicadoNaEtapa",
                $"A etapa declara o ato '{duplicado.AtoCodigo}' mais de uma vez no mesmo papel — o par ato e papel é declarado uma única vez por etapa."));
        }

        // Reconcilia pelo par que identifica o produto, em vez de trocar a coleção inteira: o
        // cliente devolve o que leu, e recriar a linha do que não mudou gira o Id, que entra nos
        // bytes canônicos. O hash da publicação passaria a mudar a cada gravação idêntica, e com
        // ele a resposta para "esta configuração ainda é a que foi publicada?".
        //
        // É o mesmo tratamento que a reposição da versão congelada já dá alguns métodos abaixo,
        // e pela mesma razão que o cronograma reconcilia as fases: preservar a linha preserva o
        // CreatedAt e as referências que apontam para o Id — aqui, o produto âncora do recurso.
        List<ProdutoDaEtapa> disponiveis = [.. _produtos];
        List<ProdutoDaEtapa> reconciliados = [];
        foreach (ProdutoDaEtapa declarado in produtos)
        {
            // Compara sob a mesma normalização que o documento canônico usa: o cliente pode
            // devolver o acento em forma decomposta, e ordinalmente esse texto não é o mesmo.
            // Sem isto, um reenvio idêntico aos olhos do operador recriaria a linha.
            string atoDeclarado = HashCanonicalComputer.NormalizeNfc(declarado.AtoCodigo);
            ProdutoDaEtapa? existente = disponiveis.Find(p =>
                string.Equals(
                    HashCanonicalComputer.NormalizeNfc(p.AtoCodigo),
                    atoDeclarado,
                    StringComparison.Ordinal)
                && p.Papel == declarado.Papel);

            if (existente is null)
            {
                reconciliados.Add(declarado);
                continue;
            }

            disponiveis.Remove(existente);
            reconciliados.Add(existente);
        }

        _produtos.Clear();
        foreach (ProdutoDaEtapa produto in reconciliados)
        {
            produto.VincularEtapa(Id);
            _produtos.Add(produto);
        }

        return Result.Success();
    }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125). Precisão/
    /// escala de <see cref="Peso"/>, <see cref="NotaMinima"/>, <see cref="NotaMaxima"/> e
    /// <see cref="PassoDoLancamento"/> (<c>numeric(18,4)</c>) permanecem
    /// só no validator — não há ainda um helper de domínio para limite decimal, então checar
    /// aqui duplicaria uma regra de forma de wire/coluna sem trazer nenhum ganho de correção.
    /// </summary>
    public static Result<EtapaProcesso> Criar(
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso = null,
        decimal? notaMinima = null,
        int? ordem = null,
        string? faseCodigo = null,
        decimal? notaMaxima = null,
        decimal? passoDoLancamento = null,
        FormaDeAvaliacao? formaDeAvaliacao = null,
        ReferenciaRegra? regraConsolidacao = null,
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itensDeAvaliacao = null,
        IReadOnlyList<NivelDeAtendimento>? escalaDeAtendimento = null)
    {
        ArgumentNullException.ThrowIfNull(tipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(
            nome, carater, peso, notaMinima, ordem, notaMaxima, passoDoLancamento, itensDeAvaliacao, escalaDeAtendimento);
        erros.AddRange(ValidarAvaliacao(
            tipoEtapa.NotaDeOrigemNoEnem, passoDoLancamento, formaDeAvaliacao, regraConsolidacao?.Codigo,
            itensDeAvaliacao?.Count > 0, escalaDeAtendimento?.Count > 0));
        if (erros.Count > 0)
        {
            return Result<EtapaProcesso>.ValidationFailure(erros);
        }

        EtapaProcesso etapa = new()
        {
            Nome = nome.Trim(),
            Carater = carater,
            TipoEtapa = tipoEtapa,
            Peso = peso,
            NotaMinima = notaMinima,
            NotaMaxima = notaMaxima,
            PassoDoLancamento = passoDoLancamento,
            FormaDeAvaliacao = formaDeAvaliacao,
            RegraConsolidacao = regraConsolidacao,
            EscalaDeAtendimento = NiveisDeAtendimento.EmOrdemCanonica(escalaDeAtendimento),
            Ordem = ordem,
            FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim(),
        };
        etapa.ReconciliarItens(itensDeAvaliacao);

        return Result<EtapaProcesso>.Success(etapa);
    }

    /// <summary>
    /// Nome/Carater/Peso/NotaMinima/Ordem e a escala (nota máxima e passo) não dependem de nenhum cadastro cross-módulo —
    /// diferente de <see cref="TipoEtapa"/> (resolvido à parte, contra o cadastro vivo de
    /// Configuração, por quem chama). Compartilhada entre <see cref="Criar"/> e
    /// <see cref="AtualizarDados"/> (mesmas checagens) e exposta para o handler poder
    /// confirmar a forma de TODAS as etapas do payload numa primeira passada, antes de
    /// resolver o tipo de etapa no cadastro (mesmo padrão de
    /// <c>FormaDoItem.ValidarFormaBasica</c>, PR #1214).
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(
        string? nome,
        CaraterEtapa carater,
        decimal? peso,
        decimal? notaMinima,
        int? ordem,
        decimal? notaMaxima = null,
        decimal? passoDoLancamento = null,
        IReadOnlyList<ItemDeAvaliacaoDeclarado?>? itensDeAvaliacao = null,
        IReadOnlyList<NivelDeAtendimento?>? escalaDeAtendimento = null)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                "EtapaProcesso.NomeObrigatorio", "O nome da etapa é obrigatório.")));
        }
        else if (nome.Trim().Length > NomeMaxLength)
        {
            erros.Add(new("nome", new DomainError(
                "EtapaProcesso.NomeTamanho", $"O nome da etapa deve ter no máximo {NomeMaxLength} caracteres.")));
        }

        if (carater == CaraterEtapa.Nenhum || !Enum.IsDefined(carater))
        {
            erros.Add(new("carater", new DomainError(
                "EtapaProcesso.CaraterObrigatorio",
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).")));
        }

        if (peso is <= 0)
        {
            erros.Add(new("peso", new DomainError(
                "EtapaProcesso.PesoInvalido", "O peso da etapa, quando informado, deve ser maior que zero.")));
        }
        else if (peso.HasValue && carater is CaraterEtapa.Eliminatoria)
        {
            // Peso em etapa que não pontua é dado morto: CalcularDivisorMedia soma apenas as
            // etapas cujo caráter compõe a nota, então o valor fica gravado sem nunca pesar em
            // nada — e quem configurou acredita que pesa.
            erros.Add(new("peso", new DomainError(
                "EtapaProcesso.PesoSemCaraterQuePontua",
                "Peso só se aplica a etapa cujo caráter compõe a nota final; etapa apenas eliminatória aprova ou reprova sem pontuar.")));
        }

        if (notaMinima is < 0)
        {
            erros.Add(new("notaMinima", new DomainError(
                "EtapaProcesso.NotaMinimaInvalida", "A nota mínima, quando informada, não pode ser negativa.")));
        }
        else if (notaMinima.HasValue && carater is CaraterEtapa.Classificatoria)
        {
            erros.Add(new("notaMinima", new DomainError(
                "EtapaProcesso.NotaMinimaSemCaraterQueElimina",
                "Nota mínima só se aplica a etapa cujo caráter elimina; etapa apenas classificatória pontua sem reprovar.")));
        }

        ValidarEscala(
            erros, notaMinima, notaMaxima, passoDoLancamento,
            notaMinimaJaRecusada: erros.Exists(static e => e.Field == "notaMinima"));

        if (ordem is <= 0)
        {
            erros.Add(new("ordem", new DomainError(
                "EtapaProcesso.OrdemInvalida", "A ordem da etapa, quando informada, deve ser maior que zero.")));
        }

        erros.AddRange(ItemDeAvaliacao.Validar(itensDeAvaliacao, passoDoLancamento));
        erros.AddRange(NiveisDeAtendimento.Validar(escalaDeAtendimento));

        return erros;
    }

    /// <summary>
    /// Recusa a contradição entre valores declarados da escala, em rascunho também: a falta é
    /// tolerada (o gate de publicação a cobra), mas dois valores que não convivem não são falta.
    /// Quem já recusou o valor isolado não o repete nas regras que o comparam, o que também
    /// evita dividir por zero no múltiplo do passo.
    /// </summary>
    private static void ValidarEscala(
        List<FieldError> erros, decimal? notaMinima, decimal? notaMaxima, decimal? passo, bool notaMinimaJaRecusada)
    {
        if (notaMaxima is <= 0)
        {
            erros.Add(new("notaMaxima", new DomainError(
                NotaMaximaInvalida, "A nota máxima da etapa, quando informada, deve ser maior que zero.")));
        }

        if (passo is <= 0)
        {
            erros.Add(new("passoDoLancamento", new DomainError(
                PassoDoLancamentoInvalido, "O passo do lançamento, quando informado, deve ser maior que zero.")));
        }

        if (notaMaxima is not > 0)
        {
            return;
        }

        if (!notaMinimaJaRecusada && notaMinima > notaMaxima)
        {
            erros.Add(new("notaMinima", new DomainError(
                NotaMinimaAcimaDaNotaMaxima, "A nota mínima da etapa não pode passar da nota máxima.")));
        }

        if (passo > 0 && notaMaxima % passo != 0)
        {
            erros.Add(new("notaMaxima", new DomainError(
                NotaMaximaForaDoPasso, "A nota máxima da etapa deve ser múltipla do passo do lançamento.")));
        }
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
    /// </para>
    /// </remarks>
    internal void ReporDadosCongelados(
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso,
        decimal? notaMinima,
        int? ordem,
        string? faseCodigo,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        bool emiteParecerIndividual,
        IReadOnlyList<ProdutoDaEtapa> produtos,
        IReadOnlyList<BancaDaEtapa> bancas,
        IReadOnlyList<RecursoDaEtapa> recursos,
        decimal? notaMaxima,
        decimal? passoDoLancamento,
        FormaDeAvaliacao? formaDeAvaliacao,
        ReferenciaRegra? regraConsolidacao,
        IReadOnlyList<ItemDeAvaliacao> itensDeAvaliacao,
        IReadOnlyList<NivelDeAtendimento>? escalaDeAtendimento)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(itensDeAvaliacao);
        ArgumentNullException.ThrowIfNull(tipoEtapa);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancas);
        ArgumentNullException.ThrowIfNull(recursos);

        Nome = nome.Trim();
        Carater = carater;
        TipoEtapa = tipoEtapa;
        Peso = peso;
        NotaMinima = notaMinima;
        NotaMaxima = notaMaxima;
        PassoDoLancamento = passoDoLancamento;
        FormaDeAvaliacao = formaDeAvaliacao;
        RegraConsolidacao = regraConsolidacao;
        EscalaDeAtendimento = NiveisDeAtendimento.EmOrdemCanonica(escalaDeAtendimento);
        Ordem = ordem;
        FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim();
        Inicio = inicio?.ToUniversalTime();
        ReporItensCongelados(itensDeAvaliacao);
        Fim = fim?.ToUniversalTime();
        EmiteParecerIndividual = emiteParecerIndividual;

        // Produtos em duas passadas, como na fase: a primeira casa o par ato + papel — a linha
        // que não mudou — e a segunda casa pelo ato entre as que sobraram, preservando o Id de
        // quem só trocou de papel. Sem isso a linha sairia como órfã e voltaria como inserção,
        // com DELETE e INSERT disputando o mesmo slot do índice único (etapa, ato, papel)
        // dentro da mesma transação. Cada viva é consumida uma vez só, senão a preliminar e a
        // definitiva da mesma matéria casariam com a mesma linha.
        Dictionary<Guid, Guid> ancoraCongeladaParaViva = [];
        List<ProdutoDaEtapa> disponiveis = [.. _produtos];
        List<ProdutoDaEtapa> reconciliados = [];
        List<ProdutoDaEtapa> semPar = [];

        foreach (ProdutoDaEtapa congelado in produtos)
        {
            // Sob a mesma normalização da gravação: o envelope congela o ato em NFC, e a linha
            // viva guarda o que o cliente escreveu. Casar ordinalmente faria a linha existente
            // sair como órfã e a congelada entrar como inserção, com DELETE e INSERT disputando
            // o mesmo slot do índice único (etapa, ato, papel) dentro da mesma transação.
            string atoCongelado = HashCanonicalComputer.NormalizeNfc(congelado.AtoCodigo);
            ProdutoDaEtapa? mesmoParaOMesmoPapel = disponiveis.Find(
                p => string.Equals(
                        HashCanonicalComputer.NormalizeNfc(p.AtoCodigo), atoCongelado, StringComparison.Ordinal)
                    && p.Papel == congelado.Papel);
            if (mesmoParaOMesmoPapel is not null)
            {
                disponiveis.Remove(mesmoParaOMesmoPapel);
                reconciliados.Add(mesmoParaOMesmoPapel);
                ancoraCongeladaParaViva[congelado.Id] = mesmoParaOMesmoPapel.Id;
            }
            else
            {
                // Guarda o lugar na ordem de chegada; a segunda passada o preenche.
                reconciliados.Add(congelado);
                semPar.Add(congelado);
            }
        }

        foreach (ProdutoDaEtapa congelado in semPar)
        {
            string atoCongelado = HashCanonicalComputer.NormalizeNfc(congelado.AtoCodigo);
            ProdutoDaEtapa? mesmoAto = disponiveis.Find(
                p => string.Equals(
                    HashCanonicalComputer.NormalizeNfc(p.AtoCodigo), atoCongelado, StringComparison.Ordinal));
            if (mesmoAto is null)
            {
                continue;
            }

            disponiveis.Remove(mesmoAto);
            mesmoAto.AtualizarPapel(congelado.Papel);
            reconciliados[reconciliados.IndexOf(congelado)] = mesmoAto;
            ancoraCongeladaParaViva[congelado.Id] = mesmoAto.Id;
        }

        _produtos.Clear();
        foreach (ProdutoDaEtapa produto in reconciliados)
        {
            produto.VincularEtapa(Id);
            _produtos.Add(produto);
        }

        // Bancas pelo código, que é a chave do índice único da etapa — mesmo motivo do par
        // acima. O tipo de origem é reposto na linha viva em vez de recriá-la.
        List<BancaDaEtapa> bancasVivas = [.. _bancas];
        List<BancaDaEtapa> bancasFinais = [];
        foreach (BancaDaEtapa congelada in bancas)
        {
            string codigoCongelado = HashCanonicalComputer.NormalizeNfc(congelada.Codigo);
            BancaDaEtapa? mesmoCodigo = bancasVivas.Find(
                b => string.Equals(
                    HashCanonicalComputer.NormalizeNfc(b.Codigo), codigoCongelado, StringComparison.Ordinal));
            if (mesmoCodigo is null)
            {
                bancasFinais.Add(congelada);
                continue;
            }

            bancasVivas.Remove(mesmoCodigo);
            mesmoCodigo.ReporOrigem(congelada.TipoBancaOrigemId);
            bancasFinais.Add(mesmoCodigo);
        }

        _bancas.Clear();
        foreach (BancaDaEtapa banca in bancasFinais)
        {
            banca.VincularEtapa(Id);
            _bancas.Add(banca);
        }

        // Recursos pelo Id, que o envelope congela: aqui não há índice único além da própria
        // chave primária, e é ELA que colidiria se o mesmo recurso saísse e voltasse na mesma
        // transação. A âncora é traduzida para o Id que o produto tem depois da reconciliação —
        // sem isso ela apontaria para uma linha que esta mesma transação remove como órfã, e o
        // sintoma só apareceria muito depois, ao resolver o recurso de um ato publicado.
        List<RecursoDaEtapa> recursosVivos = [.. _recursos];
        List<RecursoDaEtapa> recursosFinais = [];
        foreach (RecursoDaEtapa congelado in recursos)
        {
            Guid ancora = ancoraCongeladaParaViva.TryGetValue(congelado.ProdutoAncoraId, out Guid viva)
                ? viva
                : congelado.ProdutoAncoraId;

            // Casa pela ÂNCORA, que é o que identifica a janela no negócio — o envelope não
            // congela o id do recurso, e casar por ele nunca acertaria: a linha viva sairia e
            // voltaria a cada restauração, trocando de identidade sem que nada tivesse mudado.
            RecursoDaEtapa? mesmaAncora = recursosVivos.Find(
                r => r.Ancora == congelado.Ancora && r.ProdutoAncoraId == ancora);
            RecursoDaEtapa destino = mesmaAncora ?? congelado;
            if (mesmaAncora is not null)
            {
                recursosVivos.Remove(mesmaAncora);
            }

            destino.ReporDadosCongelados(congelado.Ancora, congelado.Regra, congelado.Args, ancora);
            recursosFinais.Add(destino);
        }

        _recursos.Clear();
        foreach (RecursoDaEtapa recurso in recursosFinais)
        {
            recurso.VincularEtapa(Id);
            _recursos.Add(recurso);
        }
    }

    /// <summary>
    /// Confere o caráter declarado contra o que o tipo de etapa admite, acumulando as duas
    /// violações possíveis (ADR-0125).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fora de <see cref="ValidarFormaBasica"/> de propósito: aquela checagem é de forma, e esta
    /// depende de onde vêm os sinalizadores — do cadastro vivo quando a gravação lê o tipo
    /// (etapa nova, vínculo novo ou caráter alterado), do <see cref="TipoEtapaSnapshot"/> quando
    /// o tipo foi desativado e a etapa mantém o vínculo. Quem escolhe a fonte é quem chama
    /// (ADR-0129).
    /// </para>
    /// <para>
    /// Não entra em <see cref="Criar"/> nem em <see cref="AtualizarDados"/>: quem chama confere
    /// numa passada própria, antes de qualquer mutação, para devolver de uma vez a recusa de
    /// todas as etapas do payload (ADR-0125). A restauração de uma versão congelada, que repõe
    /// por <see cref="Reidratar"/> e <see cref="ReporDadosCongelados"/>, também não confere:
    /// repor não é declarar, e o par caráter e sinalizadores volta como foi conferido ao congelar.
    /// </para>
    /// </remarks>
    public static List<FieldError> ValidarCaraterAdmitido(
        CaraterEtapa carater, bool admitePontuacao, bool admiteEliminacao, string nomeDoTipo)
    {
        List<FieldError> erros = [];

        if ((carater is CaraterEtapa.Classificatoria or CaraterEtapa.Ambas) && !admitePontuacao)
        {
            erros.Add(new("carater", new DomainError(
                CaraterNaoAdmitidoPeloTipo,
                $"O tipo de etapa '{nomeDoTipo}' não compõe a nota final, então a etapa não pode ter caráter que pontua.")));
        }

        if ((carater is CaraterEtapa.Eliminatoria or CaraterEtapa.Ambas) && !admiteEliminacao)
        {
            erros.Add(new("carater", new DomainError(
                CaraterNaoAdmitidoPeloTipo,
                $"O tipo de etapa '{nomeDoTipo}' não elimina candidato, então a etapa não pode ter caráter que reprova.")));
        }

        return erros;
    }

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
    /// recurso apontam, e que chega por <paramref name="itensDeAvaliacao"/>. Os ids das
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
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso,
        decimal? notaMinima,
        int? ordem,
        string? faseCodigo = null,
        decimal? notaMaxima = null,
        decimal? passoDoLancamento = null,
        FormaDeAvaliacao? formaDeAvaliacao = null,
        ReferenciaRegra? regraConsolidacao = null,
        IReadOnlyList<ItemDeAvaliacao>? itensDeAvaliacao = null,
        IReadOnlyList<NivelDeAtendimento>? escalaDeAtendimento = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentNullException.ThrowIfNull(tipoEtapa);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A etapa reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).",
                nameof(carater));
        }

        EtapaProcesso etapa = new()
        {
            Id = id,
            Nome = nome.Trim(),
            Carater = carater,
            TipoEtapa = tipoEtapa,
            Peso = peso,
            NotaMinima = notaMinima,
            NotaMaxima = notaMaxima,
            PassoDoLancamento = passoDoLancamento,
            FormaDeAvaliacao = formaDeAvaliacao,
            RegraConsolidacao = regraConsolidacao,
            EscalaDeAtendimento = NiveisDeAtendimento.EmOrdemCanonica(escalaDeAtendimento),
            Ordem = ordem,
            FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim(),
        };
        etapa.ReporItensCongelados(itensDeAvaliacao ?? []);

        return etapa;
    }

    /// <summary>
    /// Uma etapa compõe a nota final quando o seu caráter pontua
    /// (classificatória ou ambas) e ela declara peso — é o critério que a
    /// inclui no divisor da média (<see cref="ProcessoSeletivo.CalcularDivisorMedia"/>).
    /// </summary>
    public bool ComponeNota => Carater is CaraterEtapa.Classificatoria or CaraterEtapa.Ambas && Peso.HasValue;

    /// <summary>
    /// A etapa declara que sua nota vem do ENEM — é o atributo do tipo congelado que diz
    /// isso, e é essa declaração que o motor de classificação consome.
    /// </summary>
    public bool DeclaraNotaDoEnem => TipoEtapa.NotaDeOrigemNoEnem;

    /// <summary>
    /// Atualiza os dados da MESMA etapa (mesmo <see cref="EntityBase.Id"/>) em
    /// vez de recriá-la — permite que <c>DefinirEtapasCommandHandler</c>
    /// reconcilie o payload de <c>PUT /etapas</c> com o agregado tracked
    /// preservando a identidade de uma etapa já referenciada por critério de
    /// desempate ou regra de eliminação da classificação (sem isso, qualquer
    /// reconfiguração de etapas quebraria essas referências por construção).
    /// </summary>
    public Result AtualizarDados(
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso,
        decimal? notaMinima,
        int? ordem,
        string? faseCodigo = null,
        decimal? notaMaxima = null,
        decimal? passoDoLancamento = null,
        FormaDeAvaliacao? formaDeAvaliacao = null,
        ReferenciaRegra? regraConsolidacao = null,
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itensDeAvaliacao = null,
        IReadOnlyList<NivelDeAtendimento>? escalaDeAtendimento = null)
    {
        ArgumentNullException.ThrowIfNull(tipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(
            nome, carater, peso, notaMinima, ordem, notaMaxima, passoDoLancamento, itensDeAvaliacao, escalaDeAtendimento);
        erros.AddRange(ValidarAvaliacao(
            tipoEtapa.NotaDeOrigemNoEnem, passoDoLancamento, formaDeAvaliacao, regraConsolidacao?.Codigo,
            itensDeAvaliacao?.Count > 0, escalaDeAtendimento?.Count > 0));
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Nome = nome.Trim();
        Carater = carater;
        TipoEtapa = tipoEtapa;
        Peso = peso;
        NotaMinima = notaMinima;
        NotaMaxima = notaMaxima;
        PassoDoLancamento = passoDoLancamento;
        FormaDeAvaliacao = formaDeAvaliacao;
        RegraConsolidacao = regraConsolidacao;
        EscalaDeAtendimento = NiveisDeAtendimento.EmOrdemCanonica(escalaDeAtendimento);
        Ordem = ordem;
        FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim();
        ReconciliarItens(itensDeAvaliacao);

        return Result.Success();
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Substitui os itens reconciliando pelo id: o item declarado com o id de um existente é a
    /// mesma linha com rótulo e pontuação novos, e é isso que mantém o id estável para quem aponta
    /// para ele. Sem id, ou com um que a etapa não conhece, é item novo.
    /// </summary>
    private void ReconciliarItens(IReadOnlyList<ItemDeAvaliacaoDeclarado>? declarados)
    {
        List<ItemDeAvaliacao> vivos = [.. _itensDeAvaliacao];
        List<ItemDeAvaliacao> finais = [];
        foreach (ItemDeAvaliacaoDeclarado declarado in declarados ?? [])
        {
            ItemDeAvaliacao? existente = declarado.Id is { } id ? vivos.Find(i => i.Id == id) : null;
            if (existente is null)
            {
                finais.Add(ItemDeAvaliacao.Criar(declarado));
                continue;
            }

            vivos.Remove(existente);
            existente.Atualizar(declarado.Rotulo, declarado.PontuacaoMaxima);
            finais.Add(existente);
        }

        SubstituirItens(finais);
    }

    /// <summary>
    /// Repõe os itens congelados preservando o id: o item vivo de mesmo id é reaproveitado, e o
    /// congelado que a linha viva não tem entra com o id do envelope.
    /// </summary>
    private void ReporItensCongelados(IReadOnlyList<ItemDeAvaliacao> congelados)
    {
        List<ItemDeAvaliacao> vivos = [.. _itensDeAvaliacao];
        List<ItemDeAvaliacao> finais = [];
        foreach (ItemDeAvaliacao congelado in congelados)
        {
            ItemDeAvaliacao? existente = vivos.Find(i => i.Id == congelado.Id);
            if (existente is null)
            {
                finais.Add(congelado);
                continue;
            }

            vivos.Remove(existente);
            existente.Atualizar(congelado.Rotulo, congelado.PontuacaoMaxima);
            finais.Add(existente);
        }

        SubstituirItens(finais);
    }

    private void SubstituirItens(List<ItemDeAvaliacao> itens)
    {
        _itensDeAvaliacao.Clear();
        foreach (ItemDeAvaliacao item in itens)
        {
            item.VincularEtapa(Id);
            _itensDeAvaliacao.Add(item);
        }
    }


    /// <summary>
    /// Recusa a contradição entre o que a etapa declara sobre como a nota é obtida, em rascunho
    /// também: a falta (de forma, de regra, de passo, de banca) é tolerada, porque o gate de
    /// publicação a cobra, e a recusa que depende do grafo — a etapa sem nota não declara forma —
    /// também é do gate.
    /// </summary>
    /// <remarks>
    /// A etapa de nota do ENEM não declara forma nem regra: a nota vem do INEP. Quando recusa por
    /// isso, não repete as demais recusas, que seriam consequência da mesma causa. Recebe só o
    /// código da regra, para a Application poder rodar a coerência de todas as etapas antes de ler
    /// o catálogo (ADR-0125); que a regra exista nele e seja do tipo de consolidação é conferido
    /// por ela (ADR-0042). O código ser do rol conhecido é a defesa do domínio, a mesma que o
    /// decodificador do envelope aplica ao reidratar.
    /// </remarks>
    public static List<FieldError> ValidarAvaliacao(
        bool notaDeOrigemNoEnem,
        decimal? passoDoLancamento,
        FormaDeAvaliacao? forma,
        string? regraConsolidacaoCodigo,
        bool declaraItens = false,
        bool declaraEscala = false)
    {
        List<FieldError> erros = [];

        if (notaDeOrigemNoEnem)
        {
            if (forma is not null)
            {
                erros.Add(new("formaDeAvaliacao", new DomainError(
                    AvaliacaoNaEtapaDoEnem,
                    "A etapa de nota do ENEM não declara forma de avaliação: a nota vem do INEP.")));
            }

            if (regraConsolidacaoCodigo is not null)
            {
                erros.Add(new("regraConsolidacao", new DomainError(
                    AvaliacaoNaEtapaDoEnem,
                    "A etapa de nota do ENEM não declara regra de consolidação: a nota vem do INEP.")));
            }

            if (declaraItens)
            {
                erros.Add(new("itensDeAvaliacao", new DomainError(
                    ItensNaEtapaDoEnem,
                    "A etapa de nota do ENEM não declara itens de avaliação: a nota vem do INEP.")));
            }

            if (declaraEscala)
            {
                erros.Add(new("escalaDeAtendimento", new DomainError(
                    ItensNaEtapaDoEnem,
                    "A etapa de nota do ENEM não declara escala de atendimento: a nota vem do INEP.")));
            }

            return erros;
        }

        if (regraConsolidacaoCodigo is not null)
        {
            if (forma != Enums.FormaDeAvaliacao.Humana)
            {
                erros.Add(new("regraConsolidacao", new DomainError(
                    RegraDeConsolidacaoForaDaFormaHumana,
                    "A regra de consolidação só se aplica à avaliação humana: com mais de um avaliador é que as notas precisam ser combinadas.")));
            }
            else if (!RegraConsolidacaoCodigo.Rol.Contains(regraConsolidacaoCodigo))
            {
                erros.Add(new("regraConsolidacao", new DomainError(
                    RegraDeConsolidacaoInvalida,
                    $"A regra '{regraConsolidacaoCodigo}' não é uma regra de consolidação do catálogo.")));
            }
        }

        if (forma == Enums.FormaDeAvaliacao.Objetiva && passoDoLancamento is not null)
        {
            erros.Add(new("passoDoLancamento", new DomainError(
                PassoNaFormaObjetiva,
                "O passo do lançamento só se aplica à avaliação humana: na avaliação objetiva ninguém lança a nota.")));
        }

        if (declaraEscala)
        {
            if (!declaraItens)
            {
                erros.Add(new("escalaDeAtendimento", new DomainError(
                    EscalaSemItens,
                    "A escala de atendimento é aplicada a cada item: sem itens de avaliação, não há o que atender.")));
            }

            if (forma == Enums.FormaDeAvaliacao.Objetiva)
            {
                erros.Add(new("escalaDeAtendimento", new DomainError(
                    EscalaNaFormaObjetiva,
                    "A escala de atendimento só se aplica à avaliação humana: na avaliação objetiva ninguém escolhe o nível.")));
            }

            if (passoDoLancamento is not null)
            {
                erros.Add(new("passoDoLancamento", new DomainError(
                    PassoComEscalaDeAtendimento,
                    "Com escala de atendimento a banca escolhe o nível e não digita a nota, então a etapa não declara passo do lançamento.")));
            }
        }

        return erros;
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

        Inicio = inicio?.ToUniversalTime();
        Fim = fim?.ToUniversalTime();
        EmiteParecerIndividual = emiteParecerIndividual;
        return Result.Success();
    }

    /// <summary>Substitui as bancas da etapa por inteiro, recusando o mesmo tipo duas vezes.</summary>
    public Result DefinirBancas(IReadOnlyList<BancaDaEtapa> bancas)
    {
        ArgumentNullException.ThrowIfNull(bancas);

        // Normalizado pela mesma razão do ato do produto: o documento canônico emite o código
        // em NFC, e duas grafias do mesmo código passariam a guarda para depois sair do
        // envelope como duas bancas de código idêntico.
        List<string> codigos = [.. bancas.Select(b => HashCanonicalComputer.NormalizeNfc(b.Codigo))];
        if (codigos.Distinct(StringComparer.Ordinal).Count() != codigos.Count)
        {
            return Result.Failure(new DomainError(
                "EtapaProcesso.BancaDuplicadaNaEtapa",
                "Cada tipo de banca pode ser requerido uma única vez por etapa."));
        }

        // Mesma reconciliação dos produtos, pela mesma razão: o código identifica a banca, e a
        // linha que continua declarada continua sendo a mesma linha.
        List<BancaDaEtapa> bancasDisponiveis = [.. _bancas];
        List<BancaDaEtapa> bancasReconciliadas = [];
        foreach (BancaDaEtapa declarada in bancas)
        {
            string codigoDeclarado = HashCanonicalComputer.NormalizeNfc(declarada.Codigo);
            BancaDaEtapa? existente = bancasDisponiveis.Find(b =>
                string.Equals(
                    HashCanonicalComputer.NormalizeNfc(b.Codigo),
                    codigoDeclarado,
                    StringComparison.Ordinal));

            if (existente is null)
            {
                bancasReconciliadas.Add(declarada);
                continue;
            }

            bancasDisponiveis.Remove(existente);
            existente.ReporOrigem(declarada.TipoBancaOrigemId);
            bancasReconciliadas.Add(existente);
        }

        _bancas.Clear();
        foreach (BancaDaEtapa banca in bancasReconciliadas)
        {
            banca.VincularEtapa(Id);
            _bancas.Add(banca);
        }

        return Result.Success();
    }

    /// <summary>
    /// A violação da âncora das janelas de recurso que a etapa declara, ou
    /// <see langword="null"/> quando todas se ancoram onde podem. É o predicado que a gravação
    /// das janelas e o gate de publicação chamam: o estado carregado do banco não passa pela
    /// gravação, e só o gate o alcança.
    /// </summary>
    internal DomainError? ViolacaoDaAncoraDoRecurso() => ViolacaoDaAncoraDoRecurso(_recursos);

    private DomainError? ViolacaoDaAncoraDoRecurso(IReadOnlyCollection<RecursoDaEtapa> recursos)
    {
        HashSet<Guid> preliminares =
            [.. _produtos.Where(p => p.Papel == PapelProdutoFase.Preliminar).Select(p => p.Id)];

        foreach (RecursoDaEtapa recurso in recursos)
        {
            // Na etapa de nota do ENEM, o recurso em ato não cabe de modo algum, e a regra do
            // processo diz isso nomeando o caminho certo (a regra de recurso da fase). Pedir
            // aqui um preliminar que ela depois recusa mandaria o operador na direção errada.
            if (recurso.Ancora == AncoraDoRecurso.AtoPublicado && !DeclaraNotaDoEnem && !preliminares.Contains(recurso.ProdutoAncoraId))
            {
                return new DomainError(
                    "EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa",
                    "O recurso contra ato publicado ancora num resultado preliminar desta etapa — é a publicação dele que abre a janela.");
            }

            if (recurso.Ancora == AncoraDoRecurso.CienciaIndividual && !EmiteParecerIndividual)
            {
                return new DomainError(
                    "EtapaProcesso.CienciaSemParecerIndividual",
                    "O prazo que corre da ciência do candidato exige que a etapa prometa parecer individual: é dele que o candidato toma ciência.");
            }
        }

        return null;
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

        if (ViolacaoDaAncoraDoRecurso(recursos) is { } violacaoDaAncora)
        {
            return Result.Failure(violacaoDaAncora);
        }

        List<string> fatos =
            [.. recursos.Select(r => r.Ancora == AncoraDoRecurso.AtoPublicado
                ? $"ato:{r.ProdutoAncoraId}"
                : "ciencia")];
        if (fatos.Distinct(StringComparer.Ordinal).Count() != fatos.Count)
        {
            return Result.Failure(new DomainError(
                "EtapaProcesso.RecursoDuplicadoNaEtapa",
                "Cada fato recorrível abre uma janela só: dois prazos para a mesma publicação, ou dois para a ciência, seriam ambíguos."));
        }

        _recursos.Clear();
        foreach (RecursoDaEtapa recurso in recursos)
        {
            recurso.VincularEtapa(Id);
            _recursos.Add(recurso);
        }

        return Result.Success();
    }
}
