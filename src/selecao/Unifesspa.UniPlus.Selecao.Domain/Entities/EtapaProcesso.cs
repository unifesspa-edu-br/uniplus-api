namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Etapa <em>pontuada</em> do <see cref="ProcessoSeletivo"/> (peso, caráter e
/// nota mínima) — distinta de fase do cronograma, que é o eixo temporal do
/// certame. Entidade interna do agregado: criada, substituída e persistida
/// exclusivamente pela raiz.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete) de propósito:
/// a configuração em rascunho é substituível por inteiro (comandos
/// <c>Definir*</c>) e a trilha auditável do que valeu em cada publicação é o
/// snapshot RN08 da Story #759 — não faz sentido acumular linhas logicamente
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

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>
    /// A fase do cronograma a que esta etapa pertence. É o vínculo que substitui a
    /// bicondicional por sinalizador: qualquer fase pode subdividir-se em etapas, e é a
    /// posição na fase — não um booleano do cadastro — que diz quais são as dela.
    /// </summary>
    public Guid FaseCronogramaId { get; private set; }

    /// <summary>
    /// Código canônico da fase declarada pelo cliente (ex.: <c>"AVALIACAO"</c>). A raiz o
    /// resolve contra o cronograma para preencher <see cref="FaseCronogramaId"/>. É por
    /// código, e não por id, porque o id da fase não sobrevive à reconciliação da
    /// restauração — mesmo motivo de <c>FaseCronograma.FaseConcluinteCodigo</c>.
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

        ProdutoDaEtapa? duplicado = produtos
            .GroupBy(static p => (p.AtoCodigo, p.Papel))
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
            ProdutoDaEtapa? existente = disponiveis.FirstOrDefault(p =>
                string.Equals(
                    HashCanonicalComputer.NormalizeNfc(p.AtoCodigo),
                    HashCanonicalComputer.NormalizeNfc(declarado.AtoCodigo),
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
    /// escala de <see cref="Peso"/>/<see cref="NotaMinima"/> (<c>numeric(18,4)</c>) permanecem
    /// só no validator — não há ainda um helper de domínio para limite decimal (achado B2 do
    /// mapeamento da rolagem), então checar aqui duplicaria uma regra de forma de wire/coluna
    /// sem trazer nenhum ganho de correção.
    /// </summary>
    public static Result<EtapaProcesso> Criar(
        string nome,
        CaraterEtapa carater,
        TipoEtapaSnapshot tipoEtapa,
        decimal? peso = null,
        decimal? notaMinima = null,
        int? ordem = null,
        string? faseCodigo = null)
    {
        ArgumentNullException.ThrowIfNull(tipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(nome, carater, peso, notaMinima, ordem);
        if (erros.Count > 0)
        {
            return Result<EtapaProcesso>.ValidationFailure(erros);
        }

        return Result<EtapaProcesso>.Success(new EtapaProcesso
        {
            Nome = nome.Trim(),
            Carater = carater,
            TipoEtapa = tipoEtapa,
            Peso = peso,
            NotaMinima = notaMinima,
            Ordem = ordem,
            FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim(),
        });
    }

    /// <summary>
    /// Nome/Carater/Peso/NotaMinima/Ordem não dependem de nenhum cadastro cross-módulo —
    /// diferente de <see cref="TipoEtapa"/> (resolvido à parte, contra o cadastro vivo de
    /// Configuração, por quem chama). Compartilhada entre <see cref="Criar"/> e
    /// <see cref="AtualizarDados"/> (mesmas checagens) e exposta para o handler poder
    /// confirmar a forma de TODAS as etapas do payload numa primeira passada, antes de
    /// resolver o tipo de etapa no cadastro (mesmo padrão de
    /// <c>FatoColetado.ValidarFormaBasica</c>, PR #1214).
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(
        string? nome, CaraterEtapa carater, decimal? peso, decimal? notaMinima, int? ordem)
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

        if (ordem is <= 0)
        {
            erros.Add(new("ordem", new DomainError(
                "EtapaProcesso.OrdemInvalida", "A ordem da etapa, quando informada, deve ser maior que zero.")));
        }

        return erros;
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
        IReadOnlyList<RecursoDaEtapa> recursos)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(tipoEtapa);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancas);
        ArgumentNullException.ThrowIfNull(recursos);

        Nome = nome.Trim();
        Carater = carater;
        TipoEtapa = tipoEtapa;
        Peso = peso;
        NotaMinima = notaMinima;
        Ordem = ordem;
        FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim();
        Inicio = inicio?.ToUniversalTime();
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
            ProdutoDaEtapa? mesmoParaOMesmoPapel = disponiveis.Find(
                p => string.Equals(p.AtoCodigo, congelado.AtoCodigo, StringComparison.Ordinal)
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
            ProdutoDaEtapa? mesmoAto = disponiveis.Find(
                p => string.Equals(p.AtoCodigo, congelado.AtoCodigo, StringComparison.Ordinal));
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
            BancaDaEtapa? mesmoCodigo = bancasVivas.Find(
                b => string.Equals(b.Codigo, congelada.Codigo, StringComparison.Ordinal));
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
    /// Confere o caráter declarado contra o que o tipo de etapa admite no cadastro de
    /// Configuração, acumulando as duas violações possíveis (ADR-0125).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fora de <see cref="ValidarFormaBasica"/> de propósito: aquela checagem é de forma e não
    /// depende de cadastro nenhum, enquanto esta precisa dos sinalizadores que só a Application
    /// consegue resolver contra o catálogo vivo — é o mesmo arranjo do
    /// <see cref="TipoEtapaSnapshot"/>, resolvido por quem chama e entregue pronto ao domínio
    /// (ADR-0129).
    /// </para>
    /// <para>
    /// Não entra em <see cref="Criar"/> nem em <see cref="AtualizarDados"/> pelo mesmo motivo:
    /// a restauração de uma versão congelada reaplica dados que foram válidos quando o catálogo
    /// era outro, e ela não tem como reconsultar o cadastro.
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
    /// O <c>id</c> da etapa é o <b>único</b> id de entidade-filha que o envelope congela
    /// (ADR-0110 D2), porque <c>criteriosDesempate.args.etapaRef</c> e
    /// <c>regrasEliminacao.args.etapaRef</c> apontam para ele: sem preservá-lo, o
    /// snapshot reidratado teria referências que não resolvem, e o desempate e a
    /// eliminação do certame ficariam inexecutáveis. Os ids das demais filhas são
    /// regenerados — nenhuma referência de negócio exige estabilidade deles, e as FKs
    /// internas são reconstruídas junto com o grafo.
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
        string? faseCodigo = null)
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

        return new EtapaProcesso
        {
            Id = id,
            Nome = nome.Trim(),
            Carater = carater,
            TipoEtapa = tipoEtapa,
            Peso = peso,
            NotaMinima = notaMinima,
            Ordem = ordem,
            FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim(),
        };
    }

    /// <summary>
    /// Uma etapa compõe a nota final quando o seu caráter pontua
    /// (classificatória ou ambas) e ela declara peso — é o critério que a
    /// inclui no divisor da média (<see cref="ProcessoSeletivo.CalcularDivisorMedia"/>).
    /// </summary>
    public bool ComponeNota => Carater is CaraterEtapa.Classificatoria or CaraterEtapa.Ambas && Peso.HasValue;

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
        string? faseCodigo = null)
    {
        ArgumentNullException.ThrowIfNull(tipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(nome, carater, peso, notaMinima, ordem);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Nome = nome.Trim();
        Carater = carater;
        TipoEtapa = tipoEtapa;
        Peso = peso;
        NotaMinima = notaMinima;
        Ordem = ordem;
        FaseCodigo = string.IsNullOrWhiteSpace(faseCodigo) ? null : faseCodigo.Trim();

        return Result.Success();
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Prende a etapa à fase que a contém. Chamado pela raiz, que é quem enxerga o
    /// cronograma inteiro e pode conferir que a fase existe nele.
    /// </summary>
    internal void VincularFase(Guid faseCronogramaId) =>
        FaseCronogramaId = faseCronogramaId;

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

        List<string> codigos = [.. bancas.Select(b => b.Codigo)];
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
            BancaDaEtapa? existente = bancasDisponiveis.FirstOrDefault(b =>
                string.Equals(
                    HashCanonicalComputer.NormalizeNfc(b.Codigo),
                    HashCanonicalComputer.NormalizeNfc(declarada.Codigo),
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
    /// Substitui as janelas recursais por inteiro. Prova o que só a etapa enxerga: a âncora
    /// em ato referencia um produto preliminar desta etapa; a âncora em ciência exige a
    /// promessa de parecer individual, porque é dele que corre o prazo; e não há duas
    /// janelas para o mesmo fato.
    /// </summary>
    public Result DefinirRecursos(IReadOnlyList<RecursoDaEtapa> recursos)
    {
        ArgumentNullException.ThrowIfNull(recursos);

        HashSet<Guid> preliminares =
            [.. _produtos.Where(p => p.Papel == PapelProdutoFase.Preliminar).Select(p => p.Id)];

        foreach (RecursoDaEtapa recurso in recursos)
        {
            if (recurso.Ancora == AncoraDoRecurso.AtoPublicado && !preliminares.Contains(recurso.ProdutoAncoraId))
            {
                return Result.Failure(new DomainError(
                    "EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa",
                    "O recurso contra ato publicado ancora num resultado preliminar desta etapa — é a publicação dele que abre a janela."));
            }

            if (recurso.Ancora == AncoraDoRecurso.CienciaIndividual && !EmiteParecerIndividual)
            {
                return Result.Failure(new DomainError(
                    "EtapaProcesso.CienciaSemParecerIndividual",
                    "O prazo que corre da ciência do candidato exige que a etapa prometa parecer individual: é dele que o candidato toma ciência."));
            }
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
