namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Events;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Agregado-raiz do certame (UNI-REQ-0014/0015): o administrador cria o processo em rascunho e monta
/// a configuração a partir dos cadastros de referência. Todas as entidades de configuração pendem
/// desta raiz e são acessadas e persistidas exclusivamente por ela.
/// </summary>
/// <remarks>
/// O documento normativo não pertence a esta raiz: é o ato publicado, que vive em <c>Publicacoes</c>
/// (ADR-0103/0105). A publicação produz aqui a <see cref="VersaoConfiguracao"/> congelada
/// (UNI-REQ-0019), que referencia o ato por valor — o par <c>{id, hash}</c>, sem chave estrangeira
/// (ADR-0061). Enquanto o processo está em rascunho a configuração é substituível; é CRUD puro via
/// EF Core, e a fronteira de Event Sourcing (ADR-0069) começa nos agregados de decisão downstream.
/// </remarks>
public sealed class ProcessoSeletivo : SoftDeletableEntity
{
    public string Nome { get; private set; } = string.Empty;

    /// <summary>
    /// Identificador legível, de onde derivam o endereço da página pública do certame e a chave do
    /// documento no acervo. Imutável depois de constar em versão publicada
    /// (<see cref="DefinirIdentificadorLegivel"/>): trocá-lo mudaria o endereço de um certame que o
    /// público já conhece.
    /// </summary>
    public IdentificadorLegivel? IdentificadorLegivel { get; private set; }

    /// <summary>Id da origem em Configuração, sem FK cross-schema (ADR-0061).</summary>
    public Guid TipoProcessoOrigemId => TipoProcesso.OrigemId;

    /// <summary>Cópia do tipo escolhido na criação, imutável para a vida do processo.</summary>
    public TipoProcessoSnapshot TipoProcesso { get; private set; } = null!;

    /// <summary>Alias de compatibilidade interna para fixtures; o contrato HTTP expõe <see cref="TipoProcesso"/>.</summary>
    public TipoProcessoSnapshot Tipo => TipoProcesso;
    public StatusProcesso Status { get; private set; }

    /// <summary>De onde vêm os candidatos do certame; deriva o piso mínimo do cronograma e nunca ramifica por <see cref="TipoProcesso"/>.</summary>
    public OrigemCandidatos OrigemCandidatos { get; private set; }

    /// <summary>Quem responde pelo certame: escalar de topo sem FK entre schemas, resolvido uma única vez na criação (ADR-0056) e imutável depois.</summary>
    public Guid UnidadeAdministradoraOrigemId { get; private set; }

    /// <summary>Cópia (ADR-0061) da unidade administradora, congelada na criação.</summary>
    public UnidadeAdministradoraSnapshot UnidadeAdministradora { get; private set; } = null!;

    /// <summary>
    /// Município cujo calendário rege a contagem dos prazos do certame (UNI-REQ-0111). Não deriva da
    /// unidade administradora: a cidade dela diz onde fica, não sob que calendário o certame corre.
    /// </summary>
    public LocalidadeRegente Localidade { get; private set; } = null!;

    /// <summary>
    /// Convenção de contagem dos prazos que distinguem dia útil (UNI-REQ-0112), identificada por
    /// <c>(codigo, versao, hash)</c>. Ausente só impede publicar quando alguma contagem depende dela.
    /// É uma por processo: por fase, duas fases do mesmo edital poderiam ler diferente o que é dia útil.
    /// </summary>
    public ReferenciaRegra? AlgoritmoContagemPrazo { get; private set; }

    private readonly List<FormularioProcesso> _formularios = [];

    /// <summary>Os formulários do processo (UNI-REQ-0144).</summary>
    public IReadOnlyCollection<FormularioProcesso> Formularios => _formularios.AsReadOnly();

    private readonly List<TermoExigidoFormulario> _termosExigidos = [];

    /// <summary>Os termos de consentimento ou declaração que os formulários exigem, congelados por versão (UNI-REQ-0086), cada um com a finalidade do seu formulário.</summary>
    public IReadOnlyCollection<TermoExigidoFormulario> TermosExigidos => _termosExigidos.AsReadOnly();

    private readonly List<EtapaProcesso> _etapas = [];
    public IReadOnlyCollection<EtapaProcesso> Etapas => _etapas.AsReadOnly();

    /// <summary>As etapas declaradas na fase <paramref name="faseCodigo"/>, na ordem em que foram enviadas.</summary>
    public IReadOnlyList<EtapaProcesso> EtapasDaFase(string faseCodigo) =>
        [.. _etapas.Where(e => string.Equals(e.FaseCodigo, faseCodigo, StringComparison.Ordinal))];

    private readonly List<FaseCronograma> _cronogramaFases = [];

    /// <summary>O eixo temporal do certame, distinto das <see cref="Etapas"/> (eixo de pontuação).</summary>
    public IReadOnlyCollection<FaseCronograma> CronogramaFases => _cronogramaFases.AsReadOnly();

    private readonly List<DocumentoExigido> _documentosExigidos = [];

    /// <summary>Documentos exigidos do certame, por fase, aplicabilidade e gatilho.</summary>
    public IReadOnlyCollection<DocumentoExigido> DocumentosExigidos => _documentosExigidos.AsReadOnly();

    /// <summary>Todo campo que o processo coleta: os itens dos formulários e os campos dos grupos repetíveis, que se distinguem pelo grupo.</summary>
    private readonly List<FatoColetado> _campos = [];

    /// <summary>Os fatos coletados do candidato, com a ordem e a pré-condição de cada um; a pré-condição só cita fatos anteriores na ordem.</summary>
    public IReadOnlyCollection<FatoColetado> FatosColetados => [.. Itens];

    /// <summary>Os itens dos formulários: os campos fora de grupo repetível.</summary>
    private IEnumerable<FatoColetado> Itens => EstadoDaConfiguracao.De(this).Itens;

    private readonly List<GrupoColetado> _gruposColetados = [];

    /// <summary>Os grupos repetíveis dos formulários (UNI-REQ-0146), cada um dono dos seus campos, que não entram em <see cref="FatosColetados"/>.</summary>
    public IReadOnlyCollection<GrupoColetado> GruposColetados => _gruposColetados.AsReadOnly();

    /// <summary>Os itens e os campos dos grupos: todo campo que o processo apresenta ao candidato.</summary>
    public IReadOnlyCollection<FatoColetado> Campos => _campos.AsReadOnly();

    private readonly List<OpcaoDeclaradaFato> _opcoesDeclaradas = [];

    /// <summary>As opções que o processo declara para os fatos cuja fonte dos valores é o processo, exceto os de atendimento especializado, cujas opções vêm da oferta.</summary>
    public IReadOnlyCollection<OpcaoDeclaradaFato> OpcoesDeclaradas => _opcoesDeclaradas.AsReadOnly();

    /// <summary>As opções do processo para o fato, em ordem: as da oferta de atendimento, quando ela gere o fato, ou as declaradas. É a única leitura delas.</summary>
    public IReadOnlyList<OpcaoDoProcesso> OpcoesDoProcesso(string fatoCodigo) =>
        RegrasDasOpcoesDeclaradas.OpcoesDoProcesso(EstadoDaConfiguracao.De(this), fatoCodigo);

    /// <summary>
    /// Substitui as opções declaradas para um fato de fonte do processo. Recusa o fato cujas opções
    /// vêm da oferta, a lista vazia, o código repetido e a redefinição que deixaria de fora um código
    /// que uma exigência viva ainda cita, pelo próprio fato ou por um agregado cujas opções são as dele.
    /// </summary>
    /// <param name="agregadosDoFato">Os agregados sobre grupo repetível cujo fato de membro é este, lidos do catálogo.</param>
    public Result DefinirOpcoesDeclaradas(
        string fatoCodigo, IReadOnlyList<OpcaoDeclaradaFato> opcoes, PrecondicaoIfMatch precondicao,
        IReadOnlyCollection<string>? agregadosDoFato = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fatoCodigo);
        ArgumentNullException.ThrowIfNull(opcoes);

