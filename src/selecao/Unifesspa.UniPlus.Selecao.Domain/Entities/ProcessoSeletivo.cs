namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Events;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bonus;
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

    /// <summary>Âncora que resolve FAIXA_ETARIA; sem ela nenhum gatilho por idade pode existir (<see cref="ReferenciaTemporalDosFatos.PendenciaDaReferenciaTemporalFatos"/>).</summary>
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

    /// <summary>Cascata de remanejamento das cotas reservadas (UNI-REQ-0056); ausente significa nenhuma. É uma por processo, e a cobertura por oferta é conferida em <see cref="CoberturaDaCascata.PendenciaDaCascata"/>.</summary>
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
    /// da média (<see cref="EstadoDaConfiguracao.CalcularDivisorMedia"/>); a <c>Ordem</c> é só a posição de apresentação
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
            () => RegrasDaOfertaDeAtendimento.Recusa(EstadoDaConfiguracao.De(this), oferta),
            () =>
            {
                Vincular(oferta);
                OfertaAtendimento = oferta;
            });
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
    public Result DefinirBonusRegional(bool aplica, ConfiguracaoBonusRegional? bonus, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => RegrasDoBonusRegional.Recusa(EstadoDaConfiguracao.De(this), aplica, bonus),
            () =>
            {
                BonusRegional = Vincular(bonus);
                AplicaBonusRegional = aplica;
            });

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
        DomainError? recusa = RegrasDosFormularios.RecusaDaRemocaoSobRetificacao(Rascunho) ?? MutacaoBloqueada(precondicao);
        Result<FormularioProcesso> remocao = recusa is null
            ? RegrasDosFormularios.ConferirRemocao(EstadoDaConfiguracao.De(this), finalidade)
            : Result<FormularioProcesso>.Failure(recusa);
        if (remocao.IsFailure)
        {
            return Result.Failure(remocao.Error!);
        }

        RegrasDosFormularios.Remover(Colecoes(), remocao.Value!);
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

        Result<PlanoDaRecopiaDoCatalogo> plano = RegrasDaColeta.PlanejarRecopia(EstadoDaConfiguracao.De(this), catalogo, fatosJaColetados);
        if (plano.IsFailure)
        {
            return Result.ValidationFailure(plano.Errors);
        }

        plano.Value!.Aplicar();
        return Result.Success();
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
    /// descarte de uma retificação. São as duas coleções que a edição sob retificação
    /// tornou mutáveis e cuja reposição colidiria no índice único: uma sessão pode ter trocado
    /// ordens (0↔1) ou alterado pré-condições/regras, e repor as instâncias congeladas por cima
    /// das vivas colidiria no índice único de <c>Ordem</c>/código na mesma transação. As opções
    /// declaradas não passam por aqui: a reposição as reconcilia por chave, sem
    /// colisão. A orquestração do descarte chama este método, faz um
    /// <c>SaveChanges</c> intermediário (os <c>DELETE</c>s saem primeiro) e só então aplica o grafo
    /// congelado (<c>INSERT</c> das instâncias reidratadas) — a reposição fiel de graça, sem
    /// reconciliação profunda dos filhos. Nenhuma identidade precisa sobreviver: são
    /// <c>EntityBase</c> puros, sem soft-delete, e o <c>Id</c> não é congelado no envelope nem
    /// referenciado por FK externa.
    /// </summary>
    public void LimparColetaEDerivacaoParaRestauracao() =>
        RegrasDaColeta.LimparParaRestauracao(Colecoes());

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
    /// Descarta a sessão editorial: o administrador abriu e desistiu (ADR-0110).
    /// </summary>
    /// <remarks>
    /// Este método só encerra a sessão; não repõe nada. A reposição da configuração congelada é da
    /// Application (o Domain não canonicaliza, ADR-0042) e tem de vir antes, por
    /// <c>IRestauradorDeConfiguracao</c>, que repõe e prova o round-trip byte a byte. Encerrar sem repor
    /// deixaria o certame servindo, em silêncio, uma configuração editada que nunca foi publicada.
    /// </remarks>
    public Result DescartarRetificacao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (GuardaDeEdicao.PendenciaDaSessao(Status, Rascunho, precondicao) is { } pendencia)
        {
            return Result.Failure(pendencia);
        }

        // Um teste textual de callers só diz que ninguém descarta sem repor; o carimbo torna isso
        // irrepresentável. RestaurarConfiguracaoCongelada, que só repõe depois de provar o round-trip,
        // carimba a versão que repôs, e o descarte exige o carimbo da versão-base desta sessão.
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
    /// A versão cuja configuração foi reposta e provada nesta unidade de trabalho: o carimbo que o
    /// descarte exige.
    /// </summary>
    /// <remarks>
    /// Transiente de propósito: não é persistido nem mapeado. Um carimbo que sobrevivesse ao escopo
    /// autorizaria um descarte futuro com base numa restauração antiga.
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
    /// <see cref="VersaoConfiguracao"/> congelada deste processo (ADR-0110 D2): o descarte de uma sessão
    /// editorial só é verificável se o que volta é exatamente o que o documento publicado diz.
    /// </summary>
    /// <remarks>
    /// Não é um <c>Definir*</c> e não passa pela trava de mutação pós-publicação: devolve a configuração
    /// ao que a versão já dizia. Exige o processo publicado, e a versão é a credencial (a raiz não conhece
    /// a cadeia; o handler escolhe a vigente).
    /// <para>
    /// A reposição é integral e validada no estado final. Uma sequência de <c>Definir*</c> validaria
    /// referências cruzadas contra o estado corrente e recusaria um grafo meio-construído, de modo que a
    /// ordem das dimensões decidiria o resultado. Validado inteiro e só então aplicado, uma restauração
    /// que falha não deixa o agregado meio-reposto.
    /// </para>
    /// <para>
    /// As etapas são reconciliadas por <c>Id</c>: substituir a instância rastreada por outra de mesmo
    /// <c>Id</c> colidiria com o identity map do EF e perderia o <c>CreatedAt</c>. As demais filhas são
    /// recriadas e perdem ids técnicos e <c>CreatedAt</c>; a auditoria com peso jurídico vive na versão
    /// append-only, que não é tocada aqui.
    /// </para>
    /// </remarks>
    /// <param name="versao">Versão congelada de onde o grafo foi reconstruído: deste processo, a que o handler elegeu como vigente.</param>
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
    /// Uma sombra deste processo: mesma identidade, tipo e status, mas sem configuração e fora do change
    /// tracker.
    /// </summary>
    /// <remarks>
    /// A fidelidade de uma reidratação só se demonstra recanonicalizando o agregado já reposto, e fazer
    /// isso na raiz viva a mutaria antes de saber se a reposição é boa; uma prova que falhasse deixaria o
    /// agregado rastreado empobrecido, pronto para um <c>SaveChanges</c> gravar o estrago. Com a sombra,
    /// prova-se primeiro e aplica-se depois. O <c>Id</c> é o mesmo porque as filhas o recebem em
    /// <c>VincularProcesso</c> e a versão congelada o referencia.
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
    /// Repõe a configuração congelada, já validada por <see cref="ValidacaoDoGrafo.Validar"/>: depois
    /// da validação não há caminho de falha, e é isso que faz a restauração recusada não alterar nada
    /// (ADR-0110 D2).
    /// </summary>
    private void AplicarGrafo(GrafoConfiguracao grafo) =>
        AplicarEscalares(ReposicaoDaConfiguracao.Repor(Colecoes(), Id, grafo));

    private void AplicarEscalares(ReposicaoDosEscalares reposicao)
    {
        OfertaAtendimento = Vincular(reposicao.OfertaAtendimento);
        BonusRegional = Vincular(reposicao.BonusRegional);
        AplicaBonusRegional = reposicao.BonusRegional is not null;
        NumeroDeOpcoesDeCurso = reposicao.NumeroDeOpcoesDeCurso;
        Cascata = Vincular(reposicao.Cascata);
        Classificacao = Vincular(reposicao.Classificacao);
        ConfiguracaoDivulgacao = Vincular(reposicao.ConfiguracaoDivulgacao);
        ConfiguracaoTaxaInscricao = Vincular(reposicao.ConfiguracaoTaxaInscricao);
        Localidade = reposicao.Localidade ?? Localidade;
        AlgoritmoContagemPrazo = reposicao.AlgoritmoContagemPrazo;
        IdentificadorLegivel = reposicao.IdentificadorLegivel;
        ReferenciaTemporalFatos = reposicao.ReferenciaTemporalFatos;
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
