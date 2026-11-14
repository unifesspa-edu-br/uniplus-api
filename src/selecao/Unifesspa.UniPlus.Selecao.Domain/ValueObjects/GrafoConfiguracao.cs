namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// As dimensões da configuração de um <see cref="ProcessoSeletivo"/>,
/// reconstruídas a partir de uma <see cref="VersaoConfiguracao"/> congelada
/// (ADR-0110 D1/D2) e repostas no agregado por
/// <see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>.
/// </summary>
/// <remarks>
/// <para>
/// É <b>classe</b>, e não <c>record</c>, deliberadamente: carrega entidades mutáveis
/// e listas, cuja igualdade de <c>record</c> seria <b>referencial</b>. Prometer
/// semântica de valor onde ela não existe convidaria a comparar dois grafos com
/// <c>==</c> e a concluir que diferem quando são o mesmo conteúdo — num caminho cuja
/// única prova de correção é justamente a igualdade (o round-trip byte-a-byte, que
/// se faz sobre os <b>bytes canônicos</b>, nunca sobre este objeto).
/// </para>
/// <para>
/// Vive no <b>Domain</b> porque a raiz o recebe. O envelope completo — este grafo
/// mais os <c>DadosEdital</c>, o hash do documento e a <c>RetificacaoDoEdital</c> — é da
/// Application, que é quem conhece o codec (ADR-0042).
/// </para>
/// </remarks>
public sealed class GrafoConfiguracao
{
    public GrafoConfiguracao(
        IReadOnlyList<EtapaProcesso> etapas,
        OfertaAtendimentoEspecializado ofertaAtendimento,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas,
        ConfiguracaoBonusRegional? bonusRegional,
        IReadOnlyList<CriterioDesempate> criteriosDesempate,
        ConfiguracaoClassificacao classificacao,
        IReadOnlyList<FaseCronograma> cronogramaFases,
        IReadOnlyList<DocumentoExigido> documentosExigidos,
        IReadOnlyList<NoExigencia> nosExigencia,
        ReferenciaTemporalFatos? referenciaTemporalFatos,
        // Fatos e regras são opcionais com default vazio: um processo sem coleta nem derivação é
        // estado válido. O decoder do envelope sempre os passa explicitamente.
        IReadOnlyList<FatoColetado>? fatosColetados = null,
        IReadOnlyList<ConfiguracaoDerivacaoFato>? regrasDerivacao = null,
        // Cascata de remanejamento: ausência = nenhuma cascata configurada (toggle por presença,
        // como o bônus regional).
        ConfiguracaoCascataRemanejamento? cascataRemanejamento = null,
        // Formulários por finalidade (UNI-REQ-0144) e os termos que eles exigem (UNI-REQ-0086);
        // ausência = nenhum.
        IReadOnlyList<FormularioProcesso>? formularios = null,
        IReadOnlyList<TermoExigidoFormulario>? termosExigidos = null,
        // Divulgação pública (UNI-REQ-0050): ausência = default minimizado (só o número de
        // inscrição), toggle por presença como o bônus regional e a cascata.
        ConfiguracaoDivulgacao? configuracaoDivulgacao = null,
        // Taxa de inscrição e isenção: ausência = "ainda não declarado", estado que bloqueia a
        // publicação, diferente do bônus regional (onde ausência é estado válido). O grafo só
        // carrega o valor tal como veio congelado; a recusa acontece na publicação, não aqui.
        ConfiguracaoTaxaInscricao? configuracaoTaxaInscricao = null,
        // Localidade regente (UNI-REQ-0111): a raiz só a repõe quando presente. Diferente do fuso,
        // que acompanha o envelope reidratado, é estado vivo da raiz e volta para ela no descarte
        // da retificação.
        LocalidadeRegente? localidade = null,
        // Convenção de contagem (UNI-REQ-0112): a versão publicada pode não ter declarado, porque
        // nenhuma contagem sua distingue dia útil. A raiz repõe o que veio, inclusive a ausência,
        // para que o descarte devolva a configuração que a versão de fato tinha.
        ReferenciaRegra? algoritmoContagemPrazo = null,
        // Identificador legível: o endereço público do certame, congelado com a versão. A raiz
        // repõe o que veio, inclusive a ausência: descartar uma retificação devolve o identificador
        // que a versão vigente de fato tinha.
        IdentificadorLegivel? identificadorLegivel = null,
        // Opções declaradas pelo processo: reconstruídas pelo decoder a partir dos valores
        // selecionáveis congelados dos fatos coletados de fonte do processo.
        IReadOnlyList<OpcaoDeclaradaFato>? opcoesDeclaradas = null,
        // Grupos repetíveis dos formulários (UNI-REQ-0146), com os seus campos; ausência = nenhum.
        IReadOnlyList<GrupoColetado>? gruposColetados = null,
        // Número de opções de curso (1 ou 2): dimensão da raiz, congelada em bloco próprio. A raiz
        // repõe o que veio.
        int? numeroDeOpcoesDeCurso = null)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(ofertaAtendimento);
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);
        ArgumentNullException.ThrowIfNull(criteriosDesempate);
        ArgumentNullException.ThrowIfNull(classificacao);
        ArgumentNullException.ThrowIfNull(cronogramaFases);
        ArgumentNullException.ThrowIfNull(documentosExigidos);
        ArgumentNullException.ThrowIfNull(nosExigencia);

        // Cópias defensivas: o grafo é a fronteira entre os bytes congelados e o
        // agregado vivo. Guardar a referência do caller deixaria uma janela em que
        // mutá-la, depois da validação e antes da reposição, reporia configuração
        // que nunca foi validada.
        Localidade = localidade;
        NumeroDeOpcoesDeCurso = numeroDeOpcoesDeCurso;
        AlgoritmoContagemPrazo = algoritmoContagemPrazo;
        IdentificadorLegivel = identificadorLegivel;
        Etapas = [.. etapas];
        OfertaAtendimento = ofertaAtendimento;
        DistribuicaoVagas = [.. distribuicaoVagas];
        BonusRegional = bonusRegional;
        CriteriosDesempate = [.. criteriosDesempate];
        Classificacao = classificacao;
        CronogramaFases = [.. cronogramaFases];
        DocumentosExigidos = [.. documentosExigidos];
        NosExigencia = [.. nosExigencia];
        ReferenciaTemporalFatos = referenciaTemporalFatos;
        FatosColetados = fatosColetados is null ? [] : [.. fatosColetados];
        GruposColetados = gruposColetados is null ? [] : [.. gruposColetados];
        OpcoesDeclaradas = opcoesDeclaradas is null ? [] : [.. opcoesDeclaradas];
        RegrasDerivacao = regrasDerivacao is null ? [] : [.. regrasDerivacao];
        CascataRemanejamento = cascataRemanejamento;
        Formularios = formularios is null ? [] : [.. formularios];
        TermosExigidos = termosExigidos is null ? [] : [.. termosExigidos];
        ConfiguracaoDivulgacao = configuracaoDivulgacao;
        ConfiguracaoTaxaInscricao = configuracaoTaxaInscricao;
    }

    public IReadOnlyList<EtapaProcesso> Etapas { get; }

    /// <summary>Quantos cursos o candidato escolhe na inscrição; ausência = não declarado.</summary>
    public int? NumeroDeOpcoesDeCurso { get; }

    public OfertaAtendimentoEspecializado OfertaAtendimento { get; }

    public IReadOnlyList<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas { get; }

    /// <summary>Ausência = sem bônus (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015); não existe "BONUS-NENHUM".</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; }

    public IReadOnlyList<CriterioDesempate> CriteriosDesempate { get; }

    public ConfiguracaoClassificacao Classificacao { get; }

    /// <summary>O cronograma de fases, 1..*.</summary>
    public IReadOnlyList<FaseCronograma> CronogramaFases { get; }

    /// <summary>Documentos exigidos, 0..* (as folhas, com a configuração de cada exigência).</summary>
    public IReadOnlyList<DocumentoExigido> DocumentosExigidos { get; }

    /// <summary>Árvore de satisfação, 0..* (todos os nós, planos: as raízes têm <c>NoPaiId == null</c>).</summary>
    public IReadOnlyList<NoExigencia> NosExigencia { get; }

    /// <summary>
    /// A <see cref="ValueObjects.ReferenciaTemporalFatos"/>: o insumo que
    /// <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/> resolve para a
    /// <c>dataReferenciaFatos</c> congelada. Ausência = nenhuma política configurada (estado
    /// válido enquanto não existir gatilho por <c>FAIXA_ETARIA</c>).
    /// </summary>
    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; }

    /// <summary>
    /// Os fatos que o processo coleta do candidato, congelados no envelope e repostos na restauração.
    /// </summary>
    public IReadOnlyList<FatoColetado> FatosColetados { get; }

    public IReadOnlyList<GrupoColetado> GruposColetados { get; }

    public IReadOnlyList<OpcaoDeclaradaFato> OpcoesDeclaradas { get; }

    /// <summary>
    /// As regras de derivação dos fatos derivados do processo, congeladas no envelope e repostas na restauração.
    /// </summary>
    public IReadOnlyList<ConfiguracaoDerivacaoFato> RegrasDerivacao { get; }

    /// <summary>
    /// As derivações congeladas e as que a oferta de vagas congelada define (<see cref="DerivacoesDaOferta"/>),
    /// as mesmas que o processo vivo avaliava. Com <paramref name="ofertasDeCurso"/>, as que a união das
    /// distribuições dessas ofertas define (<see cref="DerivacoesDaOferta.DistribuicaoDasOfertas"/>); sem
    /// elas, a união de todas.
    /// </summary>
    public IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesEfetivas(IReadOnlyCollection<Guid>? ofertasDeCurso = null) =>
        DerivacoesDaOferta.Efetivas(RegrasDerivacao, DerivacoesDaOferta.DistribuicaoDasOfertas(DistribuicaoVagas, ofertasDeCurso ?? []));

    /// <summary>
    /// A cascata de remanejamento das cotas reservadas, congelada no envelope e reposta na
    /// restauração. Ausência = nenhuma cascata configurada (toggle por presença, como
    /// <see cref="BonusRegional"/>).
    /// </summary>
    public ConfiguracaoCascataRemanejamento? CascataRemanejamento { get; }

    /// <summary>Os formulários do processo, um por finalidade (UNI-REQ-0144).</summary>
    public IReadOnlyList<FormularioProcesso> Formularios { get; }

    /// <summary>Os termos exigidos pelos formulários, cada um com a sua finalidade, congelados por versão (UNI-REQ-0086).</summary>
    public IReadOnlyList<TermoExigidoFormulario> TermosExigidos { get; }

    /// <summary>
    /// Divulgação pública do certame (UNI-REQ-0050), congelada no envelope e reposta na
    /// restauração. Ausência = default minimizado (só o número de inscrição), toggle por
    /// presença como <see cref="BonusRegional"/>.
    /// </summary>
    public ConfiguracaoDivulgacao? ConfiguracaoDivulgacao { get; }

    /// <summary>
    /// Taxa de inscrição e isenção, congelada no envelope e reposta na restauração. Ausência =
    /// "ainda não declarado", que não é estado válido de publicação, diferente de
    /// <see cref="BonusRegional"/>.
    /// </summary>
    public ConfiguracaoTaxaInscricao? ConfiguracaoTaxaInscricao { get; }

    /// <summary>
    /// Município cujo calendário regia a contagem dos prazos quando esta versão foi publicada
    /// (UNI-REQ-0111). <see langword="null"/> quando o grafo não a traz: a raiz mantém a viva.
    /// </summary>
    public LocalidadeRegente? Localidade { get; }

    /// <summary>
    /// Convenção de contagem que o certame declarava quando esta versão foi publicada
    /// (UNI-REQ-0112). <see langword="null"/> é valor legítimo aqui: a versão pode não ter
    /// nenhuma contagem que distinga dia útil, e nesse caso não declarou convenção alguma.
    /// </summary>
    public ReferenciaRegra? AlgoritmoContagemPrazo { get; }

    /// <summary>
    /// Identificador legível que a versão congelou. <see langword="null"/> quando a versão foi
    /// congelada sem ele.
    /// </summary>
    public IdentificadorLegivel? IdentificadorLegivel { get; }
}