        string fato = fatoCodigo.Trim();
        return Editar(precondicao,
            () => RegrasDasOpcoesDeclaradas.ConferirSubstituicao(EstadoDaConfiguracao.De(this), fato, opcoes, agregadosDoFato),
            () => Substituir(_opcoesDeclaradas, o => string.Equals(o.FatoCodigo, fato, StringComparison.Ordinal), opcoes));
    }

    private readonly List<ConfiguracaoDerivacaoFato> _regrasDerivacao = [];

    /// <summary>As regras de derivação que o processo configura, por código de fato; as que a oferta de vagas define estão em <see cref="DerivacoesEfetivas"/>.</summary>
    public IReadOnlyCollection<ConfiguracaoDerivacaoFato> RegrasDerivacao => _regrasDerivacao.AsReadOnly();

    /// <summary>Todas as derivações do processo: as configuradas e as que a oferta de vagas define (<see cref="DerivacoesDaOferta"/>).</summary>
    public IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesEfetivas() =>
        EstadoDaConfiguracao.De(this).DerivacoesEfetivas();

    private readonly List<NoExigencia> _nosExigencia = [];

    /// <summary>Todos os nós da árvore de satisfação dos documentos exigidos, raízes e demais; as raízes saem em <see cref="RaizesDeExigencia"/>.</summary>
    public IReadOnlyCollection<NoExigencia> NosExigencia => _nosExigencia.AsReadOnly();

    /// <summary>As raízes das árvores de satisfação (<c>NoPaiId == null</c>).</summary>
    public IEnumerable<NoExigencia> RaizesDeExigencia => _nosExigencia.Where(static n => n.NoPaiId is null);

    /// <summary>Âncora que resolve FAIXA_ETARIA; sem ela nenhum gatilho por idade pode existir (<see cref="PendenciaDaReferenciaTemporalFatos"/>).</summary>
    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; private set; }

    public OfertaAtendimentoEspecializado? OfertaAtendimento { get; private set; }

    private readonly List<ConfiguracaoDistribuicaoVagas> _distribuicaoVagas = [];
    public IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas => _distribuicaoVagas.AsReadOnly();

    /// <summary>Bônus regional (UNI-REQ-0015); só existe quando o processo declara que o aplica (<see cref="AplicaBonusRegional"/>).</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; private set; }

    /// <summary>Declaração explícita de que o processo aplica ou não o bônus regional. Nulo é "ainda não declarado" e bloqueia a publicação; verdadeiro exige <see cref="BonusRegional"/>.</summary>
    public bool? AplicaBonusRegional { get; private set; }

    /// <summary>Quantos cursos o candidato escolhe na inscrição (1 ou 2). Nulo é "ainda não declarado" e bloqueia a publicação; não pode passar do número de ofertas da distribuição.</summary>
    public int? NumeroDeOpcoesDeCurso { get; private set; }

    /// <summary>O maior número de opções de curso que a inscrição admite.</summary>
    public const int MaximoDeOpcoesDeCurso = 2;

    /// <summary>Cascata de remanejamento das cotas reservadas (UNI-REQ-0056); ausente significa nenhuma. É uma por processo, e a cobertura por oferta é conferida em <see cref="PendenciaDaCascata"/>.</summary>
    public ConfiguracaoCascataRemanejamento? Cascata { get; private set; }

    private readonly List<CriterioDesempate> _criteriosDesempate = [];
    public IReadOnlyCollection<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    /// <summary>Classificação: compõe por referência a fórmula, a precisão, a eliminação e a ordem de alocação.</summary>
    public ConfiguracaoClassificacao? Classificacao { get; private set; }

    /// <summary>Divulgação pública do certame (UNI-REQ-0050); ausente já é o padrão minimizado (só o número de inscrição).</summary>
    public ConfiguracaoDivulgacao? ConfiguracaoDivulgacao { get; private set; }

    /// <summary>Taxa de inscrição e isenção. Nulo é "ainda não declarado" e bloqueia a publicação (<see cref="ItensEstruturaisDeConformidade"/>), ao contrário da divulgação.</summary>
    public ConfiguracaoTaxaInscricao? ConfiguracaoTaxaInscricao { get; private set; }

    /// <summary>
    /// A sessão editorial aberta sobre a configuração, portadora da retificação (ADR-0110 D3); nula
    /// quando não há retificação em curso.
    /// </summary>
    /// <remarks>
    /// É a existência dela, e não um status, que autoriza mutar um processo publicado: o
    /// <see cref="Status"/> segue <see cref="StatusProcesso.Publicado"/> e o candidato continua vendo a
    /// versão congelada vigente. Nula significa tanto "não existe" quanto "não foi carregada"; por isso
    /// a mutação carrega por <c>IProcessoSeletivoRepository.ObterParaMutacaoAsync</c>, o único que a
    /// inclui, e um teste de arquitetura prova que todo handler de mutação passa por ele.
    /// </remarks>
    public RascunhoRetificacao? Rascunho { get; private set; }

    private ProcessoSeletivo() { }

    public static ProcessoSeletivo Criar(
        string nome,
        TipoProcessoSnapshot tipoProcesso,
        OrigemCandidatos origemCandidatos,
        Guid unidadeAdministradoraOrigemId,
        UnidadeAdministradoraSnapshot unidadeAdministradora,
        LocalidadeRegente localidade,
        IdentificadorLegivel identificadorLegivel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (tipoProcesso is null)
        {
            throw new ArgumentException("Tipo do processo é obrigatório.", nameof(tipoProcesso));
        }

        if (origemCandidatos == OrigemCandidatos.Nenhuma)
        {
            throw new ArgumentException("Origem dos candidatos é obrigatória.", nameof(origemCandidatos));
        }

        if (unidadeAdministradoraOrigemId == Guid.Empty)
        {
            throw new ArgumentException("Unidade administradora é obrigatória.", nameof(unidadeAdministradoraOrigemId));
        }

        ArgumentNullException.ThrowIfNull(unidadeAdministradora);
        ArgumentNullException.ThrowIfNull(localidade);
        ArgumentException.ThrowIfNullOrWhiteSpace(identificadorLegivel.Valor, nameof(identificadorLegivel));

        return new ProcessoSeletivo
        {
            Nome = nome.Trim(),
            TipoProcesso = tipoProcesso,
            OrigemCandidatos = origemCandidatos,
            UnidadeAdministradoraOrigemId = unidadeAdministradoraOrigemId,
            UnidadeAdministradora = unidadeAdministradora,
            Localidade = localidade,
            IdentificadorLegivel = identificadorLegivel,
            Status = StatusProcesso.Rascunho,
        };
    }

    /// <summary>
    /// Substitui integralmente as etapas pontuadas do processo. O caráter e o peso definem o divisor
    /// da média (<see cref="CalcularDivisorMedia"/>); a <c>Ordem</c> é só a posição de apresentação
    /// no envelope congelado e não entra na fórmula da nota final.
    /// </summary>
    public Result DefinirEtapas(IReadOnlyList<EtapaProcesso> etapas, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return EditarConferindo(precondicao,
            () => RegrasDasEtapas.ConferirSubstituicao(EstadoDaConfiguracao.De(this), etapas),
            () => Substituir(_etapas, etapas));
    }

    /// <summary>
    /// Define (ou substitui) a oferta de atendimento especializado. Recusa a oferta que deixaria de
    /// conter um código de condição ou de recurso que uma condição viva ainda cita.
    /// </summary>
    public Result DefinirOfertaAtendimento(OfertaAtendimentoEspecializado oferta, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(oferta);

        return Editar(
            precondicao,
            () => RecusaDaOfertaDeAtendimento(oferta),
            () =>
            {
                Vincular(oferta);
                OfertaAtendimento = oferta;
            });
    }

    // Condição e recurso são citados por código, não por Guid: só recusa se o novo conjunto de
    // ofertas deixar de conter um código hoje referenciado por condição viva.
    private DomainError? RecusaDaOfertaDeAtendimento(OfertaAtendimentoEspecializado oferta)
    {
        HashSet<string> novosCodigos = [.. oferta.Condicoes.Select(c => c.CondicaoCodigo)];
        if (ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(EstadoDaConfiguracao.De(this), OfertaAtendimentoEspecializado.FatoCondicaoAtendimento, novosCodigos))
        {
            return new DomainError(
                "ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva",
                "Existe condição de gatilho documental referenciando um código de condição de atendimento que deixaria de ser ofertado — ajuste ou remova a condição antes de redefinir a oferta de atendimento.");
        }

        HashSet<string> novosRecursos = [.. oferta.Recursos.Select(static r => r.RecursoCodigo)];
        return ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(EstadoDaConfiguracao.De(this), OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade, novosRecursos)
            ? new DomainError(
                ProcessoSeletivoErrorCodes.RecursoAcessibilidadeReferenciadoPorCondicaoViva,
                "Existe condição referenciando um código de recurso de acessibilidade que deixaria de ser ofertado — ajuste ou remova a condição antes de redefinir a oferta de atendimento.")
            : null;
    }

    /// <summary>
    /// Substitui integralmente a distribuição de vagas do processo: uma <see cref="ConfiguracaoDistribuicaoVagas"/>
    /// por oferta de curso, sem repetir a mesma oferta. As invariantes de cada configuração já foram
    /// validadas em <see cref="ConfiguracaoDistribuicaoVagas.Criar"/>.
    /// </summary>
    public Result DefinirDistribuicaoVagas(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas,
        FatosDeModalidade fatosDeModalidade,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        return EditarConferindo(precondicao,
            () => RegrasDaDistribuicaoDeVagas.ConferirSubstituicao(EstadoDaConfiguracao.De(this), distribuicaoVagas, fatosDeModalidade),
            () => Substituir(_distribuicaoVagas, distribuicaoVagas));
    }

    /// <summary>
    /// Declara quantos cursos o candidato escolhe na inscrição (1 ou 2). Com a distribuição já
    /// gravada, o número não pode superar o de ofertas; sem distribuição a declaração é aceita, e a
    /// pendência é a de distribuição ausente.
    /// </summary>
    public Result DefinirOpcoesDeCurso(int numero, PrecondicaoIfMatch precondicao) =>
        EditarConferindo(precondicao,
            () => RegrasDaDistribuicaoDeVagas.ConferirOpcoesDeCurso(EstadoDaConfiguracao.De(this), numero),
            () => NumeroDeOpcoesDeCurso = numero);

    /// <summary>
    /// Declara se o processo aplica o bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015). Aplicar exige a configuração, e não
    /// aplicar a recusa: a declaração e a configuração não podem divergir.
    /// </summary>
    public Result DefinirBonusRegional(bool aplica, ConfiguracaoBonusRegional? bonus, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (aplica && bonus is null)
        {
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao,
                "O processo que aplica o bônus regional precisa da configuração do bônus."));
        }

        if (!aplica && bonus is not null)
        {
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao,
                "O processo que não aplica o bônus regional não pode ter a configuração do bônus."));
        }

        // Os campos com os municípios do bônus como opções referenciam o município pelo código
        // IBGE: trocar a área ou remover o bônus não pode deixar de fora um município citado por
        // condição viva, que nunca mais seria satisfeita.
        HashSet<string> novosMunicipios = [.. (bonus?.Municipios ?? []).Select(static m => m.CodigoIbge)];
        if (_campos
            .Where(static f => f.OrigemValores == OrigemValoresColeta.MunicipiosDoBonus)
            .Any(f => ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(EstadoDaConfiguracao.De(this), f.FatoCodigo, novosMunicipios)))
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.MunicipioDoBonusReferenciadoPorCondicaoViva",
                "Existe condição citando um município que deixaria a área do bônus regional — ajuste ou remova a condição antes de redefinir o bônus."));
        }

        Vincular(bonus);
        BonusRegional = bonus;
        AplicaBonusRegional = aplica;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define (ou remove) a divulgação pública do processo (UNI-REQ-0050). Nula, o processo volta ao
    /// padrão minimizado, que é publicável.
    /// </summary>
    public Result DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao? configuracao, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao, () =>
        {
            Vincular(configuracao);
            ConfiguracaoDivulgacao = configuracao;
        });

    /// <summary>
    /// Define (ou remove) a taxa de inscrição e os fundamentos de isenção. Nula, o processo volta a
    /// "ainda não declarado", que bloqueia a publicação, ao contrário da divulgação.
    /// </summary>
    public Result DefinirTaxaInscricao(ConfiguracaoTaxaInscricao? configuracao, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao, () =>
        {
            Vincular(configuracao);
            ConfiguracaoTaxaInscricao = configuracao;
        });

    /// <summary>
    /// Define (ou remove) a cascata de remanejamento. A forma e o vínculo com a regra versionada já
    /// foram validados pela fábrica e pelo handler; aqui só se aplica a mutação protegida pela
    /// sessão editorial.
    /// </summary>
    public Result DefinirCascataRemanejamento(ConfiguracaoCascataRemanejamento? cascata, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao, () =>
        {
            Vincular(cascata);
            Cascata = cascata;
        });

    /// <summary>
    /// Substitui integralmente os critérios de desempate. Lista vazia remove todos: o rascunho aceita,
    /// mas a publicação de processo com inscrição própria exige ao menos um. Todo <c>etapa_ref</c> de um
    /// critério por nota de etapa precisa existir entre as etapas, senão a configuração congelaria um
    /// desempate inexecutável.
    /// </summary>
    public Result DefinirCriteriosDesempate(IReadOnlyList<CriterioDesempate> criterios, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        return EditarConferindo(precondicao,
            () => RegrasDoDesempate.ConferirSubstituicao(EstadoDaConfiguracao.De(this), criterios),
            () => Substituir(_criteriosDesempate, criterios));
    }

    /// <summary>
    /// Define ou substitui a configuração de classificação. Confere o que depende de outras dimensões
    /// (a etapa de cada eliminação existe, o quadro de pesos por área cobre o desempate), porque as
    /// invariantes internas já foram validadas em <see cref="ConfiguracaoClassificacao.Criar"/>.
    /// </summary>
    public Result DefinirClassificacao(ConfiguracaoClassificacao classificacao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(classificacao);

        return Editar(precondicao,
            () => RegrasDaClassificacao.Planejar(EstadoDaConfiguracao.De(this), classificacao),
            plano =>
            {
                foreach (OpcaoDeclaradaFato edicao in plano.EdicoesDoEnemARetirar)
                {
                    _opcoesDeclaradas.Remove(edicao);
                }

                Vincular(plano.Classificacao);
                Classificacao = plano.Classificacao;
            });
    }

    /// <summary>
    /// Define ou substitui o formulário de uma finalidade: a fase em que é respondido, o título e as
    /// etapas. Sob retificação, acrescentar formulário é permitido. O formulário que nasce com
    /// <paramref name="itensDaCriacao"/> — o de inscrição, com o conjunto básico do candidato — nasce
    /// com eles, numa operação só.
    /// </summary>
    public Result DefinirFormulario(
        FinalidadeFormulario finalidade, Guid? faseId, string? titulo, IReadOnlyList<EtapaFormulario> etapas,
        PrecondicaoIfMatch precondicao, IReadOnlyList<FatoColetado>? itensDaCriacao = null)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return Editar(precondicao,
            () => RegrasDosFormularios.PlanejarDefinicao(EstadoDaConfiguracao.De(this), finalidade, faseId, titulo, etapas, itensDaCriacao),
            plano => plano.Aplicar(Colecoes(), Id));
    }

    public Result RemoverFormulario(FinalidadeFormulario finalidade, PrecondicaoIfMatch precondicao)
    {
        if (RegrasDosFormularios.RecusaDaRemocaoSobRetificacao(Rascunho) is { } recusaDaSessao)
        {
            return Result.Failure(recusaDaSessao);
        }

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Result<FormularioProcesso> remocao = RegrasDosFormularios.ConferirRemocao(EstadoDaConfiguracao.De(this), finalidade);
        if (remocao.IsFailure)
        {
            return Result.Failure(remocao.Error!);
        }

        _formularios.Remove(remocao.Value!);
        _campos.RemoveAll(f => f.Finalidade == finalidade);
        _gruposColetados.RemoveAll(g => g.Finalidade == finalidade);
        _termosExigidos.RemoveAll(t => t.Finalidade == finalidade);
        return Result.Success();
    }

    public Result AplicarModeloDeFormulario(CopiaDeModeloDeFormulario copia, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(copia);

        EstadoDaConfiguracao estado = EstadoDaConfiguracao.De(this);
        if (RegrasDosFormularios.RecusaDeAplicacaoDeModelo(estado, precondicao) is { } recusaDeEstado)
        {
            return Result.Failure(recusaDeEstado);
        }

        Result<PlanoDoModelo> plano = AplicacaoDeModeloDeFormulario.Planejar(estado, copia);
        if (plano.IsFailure)
        {
            return Result.ValidationFailure(plano.Errors);
        }

        plano.Value!.Aplicar(Colecoes(), Id);
        return Result.Success();
    }

    public Result DefinirTermosDoFormulario(
        FinalidadeFormulario finalidade, IReadOnlyList<TermoExigidoFormulario> termos, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(termos);

        return Editar(precondicao,
            () => RegrasDosFormularios.PlanejarTermos(EstadoDaConfiguracao.De(this), finalidade, termos),
            plano => plano.Aplicar(Colecoes(), Id));
    }

    /// <summary>O formulário da finalidade, quando o processo o tem.</summary>
    public FormularioProcesso? FormularioDe(FinalidadeFormulario finalidade) =>
        EstadoDaConfiguracao.De(this).FormularioDe(finalidade);

    /// <summary>
    /// Substitui integralmente o cronograma de fases do processo. O grafo de precedências é parâmetro,
    /// não navegação: o domínio não consulta a Configuração, o handler resolve o grafo vigente e o
    /// passa pronto. Valida o que só a raiz prova (referências entre fases do mesmo cronograma e
    /// contra as etapas do mesmo processo), sem exigir etapa para a fase que agrupa etapas, porque a
    /// etapa pode ser declarada depois do cronograma.
    /// </summary>
    public Result DefinirCronogramaFases(
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(precedencias);

        return Editar(precondicao,
            () => ReconciliacaoDoCronograma.Planejar(EstadoDaConfiguracao.De(this), fases, precedencias),
            plano => plano.Aplicar(Colecoes(), Id));
    }

    /// <summary>
    /// Substitui integralmente a árvore de satisfação de documentos exigidos: as folhas
    /// (<see cref="DocumentosExigidos"/>) e a árvore (<see cref="NosExigencia"/>). Confere o que só a
    /// raiz prova — a fase de cada documento é viva no cronograma do processo e a consequência de
    /// reenvio tem fase que permite complementação —, porque os invariantes da árvore já foram
    /// validados ao montá-la em <see cref="NoExigencia.CriarGrupo"/>.
    /// </summary>
    public Result DefinirDocumentosExigidos(IReadOnlyList<NoExigencia> raizes, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(raizes);

        return Editar(precondicao,
            () => RegrasDasExigencias.Planejar(EstadoDaConfiguracao.De(this), raizes),
            plano => plano.Aplicar(Colecoes(), Id));
    }

    /// <summary>
    /// Substitui os itens do formulário de uma finalidade — quais fatos ele coleta, em que ordem e sob
    /// qual pré-condição cada campo aparece — sem mexer nos dos outros formulários. Sem grupos
    /// informados, os do formulário ficam como estão; a lista vazia os remove. Editável em rascunho e
    /// sob sessão de retificação, porque os fatos coletados participam do congelamento e da restauração.
    /// </summary>
    public Result DefinirFatosColetados(
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetado> fatosColetados,
        PrecondicaoIfMatch precondicao,
        IReadOnlyList<GrupoColetado>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);

        return Editar(precondicao,
            () => RegrasDaColeta.Planejar(EstadoDaConfiguracao.De(this), finalidade, fatosColetados, grupos),
            plano => plano.Aplicar(Colecoes(), Id));
    }

    public Result RecopiarItensDoCatalogo(
        IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(fatosJaColetados);

        return RegrasDaColeta.RecopiarItensDoCatalogo(EstadoDaConfiguracao.De(this), catalogo, fatosJaColetados);
    }

    /// <summary>
    /// Refaz nas fases do cronograma a cópia de sigla e nome da unidade responsável ao congelar uma
    /// versão: o cadastro da unidade é editável depois que o cronograma foi definido, e o
    /// congelamento é da publicação. A fase sem unidade, ou cuja unidade não veio em
    /// <paramref name="vivas"/>, fica como está.
    /// </summary>
    public void RecopiarUnidadesDasFases(IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot> vivas) =>
        ReconciliacaoDoCronograma.RecopiarUnidades(_cronogramaFases, vivas);

    public Result DefinirRegrasDerivacao(IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(regrasDerivacao);

        return EditarConferindo(precondicao,
            () => RegrasDaColeta.ConferirDerivacoes(EstadoDaConfiguracao.De(this), regrasDerivacao),
            () => Substituir(_regrasDerivacao, regrasDerivacao));
    }

    /// <summary>
    /// Limpa a coleta de fatos e as regras de derivação como passo da <b>restauração fiel</b> no
    /// descarte de uma retificação (Story #986). São as duas coleções que a edição sob retificação
    /// tornou mutáveis e cuja reposição colidiria no índice único: uma sessão pode ter trocado
    /// ordens (0↔1) ou alterado pré-condições/regras, e repor as instâncias congeladas por cima
    /// das vivas colidiria no índice único de <c>Ordem</c>/código na mesma transação. As opções
    /// declaradas (issue #1619) não passam por aqui: a reposição as reconcilia por chave, sem
    /// colisão. A orquestração do descarte chama este método, faz um
    /// <c>SaveChanges</c> intermediário (os <c>DELETE</c>s saem primeiro) e só então aplica o grafo
    /// congelado (<c>INSERT</c> das instâncias reidratadas) — a reposição fiel de graça, sem
    /// reconciliação profunda dos filhos. Nenhuma identidade precisa sobreviver: são
    /// <c>EntityBase</c> puros, sem soft-delete, e o <c>Id</c> não é congelado no envelope nem
    /// referenciado por FK externa.
    /// </summary>
    public void LimparColetaEDerivacaoParaRestauracao()
    {
        _campos.Clear();
        _gruposColetados.Clear();
        _regrasDerivacao.Clear();
        _termosExigidos.Clear();
        _formularios.Clear();
    }

    /// <summary>
    /// Define a política que ancora <c>FAIXA_ETARIA</c> na publicação. Presença 0..1: nulo é estado
    /// válido enquanto nenhuma exigência tem gatilho por idade; a ausência só vira pendência de
    /// publicação quando existir esse gatilho.
    /// </summary>
    public Result DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos? referencia, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => ReferenciaTemporalDosFatos.RecusaDaFaseForaDoCronograma(EstadoDaConfiguracao.De(this), referencia),
            () => ReferenciaTemporalFatos = referencia);

    /// <summary>Declara ou troca o identificador legível; não há remoção, o certame sempre tem um.</summary>
    public Result DefinirIdentificadorLegivel(IdentificadorLegivel identificador, PrecondicaoIfMatch precondicao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador.Valor, nameof(identificador));

        return Editar(precondicao,
            () => ContagemDePrazos.RecusaDeTrocaDoIdentificador(Rascunho),
            () => IdentificadorLegivel = identificador);
    }

    /// <summary>
    /// Redeclara o município cujo calendário rege a contagem dos prazos (UNI-REQ-0111). A atribuição
    /// é incondicional de propósito: a igualdade de <see cref="LocalidadeRegente"/> considera só o
    /// código IBGE, e comparar antes descartaria a correção de um nome de exibição divergente.
    /// </summary>
    public Result DefinirLocalidade(LocalidadeRegente localidade, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => ContagemDePrazos.RecusaDaLocalidadeAusente(localidade),
            () => Localidade = localidade);

    /// <summary>
    /// Declara a convenção de contagem dos prazos que distinguem dia útil (UNI-REQ-0112). A referência
    /// chega resolvida contra o rol de regras: o <c>hash</c> ecoado do payload não provaria nada
    /// sobre a definição aplicada.
    /// </summary>
    public Result DefinirAlgoritmoContagemPrazo(ReferenciaRegra algoritmo, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => ContagemDePrazos.RecusaDaConvencaoAusente(algoritmo),
            () => AlgoritmoContagemPrazo = algoritmo);

    /// <summary>
    /// Congela em cada exigência a proteção efetiva calculada pelo contexto (a da versão vigente, quando
    /// a exigência é a mesma, ou a vigente no cadastro), antes do snapshot. A exigência que o contexto
    /// não traz mantém a cópia.
    /// </summary>
    public void AplicarProtecaoVigente(ProtecaoVigenteDasExigencias protecao) =>
        ProtecaoDasExigencias.Recongelar(_documentosExigidos, protecao);


    /// <summary>
    /// Publica o processo (UNI-REQ-0019): confere a transição e os portões de prontidão, abre a
    /// versão 1 da configuração a partir dos bytes canônicos que a Application produziu (o Domain
    /// não canonicaliza, ADR-0042) e transita o status, tudo em memória e atomicamente. O ato que
    /// cria a versão tem o id decidido aqui, mas o documento vive em <c>Publicacoes</c>
    /// (ADR-0103/0105, ADR-0108). Não recebe precondição: o rascunho não tem sessão.
    /// </summary>
    public Result<VersaoConfiguracao> Publicar(PedidoDeCongelamento pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(pedido.Dados, "dados");
        ArgumentNullException.ThrowIfNull(pedido.Documento.Canonico, "configuracaoCongeladaCanonica");
        ArgumentNullException.ThrowIfNull(pedido.Clock, "clock");
        ArgumentNullException.ThrowIfNull(pedido.Contexto, "contexto");
        ArgumentNullException.ThrowIfNull(pedido.TiposDeAtoUnicos, "tiposDeAtoUnicos");

        if (Status != StatusProcesso.Rascunho)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível publicar um processo em rascunho — status atual: {Status}."));
        }

        if (ProntidaoParaCongelar.PrimeiraPendencia(PortoesDeCongelamento(pedido)) is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        (VersaoConfiguracao versao, ProcessoPublicadoEvent publicado) = EmissaoDeVersao.Abrir(Id, pedido);
        Status = StatusProcesso.Publicado;
        AddDomainEvent(publicado);
        return Result<VersaoConfiguracao>.Success(versao);
    }

    /// <summary>
    /// Retifica de uma vez um processo publicado, sem sessão editorial (ADR-0101/0103): emite o ato
    /// que emenda o ato criador da <paramref name="versaoAtual"/> e a sucede, e a versão anterior
    /// fica intocada. Retificar é uma relação entre atos, e não um tipo. O alvo é inferido da cadeia
    /// de versões, nunca da data documental, que pode regredir sem mover o topo da cadeia.
    /// </summary>
    /// <param name="versaoAtual">A versão corrente, carregada pelo handler: <see cref="VersaoConfiguracao"/> é agregado próprio.</param>
    /// <param name="motivo">Justificativa obrigatória do ato (ADR-0101).</param>
    public Result<VersaoConfiguracao> Retificar(PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo)
    {
        // Os argumentos e o status são conferidos antes da sessão: antepor a recusa de sessão já
        // aberta faria um `dados` nulo deixar de lançar e um estado inválido deixar de acusar a transição.
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(pedido.Dados, "dados");
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(pedido.Documento.Canonico, "configuracaoCongeladaCanonica");
        ArgumentNullException.ThrowIfNull(pedido.Clock, "clock");

        if (Status != StatusProcesso.Publicado)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {Status}."));
        }

        // O atalho e a sessão retificam o mesmo ato; correndo juntos, a versão seguinte sairia da
        // configuração viva que a sessão está editando (ADR-0110 D7).
        if (Rascunho is not null)
        {
            return Result<VersaoConfiguracao>.Failure(GuardaDeEdicao.RetificacaoJaAberta());
        }

        return SucederVersao(pedido, versaoAtual, motivo);
    }

    /// <summary>
    /// Fecha a sessão editorial: congela a versão seguinte com a configuração editada e encerra a
    /// sessão, na mesma transação (ADR-0110). O motivo vem do rascunho, onde foi normalizado uma
    /// única vez. A sessão só morre depois de a versão estar decidida: se o congelamento for
    /// recusado, ela segue aberta para o administrador corrigir e tentar de novo.
    /// </summary>
    public Result<VersaoConfiguracao> FecharRetificacao(
        PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (GuardaDeEdicao.PendenciaDaSessao(Status, Rascunho, precondicao) is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        Result<VersaoConfiguracao> versao = SucederVersao(pedido, versaoAtual, Rascunho!.Motivo);
        if (versao.IsFailure)
        {
            return versao;
        }

        Rascunho = null;
        return versao;
    }

    /// <summary>
    /// <b>Descarta</b> a sessão editorial: o administrador abriu e desistiu (ADR-0110).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Este método não repõe nada — e é o ponto mais delicado da Feature.</b> Ele
    /// apenas <b>encerra</b> a sessão. A reposição da configuração congelada é da Application
    /// (o Domain não canonicaliza — ADR-0042) e tem de acontecer <b>antes</b>, via
    /// <c>IRestauradorDeConfiguracao</c>, que repõe <b>e prova</b> o round-trip byte a byte.
    /// </para>
    /// <para>
    /// <b>Encerrar a sessão sem repor é o pior desfecho possível</b>, e é preciso dizê-lo
    /// alto: enquanto a sessão existe, os seis <c>Definir*</c> escrevem <b>direto na
    /// configuração viva</b> — não há staging. Uma sessão encerrada sem reposição deixaria o
    /// certame de volta ao estado "publicado normal", servindo, em silêncio, uma configuração
    /// que <b>nunca foi publicada</b> e que diverge do documento que o publicou. Um fitness
    /// test prova que o único caller deste método restaura antes.
    /// </para>
    /// </remarks>
    public Result DescartarRetificacao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (GuardaDeEdicao.PendenciaDaSessao(Status, Rascunho, precondicao) is { } pendencia)
        {
            return Result.Failure(pendencia);
        }

        // O ESTADO INVÁLIDO É IRREPRESENTÁVEL, e não apenas desencorajado por um fitness test.
        //
        // Descartar sem repor devolveria o certame ao estado "publicado normal" servindo, em
        // silêncio, a configuração EDITADA — que nunca foi publicada. Um teste textual de
        // callers diz "ninguém faz isso hoje"; ele não diz "isso não pode acontecer". A
        // diferença aparece no dia em que um caminho novo esquecer a reposição, e o defeito é
        // dos que ninguém percebe: o status está certo, a versão congelada está intacta, e só
        // a configuração viva está mentindo.
        //
        // A prova é o próprio agregado: RestaurarConfiguracaoCongelada — que só o
        // IRestauradorDeConfiguracao chama, e que só repõe DEPOIS de provar o round-trip byte
        // a byte — carimba aqui a versão que repôs. O descarte exige esse carimbo, e exige que
        // ele seja o da versão que ESTA sessão tomou como base.
        if (_versaoRestaurada is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.DescarteSemRestauracao",
                "A sessão editorial não pode ser encerrada sem que a configuração congelada seja reposta — "
                + "encerrá-la agora deixaria o certame servindo a configuração editada como se ela tivesse sido publicada."));
        }

        if (_versaoRestaurada != Rascunho!.VersaoBaseId)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.DescarteComVersaoErrada",
                "A configuração reposta não é a da versão sobre a qual esta retificação foi aberta."));
        }

        Rascunho = null;
        _versaoRestaurada = null;
        return Result.Success();
    }

    /// <summary>
    /// A versão cuja configuração foi <b>reposta e provada</b> nesta unidade de trabalho —
    /// o carimbo que o descarte exige.
    /// </summary>
    /// <remarks>
    /// <b>Transiente de propósito.</b> Não é persistido nem mapeado: ele vive apenas dentro da
    /// transação, entre a reposição e o encerramento da sessão, e é justamente essa vida curta
    /// que o torna uma prova — um flag que sobrevivesse ao escopo autorizaria um descarte
    /// futuro com base numa restauração antiga.
    /// </remarks>
    private Guid? _versaoRestaurada;


    /// <summary>
    /// O núcleo comum do atalho atômico (<see cref="Retificar"/>) e do fechamento da sessão
    /// (<see cref="FecharRetificacao"/>): os dois congelam a mesma coisa, a configuração viva, e só
    /// diferem na origem do motivo e no destino da sessão.
    /// </summary>
    private Result<VersaoConfiguracao> SucederVersao(PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(pedido.Dados, "dados");
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(pedido.Contexto, "contexto");
        ArgumentNullException.ThrowIfNull(pedido.Documento.Canonico, "configuracaoCongeladaCanonica");
        ArgumentNullException.ThrowIfNull(pedido.Clock, "clock");
        ArgumentNullException.ThrowIfNull(pedido.TiposDeAtoUnicos, "tiposDeAtoUnicos");

        if (Status != StatusProcesso.Publicado)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {Status}."));
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.MotivoRetificacaoObrigatorio",
                "O motivo da retificação é obrigatório."));
        }

        // A cadeia de versões não atravessa certames: a numeração deriva dela.
        if (versaoAtual.ProcessoSeletivoId != Id)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "VersaoConfiguracao.VersaoAnteriorDeOutroProcesso",
                "A versão corrente informada pertence a outro Processo Seletivo."));
        }

        // O mesmo checklist vale para as duas transições que sucedem: uma versão vinculante
        // congelada com configuração incompleta é irreparável, como na publicação.
        if (ProntidaoParaCongelar.PrimeiraPendencia(PortoesDeCongelamento(pedido)) is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        (VersaoConfiguracao versao, ProcessoPublicadoEvent publicado) = EmissaoDeVersao.Suceder(Id, versaoAtual, pedido);
        AddDomainEvent(publicado);
        return Result<VersaoConfiguracao>.Success(versao);
    }

    private ProntidaoParaCongelar.Portoes PortoesDeCongelamento(PedidoDeCongelamento pedido)
    {
        EstadoDaConfiguracao estado = EstadoDaConfiguracao.De(this);
        return new(
            IdentificadorLegivel: () => ContagemDePrazos.PendenciaDoIdentificadorLegivel(estado),
            Localidade: () => ContagemDePrazos.PendenciaDaLocalidade(estado),
            AlgoritmoDeContagem: () => ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(estado),
            CalendarioVigente: () => ContagemDePrazos.PendenciaDoCalendarioVigente(estado, pedido.Contexto),
            Conformidade: () => ChecklistDeConformidade.PendenciaDeConformidade(estado),
            Cronograma: () => PendenciasDoCronograma.PendenciaDoCronograma(estado, pedido.Contexto.FusoInstitucional),
            TipoDeAtoUnico: () => TiposDeAtoDoCronograma.PendenciaDoTipoDeAtoUnico(estado, pedido.TiposDeAtoUnicos),
            ResultadoSemPapel: () => TiposDeAtoDoCronograma.PendenciaDoResultadoSemPapel(estado, pedido.TiposDeAtoUnicos),
            TipoQueCongelaDeclaradoComoResultado: () => TiposDeAtoDoCronograma.PendenciaDoTipoQueCongelaDeclaradoComoResultado(estado, pedido.TiposDeAtoUnicos),
            Cascata: () => CoberturaDaCascata.PendenciaDaCascata(estado),
            PreCanonicalizacao: () => ProntidaoParaCongelar.PendenciaPreCanonicalizacao(estado, pedido.FatosDeModalidade));
    }

    /// <summary>
    /// Repõe integralmente a configuração viva a partir do grafo reconstruído de uma
    /// <see cref="VersaoConfiguracao"/> congelada deste processo (ADR-0110 D2) — a
    /// operação que torna o descarte de uma sessão editorial <b>verificável</b>: o que
    /// volta é exatamente o que o documento publicado diz, não uma aproximação dele.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Não é um <c>Definir*</c>, e não passa pela trava de mutação pós-publicação.</b>
    /// Os <c>Definir*</c> são edição — mudam o que a configuração diz. Esta reposição
    /// é o contrário: devolve a configuração ao que a versão congelada já dizia. Por
    /// isso ela exige o processo <b>publicado</b> (só aí existe versão a restaurar) e é
    /// ancorada na própria evidência forense — <b>a versão é a credencial</b>, no mesmo
    /// desenho de <see cref="Retificar"/>, que também recebe a versão corrente e confia
    /// ao handler a escolha dela (<see cref="VersaoConfiguracao"/> é agregado próprio,
    /// ADR-0104; a raiz não conhece a cadeia).
    /// </para>
    /// <para>
    /// <b>Reposição integral, validada no estado final.</b> Não é uma sequência de
    /// <c>Definir*</c>: eles validam referências cruzadas contra o estado <i>corrente</i>
    /// (o desempate contra as etapas de agora, a classificação contra as etapas de
    /// agora) e recusariam um grafo meio-construído — a ordem em que as dimensões
    /// entrassem decidiria se a reposição passa. Aqui o grafo é validado <b>inteiro,
    /// como ele ficará</b>, e só então aplicado: uma restauração que falha não deixa o
    /// agregado meio-reposto.
    /// </para>
    /// <para>
    /// <b>Identidade (D2).</b> As etapas são reconciliadas <b>por <c>Id</c></b>: uma
    /// etapa que ainda existe na instância <i>tracked</i> é atualizada nela mesma —
    /// substituí-la por uma instância nova com o mesmo <c>Id</c> colidiria com o
    /// identity map do EF, e o <c>CreatedAt</c> original se perderia. As demais filhas
    /// são recriadas: os ids técnicos e o <c>CreatedAt</c> delas <b>não sobrevivem</b> —
    /// perda de informação <b>declarada</b> (ADR-0110 D2), não silenciosa. A auditoria
    /// com peso jurídico vive na <see cref="VersaoConfiguracao"/>, que é append-only e
    /// não é tocada aqui.
    /// </para>
    /// </remarks>
    /// <param name="versao">Versão congelada de onde o grafo foi reconstruído — deste processo, e a que o handler elegeu como vigente.</param>
    /// <param name="grafo">As dimensões reconstruídas pelo codec do envelope (ADR-0110 D1).</param>
    public Result RestaurarConfiguracaoCongelada(VersaoConfiguracao versao, GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(grafo);

        if (Status != StatusProcesso.Publicado)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.RestauracaoForaDePublicado",
                $"Só é possível restaurar a configuração congelada de um processo publicado — status atual: {Status}."));
        }

        // A cadeia de versões não atravessa certames: restaurar aqui a configuração
        // congelada de OUTRO processo sobrescreveria este com uma configuração que
        // nunca foi dele — e o envelope da próxima publicação congelaria a troca.
        if (versao.ProcessoSeletivoId != Id)
        {
            return Result.Failure(new DomainError(
                "VersaoConfiguracao.VersaoDeOutroProcesso",
                "A versão de configuração informada pertence a outro Processo Seletivo."));
        }

        if (ValidacaoDoGrafo.Validar(grafo) is { } erro)
        {
            return Result.Failure(erro);
        }

        AplicarGrafo(grafo);

        // Carimba a reposição. É o que torna o descarte sem restauração IRREPRESENTÁVEL: sem
        // este registro, DescartarRetificacao recusa. A sombra de verificação carimba a si
        // mesma e morre com o escopo — só a raiz viva leva o carimbo adiante.
        _versaoRestaurada = versao.Id;

        return Result.Success();
    }

    /// <summary>
    /// Uma <b>sombra</b> deste processo — mesma identidade, mesmo tipo, mesmo status, mas
    /// <b>sem configuração</b> e <b>fora do change tracker</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe por uma razão só: a fidelidade de uma reidratação só é demonstrável
    /// <b>recanonicalizando o agregado já reposto</b> — e fazer isso na raiz viva
    /// significaria mutá-la <b>antes</b> de saber se a reposição é boa. Uma prova que
    /// falhasse deixaria o agregado <i>tracked</i> empobrecido, e bastaria um
    /// <c>SaveChanges</c> adiante no mesmo escopo para gravar o estrago — a atomicidade
    /// dependeria da disciplina de quem chama, não do código.
    /// </para>
    /// <para>
    /// Com a sombra, a ordem se inverte: <b>prova primeiro, aplica depois</b>. A raiz viva
    /// só é tocada quando os bytes já bateram. O <c>Id</c> é o mesmo de propósito — é ele
    /// que as filhas recebem em <c>VincularProcesso</c>, e é ele que a versão congelada
    /// referencia.
    /// </para>
    /// </remarks>
    public ProcessoSeletivo SombraParaVerificacao() => new()
    {
        Id = Id,
        Nome = Nome,
        TipoProcesso = TipoProcesso,
        Status = Status,
        OrigemCandidatos = OrigemCandidatos,
        UnidadeAdministradoraOrigemId = UnidadeAdministradoraOrigemId,
        UnidadeAdministradora = UnidadeAdministradora,
        // A localidade nasce igual à do processo e é sobrescrita pela congelada quando o grafo a
        // traz. Copiá-la aqui não é redundância: a sombra é recanonicalizada, e o bloco de
        // localidade a lê — sem isso, restaurar um envelope anterior a esta fatia projetaria
        // sobre uma raiz sem município.
        Localidade = Localidade,
        // Mesma razão da localidade: a sombra é recanonicalizada, e o bloco da convenção de
        // contagem lê da raiz projetada.
        AlgoritmoContagemPrazo = AlgoritmoContagemPrazo,
    };

    /// <summary>
    /// Aplica o grafo já validado. Chamado <b>só</b> depois de <see cref="ValidacaoDoGrafo.Validar"/>
    /// devolver <see langword="null"/> — a partir daqui não há caminho de falha, e é o
    /// que garante que uma restauração recusada não altere nada (ADR-0110 D2).
    /// </summary>
    /// <summary>
    /// Os produtos que a fase VIVA recebe da fase congelada de mesma <see cref="FaseCronograma.Ordem"/>:
    /// as próprias instâncias congeladas quando as duas fases são a MESMA, e cópias com
    /// identidade nova quando não são.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O <c>Id</c> de <see cref="ProdutoDaFase"/> é congelado no envelope, e a reconciliação
    /// dentro de <see cref="FaseCronograma.AtualizarDe"/> é por <c>AtoCodigo</c>, feita
    /// já dentro de uma fase: ela não enxerga o cronograma inteiro e não tem como saber que a
    /// fase congelada caiu sobre outra fase viva. Quando isso acontece — a sessão editorial
    /// deslocou as ordens, e a reconciliação por Ordem passa a casar fases diferentes —,
    /// deixar o Id congelado atravessar produz dois estragos ao mesmo tempo: a linha do
    /// produto migra para uma fase que não é a que a congelou, e o MESMO Id pode entrar como
    /// instância nova enquanto a linha rastreada homônima sai de outra fase como órfã, o que
    /// o identity map do EF recusa — o descarte do rascunho estouraria, e o operador ficaria
    /// sem como desfazer o deslocamento.
    /// </para>
    /// <para>
    /// A identidade congelada só vale enquanto ancorada na fase que a congelou, e é por isso
    /// que ela é descartada aqui, com o produto recriado — a mesma disciplina que as bancas
    /// requeridas já têm por nunca terem Id no envelope. A âncora do prazo de recurso é a
    /// única referência que aponta para o Id de um produto, e
    /// <see cref="ReancorarRecursoNosProdutosRecriados"/> a acompanha; o caminho que PRECISA
    /// preservar o Id — a sombra de verificação, que nasce sem fase viva alguma — nunca passa
    /// por aqui, porque cai no ramo que adota a instância congelada inteira.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<ProdutoDaFase> ProdutosParaAFaseViva(FaseCronograma congelada, FaseCronograma viva) =>
        viva.Id == congelada.Id
            ? [.. congelada.Produtos]
            : [.. congelada.Produtos.Select(static p => ProdutoDaFase.Criar(p.AtoCodigo, p.Papel))];

    /// <summary>
    /// Reaponta a âncora do recurso da fase congelada para o produto recriado equivalente,
    /// quando <see cref="ProdutosParaAFaseViva"/> descartou as identidades congeladas.
    /// </summary>
    /// <remarks>
    /// Sem isto a regra de recurso restaurada ficaria apontando para um produto que a
    /// restauração acabou de descartar, e o ato publicado deixaria de encontrar o prazo que
    /// lhe corresponde. A ponte é o PAR <see cref="ProdutoDaFase.AtoCodigo"/> +
    /// <see cref="ProdutoDaFase.Papel"/>, a mesma chave que as demais reconciliações de produto
    /// usam: o ato sozinho deixou de ser único dentro da fase quando a mesma matéria passou a
    /// ser publicável como preliminar e como definitiva, e casar só por ele remapeava a âncora
    /// do preliminar para o definitivo sempre que este viesse primeiro — o recurso passaria a
    /// correr da publicação errada, e a snapshot preservaria o engano. A segunda etapa — do
    /// produto recriado para a instância rastreada que porventura sobreviva — é feita por
    /// <c>FaseCronograma.AtualizarDe</c>.
    /// </remarks>
    private static void ReancorarRecursoNosProdutosRecriados(
        FaseCronograma congelada,
        IReadOnlyList<ProdutoDaFase> produtosDaViva)
    {
        if (congelada.RegraRecurso is not { } regraRecurso
            || congelada.Produtos.FirstOrDefault(p => p.Id == regraRecurso.ProdutoAncoraId) is not { } ancoraCongelada)
        {
            return;
        }

        ProdutoDaFase? ancoraRecriada = produtosDaViva.FirstOrDefault(
            p => string.Equals(p.AtoCodigo, ancoraCongelada.AtoCodigo, StringComparison.Ordinal)
                && p.Papel == ancoraCongelada.Papel);
        if (ancoraRecriada is not null && ancoraRecriada.Id != regraRecurso.ProdutoAncoraId)
        {
            regraRecurso.RemapearAncora(ancoraRecriada.Id);
        }
    }

    private void AplicarGrafo(GrafoConfiguracao grafo)
    {
        // Reconciliação por Id (a armadilha do EF): a instância tracked é REUSADA e
        // atualizada, nunca substituída por uma instância nova com o mesmo Id — isso
        // colidiria com o identity map. Mesmo padrão de DefinirEtapasCommandHandler, e
        // é também o que preserva o CreatedAt original das etapas sobreviventes (D2).
        Dictionary<Guid, EtapaProcesso> tracked = _etapas.ToDictionary(e => e.Id);
        List<EtapaProcesso> etapas = [];
        foreach (EtapaProcesso congelada in grafo.Etapas)
        {
            if (tracked.TryGetValue(congelada.Id, out EtapaProcesso? viva))
            {
                // Repõe sem revalidar forma: o grafo veio de um envelope já provado, e a etapa
                // viva tem de voltar exatamente ao que foi congelado — incluindo a fase em que
                // ela acontece, sem a qual a etapa sobrevivente perderia o vínculo com o
                // cronograma e nenhum outro caminho a devolveria.
                //
                // Vai tudo, não só os escalares: a janela própria, a promessa de parecer, os
                // produtos, as bancas e as janelas recursais são estado da etapa como qualquer
                // outro. Repor pela metade fazia o descarte da retificação relatar sucesso com
                // a configuração viva ainda diferente do envelope publicado — exatamente o que
                // o descarte existe para desfazer.
                viva.ReporDe(congelada);
                etapas.Add(viva);
            }
            else
            {
                etapas.Add(congelada);
            }
        }

        _etapas.Clear();
        foreach (EtapaProcesso etapa in etapas)
        {
            Vincular(etapa);
            _etapas.Add(etapa);
        }

        Vincular(grafo.OfertaAtendimento);
        OfertaAtendimento = grafo.OfertaAtendimento;

        _distribuicaoVagas.Clear();
        foreach (ConfiguracaoDistribuicaoVagas configuracao in grafo.DistribuicaoVagas)
        {
            Vincular(configuracao);
            _distribuicaoVagas.Add(configuracao);
        }

        Vincular(grafo.BonusRegional);
        BonusRegional = grafo.BonusRegional;
        // Versão publicada só existe se passou pelo gate: ter ou não o bônus era uma declaração.
        AplicaBonusRegional = grafo.BonusRegional is not null;

        // Reposição incondicional: declarar o número durante a sessão de retificação e descartar
        // não pode deixar o valor editado sobreviver.
        NumeroDeOpcoesDeCurso = grafo.NumeroDeOpcoesDeCurso;

        Vincular(grafo.CascataRemanejamento);
        Cascata = grafo.CascataRemanejamento;

        _criteriosDesempate.Clear();
        foreach (CriterioDesempate criterio in grafo.CriteriosDesempate)
        {
            Vincular(criterio);
            _criteriosDesempate.Add(criterio);
        }

        Vincular(grafo.Classificacao);
        Classificacao = grafo.Classificacao;

        // Termos exigidos (UNI-REQ-0086): mesma reconciliação dos fatos coletados abaixo — reusa a
        // instância rastreada de mesma finalidade e código, e o descarte de uma sessão que editou
        // os termos chega aqui com a coleção já limpa pela LimparColetaEDerivacaoParaRestauracao.
        Dictionary<(FinalidadeFormulario, string), TermoExigidoFormulario> termosTracked =
            _termosExigidos.ToDictionary(static t => (t.Finalidade, t.Codigo));
        _termosExigidos.Clear();
        foreach (TermoExigidoFormulario congelado in grafo.TermosExigidos)
        {
            TermoExigidoFormulario termo = termosTracked.TryGetValue((congelado.Finalidade, congelado.Codigo), out TermoExigidoFormulario? vivo)
                ? vivo
                : congelado;
            Vincular(termo);
            _termosExigidos.Add(termo);
        }

        // Divulgação pública (UNI-REQ-0050, issue #563): mesmo padrão de BonusRegional/Cascata
        // acima — reatribuição direta (toggle por presença). Sem esta reposição, editar a
        // divulgação durante uma sessão de retificação e depois descartar deixaria o valor
        // editado na configuração viva, o mesmo defeito que a reposição de BonusRegional evita.
        Vincular(grafo.ConfiguracaoDivulgacao);
        ConfiguracaoDivulgacao = grafo.ConfiguracaoDivulgacao;

        // Taxa de inscrição e isenção (issue #1112): mesmo padrão de reposição de
        // BonusRegional/Divulgacao acima. Sem esta reposição, editar a taxa durante uma sessão
        // de retificação e depois descartar deixaria o valor editado na configuração viva.
        Vincular(grafo.ConfiguracaoTaxaInscricao);
        ConfiguracaoTaxaInscricao = grafo.ConfiguracaoTaxaInscricao;

        // Localidade (UNI-REQ-0111): repõe a que estava congelada, senão editar o município sob
        // sessão editorial e descartar deixaria a nova governando a contagem dos prazos. Só
        // sobrescreve quando o grafo a traz — grafos de teste anteriores a esta fatia não têm, e
        // apagar a localidade viva deixaria a raiz num estado que o domínio não admite.
        if (grafo.Localidade is { } localidadeCongelada)
        {
            Localidade = localidadeCongelada;
        }

        // Convenção de contagem (UNI-REQ-0112): reposição INCONDICIONAL, ao contrário da
        // localidade acima. Ali, null significa grafo antigo que não a carrega, e apagar
        // deixaria a raiz num estado que o domínio não admite. Aqui, null é o que a versão
        // de fato tinha quando nenhuma contagem sua distinguia dia útil — repor só quando
        // presente faria a declaração feita durante a sessão sobreviver ao descarte, que é
        // precisamente o que o descarte existe para desfazer.
        AlgoritmoContagemPrazo = grafo.AlgoritmoContagemPrazo;

        // Identificador legível: reposição incondicional, pela mesma razão da convenção acima. Uma
        // declaração feita na sessão de retificação sobre versão congelada sem identificador não
        // pode sobreviver ao descarte — o endereço público ficaria reservado sem versão publicada
        // que o sustente.
        IdentificadorLegivel = grafo.IdentificadorLegivel;

        // Cronograma de fases (Story #851): a reconciliação é por ORDEM, não por Id —
        // reusa a instância TRACKED cuja Ordem bate com a da fase congelada,
        // atualizando-a no lugar (mesmo cuidado que as etapas já tomam — ver a nota em
        // FaseCronograma.AtualizarDe). O que a sustenta é a identidade da linha, e
        // não risco de colidir em ux_fases_cronograma_processo_ordem: um índice único sem
        // filtro entra no grafo de comandos do EF Core, que emite o DELETE liberando a
        // Ordem antes do INSERT que a quer — medido em EtapaBancaPersistenciaTests, sobre o
        // índice equivalente das bancas da etapa. Recriar a fase a cada restauração é que
        // seria o estrago: trocaria Id e CreatedAt de uma fase que nunca deixou de existir.
        // O Id da fase É congelado no envelope e preservado por FaseCronograma.Reidratar; é
        // justamente por a reconciliação acima poder trocá-lo pelo da instância viva que o
        // mapa abaixo existe.
        Dictionary<int, FaseCronograma> fasesTracked = _cronogramaFases.ToDictionary(f => f.Ordem);
        List<FaseCronograma> fases = [];

        // Achado de revisão (Story #554, PR #903): a reconciliação acima troca o Id
        // CONGELADO de uma fase pelo Id da instância VIVA sempre que reusa uma fase
        // tracked na mesma Ordem. `documentosExigidos`/`referenciaTemporalFatos`
        // referenciam fases pelo Id CONGELADO (o que estava vigente quando o snapshot foi
        // produzido) — sem este mapa, restaurar um snapshot cuja sessão de rascunho trocou
        // a fase de uma Ordem deixaria essas referências apontando para um Id ausente de
        // CronogramaFases após a restauração.
        Dictionary<Guid, Guid> faseIdCongeladaParaViva = [];
        foreach (FaseCronograma congelada in grafo.CronogramaFases)
        {
            if (fasesTracked.TryGetValue(congelada.Ordem, out FaseCronograma? viva))
            {
                IReadOnlyList<ProdutoDaFase> produtosDaViva = ProdutosParaAFaseViva(congelada, viva);
                ReancorarRecursoNosProdutosRecriados(congelada, produtosDaViva);

                viva.AtualizarDe(congelada, produtosDaViva);
                fases.Add(viva);
                if (viva.Id != congelada.Id)
                {
                    faseIdCongeladaParaViva[congelada.Id] = viva.Id;
                }
            }
            else
            {
                fases.Add(congelada);
            }
        }

        _cronogramaFases.Clear();
        foreach (FaseCronograma fase in fases)
        {
            Vincular(fase);
            _cronogramaFases.Add(fase);
        }

        // Documentos exigidos (Story #554, PR #903): o bloco `documentosExigidos.exigencias`
        // do envelope agora é real (CA-09) — reconciliação por `exigenciaId` (o
        // DocumentoExigido.Id preservado por Reidratar, o segundo caso de identidade
        // congelada depois de EtapaProcesso.Id, ADR-0110 D2). Mesmo padrão de reuso da
        // instância TRACKED das demais coleções acima. `RemapearFase` corrige
        // `ExigidoNaFaseId` para o Id da fase VIVA quando a reconciliação acima trocou de
        // instância — sem efeito quando a fase reconciliada manteve o mesmo Id.
        Dictionary<Guid, DocumentoExigido> documentosExigidosTracked = _documentosExigidos.ToDictionary(d => d.Id);
        List<DocumentoExigido> documentosExigidos = [];
        foreach (DocumentoExigido congelada in grafo.DocumentosExigidos)
        {
            if (faseIdCongeladaParaViva.TryGetValue(congelada.ExigidoNaFaseId, out Guid faseVivaId))
            {
                congelada.RemapearFase(faseVivaId);
            }

            documentosExigidos.Add(documentosExigidosTracked.TryGetValue(congelada.Id, out DocumentoExigido? viva) ? viva : congelada);
        }

        _documentosExigidos.Clear();
        foreach (DocumentoExigido documento in documentosExigidos)
        {
            Vincular(documento);
            _documentosExigidos.Add(documento);
        }

        // Formulários por finalidade (UNI-REQ-0144): reconciliação pela finalidade, reusando a
        // instância rastreada com o conteúdo congelado; a fase congelada é remapeada para a viva
        // quando a reconciliação do cronograma trocou de instância, como a dos documentos acima.
        Dictionary<FinalidadeFormulario, FormularioProcesso> formulariosTracked = _formularios.ToDictionary(static f => f.Finalidade);
        _formularios.Clear();
        foreach (FormularioProcesso congelado in grafo.Formularios)
        {
            if (congelado.FaseId is { } faseCongelada && faseIdCongeladaParaViva.TryGetValue(faseCongelada, out Guid faseDoFormulario))
            {
                congelado.RemapearFase(faseDoFormulario);
            }

            FormularioProcesso formulario = congelado;
            if (formulariosTracked.TryGetValue(congelado.Finalidade, out FormularioProcesso? vivo))
            {
                vivo.Repor(congelado);
                formulario = vivo;
            }

            Vincular(formulario);
            _formularios.Add(formulario);
        }

        // Árvore de satisfação (Story #920, wrapper de árvore no envelope — Story #923):
        // reconciliação por Id, mesmo padrão de DocumentosExigidos acima. Um
        // NoExigencia.Id só sobrevive intacto entre publicações quando NADA da árvore foi
        // reeditado no meio — DefinirDocumentosExigidos substitui a árvore por INTEIRO a
        // cada chamada, mintando Guids novos, nunca atualiza um nó existente — então reusar
        // a instância TRACKED sempre que o Id bate é seguro: o conteúdo dela já É, por
        // construção, o mesmo do congelado (mesma garantia que sustenta a reconciliação
        // acima). `RemapearDocumentoExigido` corrige a navegação de folha para a instância
        // FINAL de `documentosExigidos` (viva ou congelada, o que a reconciliação acima já
        // escolheu) — sem isso, uma folha cuja exigência foi reconciliada para a instância
        // viva ficaria com `DocumentoExigido` apontando para o objeto congelado descartado.
        Dictionary<Guid, DocumentoExigido> documentosExigidosPorId = documentosExigidos.ToDictionary(d => d.Id);
        Dictionary<Guid, NoExigencia> nosExigenciaTracked = _nosExigencia.ToDictionary(n => n.Id);
        List<NoExigencia> nosExigencia = [];
        foreach (NoExigencia congelado in grafo.NosExigencia)
        {
            NoExigencia no = nosExigenciaTracked.TryGetValue(congelado.Id, out NoExigencia? vivo) ? vivo : congelado;
            if (no.Tipo == TipoNo.Folha
                && documentosExigidosPorId.TryGetValue(no.DocumentoExigidoId!.Value, out DocumentoExigido? documentoFinal))
            {
                no.RemapearDocumentoExigido(documentoFinal);
            }

            nosExigencia.Add(no);
        }

        _nosExigencia.Clear();
        foreach (NoExigencia raiz in nosExigencia.Where(static n => n.NoPaiId is null))
        {
            Vincular(raiz);
        }

        foreach (NoExigencia no in nosExigencia)
        {
            _nosExigencia.Add(no);
        }

        // Referência temporal de fatos (Story #554, PR #903, B-03): o envelope congela a
        // POLÍTICA crua (Tipo/Data/FaseId) ao lado da data já resolvida — mesmo padrão de
        // "insumo + output derivado" de distribuicao/vagas. Repor a política (não só a
        // data) é o que torna o round-trip reidratar→recanonicalizar não-tautológico:
        // ReferenciaTemporalDosFatos.ResolverDataReferenciaFatos(EstadoDaConfiguracao.De(this)) recalcula o mesmo output a partir do mesmo insumo
        // restaurado. Cada VersaoConfiguracao congela sua PRÓPRIA política — uma
        // retificação que muda a política antes de publicar não afeta o que já foi
        // congelado (B-03). O FaseId também passa pelo mesmo remapeamento de
        // `documentosExigidos` acima, pelo mesmo motivo (INICIO_FASE/FIM_FASE apontam para
        // um Id congelado que pode ter sido substituído pela fase viva).
        ReferenciaTemporalFatos = grafo.ReferenciaTemporalFatos is { FaseId: { } faseIdCongelada }
            referencia && faseIdCongeladaParaViva.TryGetValue(faseIdCongelada, out Guid faseVivaIdReferencia)
                ? referencia.ComFaseIdRemapeada(faseVivaIdReferencia)
                : grafo.ReferenciaTemporalFatos;

        // Fatos coletados e regras de derivação (Story #928, §7.4): repostos da configuração
        // congelada. A reconciliação é por chave natural (FatoCodigo / CodigoFato) reusando a
        // instância TRACKED quando o código bate — segura quando a coleção viva NÃO foi editada (o
        // conteúdo da viva JÁ É o do congelado, como na sombra de verificação, que nasce vazia, e no
        // caminho de restauração sem edição prévia). Sob edição sob retificação (Story #986), a viva
        // pode ter trocado ordens ou alterado pré-condições/regras: reusá-la NÃO restauraria o
        // congelado, e substituí-la na mesma transação colidiria no índice único de Ordem/código.
        // Por isso a orquestração do descarte chama LimparColetaEDerivacaoParaRestauracao() e FAZ UM
        // SaveChanges intermediário ANTES desta reposição: as coleções chegam aqui vazias, o ramo
        // TRACKED não é tomado, e as instâncias CONGELADAS entram como INSERT (Id novo da
        // reidratação — nenhuma identidade precisa sobreviver). A reposição fiel de graça, sem
        // reconciliação profunda dos filhos.
        Dictionary<string, FatoColetado> fatosTracked = Itens.ToDictionary(f => f.FatoCodigo, StringComparer.Ordinal);
        _campos.Clear();
        foreach (FatoColetado congelado in grafo.FatosColetados)
        {
            FatoColetado fato = fatosTracked.TryGetValue(congelado.FatoCodigo, out FatoColetado? vivo) ? vivo : congelado;
            Vincular(fato);
            _campos.Add(fato);
        }

        // Grupos repetíveis (UNI-REQ-0146): a mesma reconciliação, por finalidade e código.
        Dictionary<(FinalidadeFormulario, string), GrupoColetado> gruposTracked =
            _gruposColetados.ToDictionary(static g => (g.Finalidade, g.Codigo));
        _gruposColetados.Clear();
        foreach (GrupoColetado congelado in grafo.GruposColetados)
        {
            GrupoColetado grupo = gruposTracked.TryGetValue((congelado.Finalidade, congelado.Codigo), out GrupoColetado? vivo) ? vivo : congelado;
            Vincular(grupo);
            _gruposColetados.Add(grupo);
            _campos.AddRange(grupo.Subitens);
        }

        Dictionary<string, ConfiguracaoDerivacaoFato> derivacoesTracked =
            _regrasDerivacao.ToDictionary(c => c.CodigoFato, StringComparer.Ordinal);
        _regrasDerivacao.Clear();
        foreach (ConfiguracaoDerivacaoFato congelada in grafo.RegrasDerivacao)
        {
            ConfiguracaoDerivacaoFato config =
                derivacoesTracked.TryGetValue(congelada.CodigoFato, out ConfiguracaoDerivacaoFato? viva) ? viva : congelada;
            Vincular(config);
            _regrasDerivacao.Add(config);
        }

        // Opções declaradas (issue #1619): por fato congelado, a versão manda — a opção viva de
        // mesma chave recebe o rótulo e a ordem congelados, a viva que a versão não tem sai, e a
        // congelada que falta entra. As chaves que saem e as que entram nunca coincidem, então não
        // há colisão no índice único. Opções de fato que a versão não congela são configuração
        // viva e ficam como estão.
        foreach (IGrouping<string, OpcaoDeclaradaFato> congeladasDoFato in grafo.OpcoesDeclaradas.GroupBy(static o => o.FatoCodigo))
        {
            Dictionary<string, OpcaoDeclaradaFato> congeladas = congeladasDoFato.ToDictionary(static o => o.Codigo, StringComparer.Ordinal);
            _opcoesDeclaradas.RemoveAll(o => string.Equals(o.FatoCodigo, congeladasDoFato.Key, StringComparison.Ordinal)
                && !congeladas.ContainsKey(o.Codigo));

            foreach (OpcaoDeclaradaFato congelada in congeladas.Values)
            {
                OpcaoDeclaradaFato? viva = _opcoesDeclaradas.FirstOrDefault(o =>
                    string.Equals(o.FatoCodigo, congelada.FatoCodigo, StringComparison.Ordinal)
                    && string.Equals(o.Codigo, congelada.Codigo, StringComparison.Ordinal));
                if (viva is not null)
                {
                    viva.ReporConteudo(congelada.Rotulo, congelada.Ordem);
                    continue;
                }

                Vincular(congelada);
                _opcoesDeclaradas.Add(congelada);
            }
        }
    }

    /// <summary>
    /// Abre a <b>sessão editorial</b> sobre a configuração de um certame publicado
    /// (ADR-0110 D3) — o que destrava os seis <c>Definir*</c> sem que o certame mude de
    /// estado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>O <see cref="Status"/> não muda</b>, e é a decisão central da ADR: um certame
    /// juridicamente publicado que exibisse um status sugerindo o contrário mentiria para
    /// o candidato — e um rascunho abandonado o deixaria assim indefinidamente. O que
    /// muda é a <b>existência</b> do <see cref="Rascunho"/>.
    /// </para>
    /// <para>
    /// <b>Nada é congelado aqui.</b> Abrir não emite ato, não abre
    /// <see cref="VersaoConfiguracao"/> e não drena evento nenhum — a versão nova nasce só
    /// no fechamento. Enquanto a sessão está aberta, o que vale para o mundo continua
    /// sendo a versão congelada vigente.
    /// </para>
    /// </remarks>
    /// <param name="motivo">Justificativa do ato de retificação — normalizada e validada em <see cref="RascunhoRetificacao"/>.</param>
    /// <param name="versaoBase">A versão corrente do processo, eleita pelo handler (<see cref="VersaoConfiguracao"/> é agregado próprio — ADR-0104).</param>
    /// <param name="identificadorDaVersaoBase">
    /// O identificador legível que a versão base congelou, lido do envelope dela pelo handler (o
    /// Domain não decodifica — ADR-0042). É ele, e não o valor vivo da raiz, que decide se a sessão
    /// pode declarar o identificador.
    /// </param>
    /// <param name="fatosDasVersoesPublicadas">
    /// O que os formulários coletaram nas versões publicadas até a base, lido dos envelopes delas pelo
    /// handler. É contra isso que a sessão confere o fato de outra finalidade (UNI-REQ-0144).
    /// </param>
    /// <param name="abertoPorSub">Sub do usuário autenticado (via <c>IUserContext</c>, nunca input do command).</param>
    /// <param name="abertoEm">Instante lido do relógio injetado (ADR-0068).</param>
    public Result<RascunhoRetificacao> AbrirRetificacao(
        string motivo,
        VersaoConfiguracao versaoBase,
        IdentificadorLegivel? identificadorDaVersaoBase,
        FatosDasVersoesPublicadas fatosDasVersoesPublicadas,
        string abertoPorSub,
        DateTimeOffset abertoEm)
    {
        ArgumentNullException.ThrowIfNull(versaoBase);
        ArgumentNullException.ThrowIfNull(fatosDasVersoesPublicadas);

        if (Status != StatusProcesso.Publicado)
        {
            return Result<RascunhoRetificacao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {Status}."));
        }

        if (versaoBase.ProcessoSeletivoId != Id)
        {
            return Result<RascunhoRetificacao>.Failure(new DomainError(
                "VersaoConfiguracao.VersaoDeOutroProcesso",
                "A versão de configuração informada pertence a outro Processo Seletivo."));
        }

        if (Rascunho is not null)
        {
            return Result<RascunhoRetificacao>.Failure(GuardaDeEdicao.RetificacaoJaAberta());
        }

        Result<RascunhoRetificacao> rascunho = RascunhoRetificacao.Criar(
            Id, motivo, versaoBase, identificadorDaVersaoBase is not null, fatosDasVersoesPublicadas, abertoPorSub, abertoEm);
        if (rascunho.IsFailure)
        {
            return rascunho;
        }

        Rascunho = rascunho.Value!;
        return rascunho;
    }

    /// <summary>
    /// Altera o motivo da sessão editorial em curso. Como toda mutação sob sessão, exige a
    /// precondição e <b>incrementa a revisão</b> (D5).
    /// </summary>
    public Result AlterarMotivoRetificacao(string motivo, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        // A inexistência da sessão vem ANTES da precondição (D9): responder 412 para um
        // rascunho que não existe mandaria o cliente recarregar um ETag inexistente.
        if (Rascunho is null)
        {
            return Result.Failure(GuardaDeEdicao.RetificacaoNaoAberta());
        }

        // E daqui em diante é o MESMO guard dos seis Definir* — não uma checagem paralela.
        // Alterar o motivo é mutação como as outras, e precisa da allowlist inteira: um
        // processo que saísse de Publicado com a sessão ainda aberta continuaria aceitando
        // esta rota se ela só conferisse a precondição, e a edição escaparia por uma porta
        // que os Definir* já tinham fechado.
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Result alterado = Rascunho.AlterarMotivo(motivo);
        if (alterado.IsFailure)
        {
            return alterado;
        }

        Rascunho.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// O <c>ETag</c> forte da sessão editorial em curso, ou <see langword="null"/> quando
    /// não há sessão — é o que o cliente devolve no <c>If-Match</c> da próxima mutação.
    /// </summary>
    public string? ETagDaSessaoEditorial => Rascunho?.ETag;

    /// <summary>A guarda de mutação da configuração (<see cref="GuardaDeEdicao.MutacaoBloqueada"/>).</summary>
    public DomainError? MutacaoBloqueada(PrecondicaoIfMatch precondicao) =>
        GuardaDeEdicao.MutacaoBloqueada(Status, Rascunho, precondicao);

    /// <summary>
    /// O protocolo comum de edição: a guarda de mutação, a conferência das regras sem efeito, a
    /// aplicação e o avanço da revisão da sessão (ADR-0110 D4/D5, ADR-0125 §5). A aplicação só
    /// roda depois de a conferência passar e nunca falha.
    /// </summary>
    private Result Editar(PrecondicaoIfMatch precondicao, Func<DomainError?> conferir, Action aplicar) =>
        EditarConferindo(precondicao, () => conferir() is { } recusa ? Result.Failure(recusa) : Result.Success(), aplicar);

    private Result EditarConferindo(PrecondicaoIfMatch precondicao, Func<Result> conferir, Action aplicar)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Result conferencia = conferir();
        if (conferencia.IsFailure)
        {
            return conferencia;
        }

        aplicar();
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    private Result Editar(PrecondicaoIfMatch precondicao, Action aplicar) =>
        Editar(precondicao, static () => null, aplicar);

    private Result Editar<TPlano>(PrecondicaoIfMatch precondicao, Func<Result<TPlano>> planejar, Action<TPlano> aplicar)
    {
        TPlano? plano = default;
        return EditarConferindo(precondicao,
            () =>
            {
                Result<TPlano> planejado = planejar();
                plano = planejado.Value;
                return planejado.IsSuccess ? Result.Success() : Result.ValidationFailure(planejado.Errors);
            },
            () => aplicar(plano!));
    }

    private ColecoesDoProcesso Colecoes() => new(
        _etapas, _cronogramaFases, _documentosExigidos, _nosExigencia, _campos, _gruposColetados,
        _formularios, _termosExigidos, _opcoesDeclaradas, _regrasDerivacao, _distribuicaoVagas, _criteriosDesempate);

    private T? Vincular<T>(T? parte)
        where T : class, IParteDoProcesso
    {
        parte?.VincularProcesso(Id);
        return parte;
    }

    private void Substituir<T>(List<T> lista, IEnumerable<T> novas)
        where T : class, IParteDoProcesso =>
        PartesDoProcesso.Substituir(lista, novas, Id);

    private void Substituir<T>(List<T> lista, Predicate<T> substituidas, IEnumerable<T> novas)
        where T : class, IParteDoProcesso =>
        PartesDoProcesso.Substituir(lista, substituidas, novas, Id);
}
