namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bonus;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Agregado-raiz do certame (UNI-REQ-0014/0015): o administrador cria o processo em rascunho e monta
/// a configuração a partir dos cadastros de referência. Todas as entidades de configuração pendem
/// desta raiz e são acessadas e persistidas exclusivamente por ela.
/// </summary>
/// <remarks>
/// O documento normativo não pertence a esta raiz: é o ato publicado, em <c>Publicacoes</c> (ADR-0103/0105), que a
/// <see cref="VersaoConfiguracao"/> congelada (UNI-REQ-0019) referencia por valor, sem chave estrangeira (ADR-0061).
/// </remarks>
public sealed class ProcessoSeletivo : SoftDeletableEntity
{
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Origem do endereço público e da chave do documento no acervo; imutável depois de constar em versão publicada, porque trocá-lo mudaria o endereço de um certame já conhecido.</summary>
    public IdentificadorLegivel? IdentificadorLegivel { get; private set; }

    /// <summary>Id da origem em Configuração, sem FK cross-schema (ADR-0061).</summary>
    public Guid TipoProcessoOrigemId => TipoProcesso.OrigemId;

    /// <summary>Cópia do tipo escolhido na criação, imutável para a vida do processo.</summary>
    public TipoProcessoSnapshot TipoProcesso { get; private set; } = null!;

    public StatusProcesso Status { get; private set; }

    /// <summary>De onde vêm os candidatos; deriva o piso mínimo do cronograma e nunca ramifica por tipo de processo.</summary>
    public OrigemCandidatos OrigemCandidatos { get; private set; }

    /// <summary>Quem responde pelo certame, resolvido uma única vez na criação (ADR-0056); sem FK entre schemas.</summary>
    public Guid UnidadeAdministradoraOrigemId { get; private set; }

    /// <summary>Cópia (ADR-0061) da unidade administradora, congelada na criação.</summary>
    public UnidadeAdministradoraSnapshot UnidadeAdministradora { get; private set; } = null!;

    /// <summary>Município cujo calendário rege a contagem dos prazos (UNI-REQ-0111); não deriva da unidade administradora.</summary>
    public LocalidadeRegente Localidade { get; private set; } = null!;

    /// <summary>Convenção de contagem dos prazos em dia útil (UNI-REQ-0112); uma por processo, para duas fases não lerem diferente o que é dia útil.</summary>
    public ReferenciaRegra? AlgoritmoContagemPrazo { get; private set; }

    private readonly List<FormularioProcesso> _formularios = [];

    /// <summary>Os formulários do processo (UNI-REQ-0144).</summary>
    public IReadOnlyCollection<FormularioProcesso> Formularios => _formularios.AsReadOnly();

    private readonly List<TermoExigidoFormulario> _termosExigidos = [];

    /// <summary>Os termos que os formulários exigem, congelados por versão (UNI-REQ-0086).</summary>
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

    /// <summary>Os itens dos formulários, com a ordem e a pré-condição de cada um; a pré-condição só cita fatos anteriores.</summary>
    public IReadOnlyCollection<FatoColetado> FatosColetados => [.. Itens];

    /// <summary>Os itens dos formulários: os campos fora de grupo repetível.</summary>
    private IEnumerable<FatoColetado> Itens => EstadoDaConfiguracao.De(this).Itens;

    private readonly List<GrupoColetado> _gruposColetados = [];

    /// <summary>Os grupos repetíveis (UNI-REQ-0146), donos dos seus campos, que não entram em <see cref="FatosColetados"/>.</summary>
    public IReadOnlyCollection<GrupoColetado> GruposColetados => _gruposColetados.AsReadOnly();

    /// <summary>Os itens e os campos dos grupos: todo campo que o processo apresenta ao candidato.</summary>
    public IReadOnlyCollection<FatoColetado> Campos => _campos.AsReadOnly();

    private readonly List<OpcaoDeclaradaFato> _opcoesDeclaradas = [];

    /// <summary>As opções que o processo declara para os fatos de fonte do processo; as do atendimento especializado vêm da oferta.</summary>
    public IReadOnlyCollection<OpcaoDeclaradaFato> OpcoesDeclaradas => _opcoesDeclaradas.AsReadOnly();

    /// <summary>As opções do processo para o fato: as da oferta de atendimento, quando ela gere o fato, ou as declaradas.</summary>
    public IReadOnlyList<OpcaoDoProcesso> OpcoesDoProcesso(string fatoCodigo) =>
        RegrasDasOpcoesDeclaradas.OpcoesDoProcesso(EstadoDaConfiguracao.De(this), fatoCodigo);

    /// <summary>
    /// Substitui as opções declaradas de um fato de fonte do processo, sem deixar de fora um código que uma exigência viva ainda cita.
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
            () => RegrasDasOpcoesDeclaradas.ConferirSubstituicao(EstadoDaConfiguracao.De(this), fato, opcoes, agregadosDoFato).Com(opcoes),
            novas => SubstituirOnde(_opcoesDeclaradas, o => string.Equals(o.FatoCodigo, fato, StringComparison.Ordinal), novas));
    }

    private readonly List<ConfiguracaoDerivacaoFato> _regrasDerivacao = [];

    /// <summary>As regras de derivação configuradas no processo; as da oferta de vagas entram em <see cref="DerivacoesEfetivas"/>.</summary>
    public IReadOnlyCollection<ConfiguracaoDerivacaoFato> RegrasDerivacao => _regrasDerivacao.AsReadOnly();

    /// <summary>As derivações configuradas e as que a oferta de vagas define.</summary>
    public IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesEfetivas() =>
        EstadoDaConfiguracao.De(this).DerivacoesEfetivas();

    private readonly List<NoExigencia> _nosExigencia = [];

    /// <summary>Todos os nós da árvore de satisfação dos documentos exigidos; as raízes saem em <see cref="RaizesDeExigencia"/>.</summary>
    public IReadOnlyCollection<NoExigencia> NosExigencia => _nosExigencia.AsReadOnly();

    /// <summary>As raízes das árvores de satisfação (<c>NoPaiId == null</c>).</summary>
    public IEnumerable<NoExigencia> RaizesDeExigencia => _nosExigencia.Where(static n => n.NoPaiId is null);

    /// <summary>Âncora que resolve FAIXA_ETARIA; sem ela nenhum gatilho por idade pode existir.</summary>
    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; private set; }

    public OfertaAtendimentoEspecializado? OfertaAtendimento { get; private set; }

    private readonly List<ConfiguracaoDistribuicaoVagas> _distribuicaoVagas = [];
    public IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas => _distribuicaoVagas.AsReadOnly();

    /// <summary>Bônus regional (UNI-REQ-0015); só existe quando o processo declara que o aplica.</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; private set; }

    /// <summary>Nulo é "ainda não declarado" e bloqueia a publicação; verdadeiro exige <see cref="BonusRegional"/>.</summary>
    public bool? AplicaBonusRegional { get; private set; }

    /// <summary>Quantos cursos o candidato escolhe na inscrição; nulo bloqueia a publicação e o número não passa do de ofertas.</summary>
    public int? NumeroDeOpcoesDeCurso { get; private set; }

    /// <summary>O maior número de opções de curso que a inscrição admite.</summary>
    public const int MaximoDeOpcoesDeCurso = 2;

    /// <summary>Cascata de remanejamento das cotas (UNI-REQ-0056); uma por processo, ausente significa nenhuma.</summary>
    public ConfiguracaoCascataRemanejamento? Cascata { get; private set; }

    private readonly List<CriterioDesempate> _criteriosDesempate = [];
    public IReadOnlyCollection<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    /// <summary>Classificação: compõe por referência a fórmula, a precisão, a eliminação e a ordem de alocação.</summary>
    public ConfiguracaoClassificacao? Classificacao { get; private set; }

    /// <summary>Divulgação pública (UNI-REQ-0050); ausente é o padrão minimizado.</summary>
    public ConfiguracaoDivulgacao? ConfiguracaoDivulgacao { get; private set; }

    /// <summary>Taxa e isenção; nulo é "ainda não declarado" e bloqueia a publicação, ao contrário da divulgação.</summary>
    public ConfiguracaoTaxaInscricao? ConfiguracaoTaxaInscricao { get; private set; }

    /// <summary>
    /// A sessão editorial aberta sobre a configuração (ADR-0110 D3). É a existência dela, e não um status, que autoriza mutar um
    /// processo publicado; nula significa "não existe" ou "não foi carregada", por isso a mutação carrega por
    /// <c>IProcessoSeletivoRepository.ObterParaMutacaoAsync</c>.
    /// </summary>
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
        RegrasDaCriacao.Conferir(nome, tipoProcesso, origemCandidatos, unidadeAdministradoraOrigemId, unidadeAdministradora, localidade, identificadorLegivel);

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

    /// <summary>Substitui as etapas pontuadas. O caráter e o peso definem o divisor da média; a <c>Ordem</c> é só apresentação.</summary>
    public Result DefinirEtapas(IReadOnlyList<EtapaProcesso> etapas, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return Editar(precondicao,
            () => RegrasDasEtapas.ConferirSubstituicao(EstadoDaConfiguracao.De(this), etapas).Com(etapas),
            novas => Substituir(_etapas, novas));
    }

    /// <summary>Define a oferta de atendimento especializado, sem deixar de fora um código que uma condição viva ainda cita.</summary>
    public Result DefinirOfertaAtendimento(OfertaAtendimentoEspecializado oferta, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(oferta);

        return Editar(precondicao,
            () => RegrasDaOfertaDeAtendimento.Recusa(EstadoDaConfiguracao.De(this), oferta).Com(oferta),
            nova => OfertaAtendimento = Vincular(nova));
    }

    /// <summary>Substitui a distribuição de vagas: uma configuração por oferta de curso, sem repetir a oferta.</summary>
    public Result DefinirDistribuicaoVagas(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas,
        FatosDeModalidade fatosDeModalidade,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        return Editar(precondicao,
            () => RegrasDaDistribuicaoDeVagas.ConferirSubstituicao(EstadoDaConfiguracao.De(this), distribuicaoVagas, fatosDeModalidade).Com(distribuicaoVagas),
            novas => Substituir(_distribuicaoVagas, novas));
    }

    /// <summary>Declara quantos cursos o candidato escolhe (1 ou 2), sem superar o de ofertas já gravadas.</summary>
    public Result DefinirOpcoesDeCurso(int numero, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => RegrasDaDistribuicaoDeVagas.ConferirOpcoesDeCurso(EstadoDaConfiguracao.De(this), numero).Com(numero),
            declarado => NumeroDeOpcoesDeCurso = declarado);

    /// <summary>Declara se o processo aplica o bônus regional (UNI-REQ-0015); a declaração e a configuração não podem divergir.</summary>
    public Result DefinirBonusRegional(bool aplica, ConfiguracaoBonusRegional? bonus, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => RegrasDoBonusRegional.Recusa(EstadoDaConfiguracao.De(this), aplica, bonus).Com((aplica, bonus)),
            declaracao => (BonusRegional, AplicaBonusRegional) = (Vincular(declaracao.bonus), declaracao.aplica));

    /// <summary>Define ou remove a divulgação pública (UNI-REQ-0050); nula volta ao padrão minimizado, que é publicável.</summary>
    public Result DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao? configuracao, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao, () => PlanejamentoDaEdicao.Livre(configuracao), nova => ConfiguracaoDivulgacao = Vincular(nova));

    /// <summary>Define ou remove a taxa e os fundamentos de isenção; nula volta a "não declarado", que bloqueia a publicação.</summary>
    public Result DefinirTaxaInscricao(ConfiguracaoTaxaInscricao? configuracao, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao, () => PlanejamentoDaEdicao.Livre(configuracao), nova => ConfiguracaoTaxaInscricao = Vincular(nova));

    /// <summary>Define ou remove a cascata de remanejamento.</summary>
    public Result DefinirCascataRemanejamento(ConfiguracaoCascataRemanejamento? cascata, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao, () => PlanejamentoDaEdicao.Livre(cascata), nova => Cascata = Vincular(nova));

    /// <summary>Substitui os critérios de desempate; todo <c>etapa_ref</c> precisa existir entre as etapas.</summary>
    public Result DefinirCriteriosDesempate(IReadOnlyList<CriterioDesempate> criterios, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        return Editar(precondicao,
            () => RegrasDoDesempate.ConferirSubstituicao(EstadoDaConfiguracao.De(this), criterios).Com(criterios),
            novos => Substituir(_criteriosDesempate, novos));
    }

    /// <summary>Define a classificação, conferindo o que depende de outras dimensões (etapa de cada eliminação, quadro de pesos do desempate).</summary>
    public Result DefinirClassificacao(ConfiguracaoClassificacao classificacao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(classificacao);

        return Editar(precondicao,
            () => RegrasDaClassificacao.Planejar(EstadoDaConfiguracao.De(this), classificacao),
            plano =>
            {
                AplicarMudanca(plano.Mudanca);
                Classificacao = Vincular(plano.Classificacao);
            });
    }

    /// <summary>Define o formulário de uma finalidade; o que nasce com <paramref name="itensDaCriacao"/> nasce com eles, numa operação só.</summary>
    public Result DefinirFormulario(
        FinalidadeFormulario finalidade, Guid? faseId, string? titulo, IReadOnlyList<EtapaFormulario> etapas,
        PrecondicaoIfMatch precondicao, IReadOnlyList<FatoColetado>? itensDaCriacao = null)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return Editar(precondicao,
            () => RegrasDosFormularios.PlanejarDefinicao(EstadoDaConfiguracao.De(this), finalidade, faseId, titulo, etapas, itensDaCriacao),
            Aplicar);
    }

    public Result RemoverFormulario(FinalidadeFormulario finalidade, PrecondicaoIfMatch precondicao)
    {
        DomainError? recusa = RecusaDeOperacaoSoEmRascunho(RegrasDosFormularios.RemocaoSoEmRascunho(), precondicao);
        Result<MudancaDaConfiguracao> remocao = recusa is null
            ? RegrasDosFormularios.PlanejarRemocao(EstadoDaConfiguracao.De(this), finalidade)
            : Result<MudancaDaConfiguracao>.Failure(recusa);
        if (remocao.IsFailure)
        {
            return Result.Failure(remocao.Error!);
        }

        AplicarMudanca(remocao.Value!);
        return Result.Success();
    }

    /// <summary>A aplicação do modelo substitui o formulário inteiro e só existe em rascunho; o handler a antecipa.</summary>
    public DomainError? RecusaDeAplicarModelo(PrecondicaoIfMatch precondicao) =>
        RecusaDeOperacaoSoEmRascunho(RegrasDosFormularios.AplicacaoDeModeloSoEmRascunho(), precondicao);

    public Result AplicarModeloDeFormulario(CopiaDeModeloDeFormulario copia, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(copia);

        Result<PlanoDaCopiaDoModelo> plano = RecusaDeAplicarModelo(precondicao) is { } recusa
            ? Result<PlanoDaCopiaDoModelo>.Failure(recusa)
            : AplicacaoDeModeloDeFormulario.Planejar(EstadoDaConfiguracao.De(this), copia);
        if (plano.IsFailure)
        {
            return Result.ValidationFailure(plano.Errors);
        }

        Aplicar(plano.Value!);
        return Result.Success();
    }

    /// <summary>Operação só de rascunho: a recusa da sessão vem antes da precondição, que só esconderia o motivo.</summary>
    private DomainError? RecusaDeOperacaoSoEmRascunho(DomainError recusaSobSessao, PrecondicaoIfMatch precondicao) =>
        Rascunho is not null ? recusaSobSessao : MutacaoBloqueada(precondicao);

    public Result DefinirTermosDoFormulario(
        FinalidadeFormulario finalidade, IReadOnlyList<TermoExigidoFormulario> termos, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(termos);

        return Editar(precondicao,
            () => RegrasDosFormularios.PlanejarTermos(EstadoDaConfiguracao.De(this), finalidade, termos),
            AplicarMudanca);
    }

    /// <summary>O formulário da finalidade, quando o processo o tem.</summary>
    public FormularioProcesso? FormularioDe(FinalidadeFormulario finalidade) =>
        EstadoDaConfiguracao.De(this).FormularioDe(finalidade);

    /// <summary>
    /// Substitui o cronograma. O grafo de precedências e as fases já iniciadas são parâmetros: o domínio não consulta a
    /// Configuração nem o que já aconteceu em cada fase, e a unidade de uma fase iniciada não troca. Sem
    /// <paramref name="iniciadas"/>, nenhuma fase conta como iniciada (o rascunho nunca publicado).
    /// </summary>
    public Result DefinirCronogramaFases(
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias,
        PrecondicaoIfMatch precondicao,
        FasesIniciadas? iniciadas = null)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(precedencias);

        return Editar(precondicao,
            () => RegrasDoCronograma.Planejar(EstadoDaConfiguracao.De(this), fases, precedencias, iniciadas ?? FasesIniciadas.Nenhuma),
            Aplicar);
    }

    /// <summary>Substitui a árvore de satisfação dos documentos exigidos, conferindo a fase de cada documento no cronograma.</summary>
    public Result DefinirDocumentosExigidos(IReadOnlyList<NoExigencia> raizes, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(raizes);

        return Editar(precondicao,
            () => RegrasDasExigencias.Planejar(EstadoDaConfiguracao.De(this), raizes),
            AplicarMudanca);
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
            AplicarMudanca);
    }

    /// <summary>Refaz a cópia dos itens, a das unidades das fases e a proteção das exigências, nessa ordem, antes de canonicalizar; a recusa dos itens deixa o resto como está.</summary>
    public Result RecopiarAoCongelar(CopiasDoCongelamento copias)
    {
        ArgumentNullException.ThrowIfNull(copias);

        Result itens = RecopiarItensDoCatalogo(copias.CopiasDoCatalogo, copias.FatosJaColetados);
        if (itens.IsSuccess)
        {
            RecopiarUnidadesDasFases(copias.UnidadesVivas);
            AplicarProtecaoVigente(copias.ProtecaoVigente);
        }

        return itens;
    }

    internal Result RecopiarItensDoCatalogo(
        IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        Result<PlanoDaRecopiaDoCatalogo> plano = RegrasDaColeta.PlanejarRecopia(EstadoDaConfiguracao.De(this), catalogo, fatosJaColetados);
        if (plano.IsFailure)
        {
            return Result.ValidationFailure(plano.Errors);
        }

        plano.Value!.Aplicar();
        return Result.Success();
    }

    /// <summary>Refaz nas fases a cópia de sigla e nome da unidade responsável ao congelar a versão; a fase sem unidade em <paramref name="vivas"/> fica como está.</summary>
    internal void RecopiarUnidadesDasFases(IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot> vivas) =>
        RegrasDoCronograma.AplicarUnidadesVivas(CronogramaFases, vivas);

    public Result DefinirRegrasDerivacao(IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(regrasDerivacao);

        return Editar(precondicao,
            () => RegrasDaColeta.ConferirDerivacoes(EstadoDaConfiguracao.De(this), regrasDerivacao).Com(regrasDerivacao),
            novas => Substituir(_regrasDerivacao, novas));
    }

    /// <summary>Limpa a coleta e a derivação antes da reposição fiel do descarte, que grava os <c>DELETE</c>s antes de aplicar o grafo: repor por cima das vivas colidiria no índice único de ordem.</summary>
    public void LimparColetaEDerivacaoParaRestauracao() =>
        AplicarMudanca(RegrasDaColeta.PlanejarLimpezaParaRestauracao(EstadoDaConfiguracao.De(this)));

    /// <summary>Define a âncora de <c>FAIXA_ETARIA</c>; nula é válida enquanto nenhuma exigência tem gatilho por idade.</summary>
    public Result DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos? referencia, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => ReferenciaTemporalDosFatos.RecusaDaFaseForaDoCronograma(EstadoDaConfiguracao.De(this), referencia).Com(referencia),
            nova => ReferenciaTemporalFatos = nova);

    /// <summary>Declara ou troca o identificador legível; não há remoção, o certame sempre tem um.</summary>
    public Result DefinirIdentificadorLegivel(IdentificadorLegivel identificador, PrecondicaoIfMatch precondicao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador.Valor, nameof(identificador));

        return Editar(precondicao,
            () => ContagemDePrazos.RecusaDeTrocaDoIdentificador(Rascunho).Com(identificador),
            novo => IdentificadorLegivel = novo);
    }

    /// <summary>Redeclara a localidade (UNI-REQ-0111). A atribuição é incondicional: a igualdade considera só o código IBGE, e comparar antes descartaria a correção de um nome divergente.</summary>
    public Result DefinirLocalidade(LocalidadeRegente localidade, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => ContagemDePrazos.RecusaDaLocalidadeAusente(localidade).Com(localidade),
            nova => Localidade = nova);

    /// <summary>Declara a convenção de contagem dos prazos (UNI-REQ-0112), já resolvida contra o rol de regras.</summary>
    public Result DefinirAlgoritmoContagemPrazo(ReferenciaRegra algoritmo, PrecondicaoIfMatch precondicao) =>
        Editar(precondicao,
            () => ContagemDePrazos.RecusaDaConvencaoAusente(algoritmo).Com(algoritmo),
            nova => AlgoritmoContagemPrazo = nova);

    /// <summary>Congela em cada exigência a proteção efetiva calculada pelo contexto; a exigência que o contexto não traz mantém a cópia.</summary>
    internal void AplicarProtecaoVigente(ProtecaoVigenteDasExigencias protecao) =>
        ProtecaoDasExigencias.Planejar(EstadoDaConfiguracao.De(this), protecao).Aplicar();

    /// <summary>
    /// Publica o processo em rascunho (UNI-REQ-0019): confere a transição e os portões de prontidão, abre
    /// a versão 1 a partir dos bytes canônicos que a Application produziu (o Domain não canonicaliza,
    /// ADR-0042) e transita o status, tudo em memória e atomicamente.
    /// </summary>
    public Result<VersaoConfiguracao> Publicar(PedidoDeCongelamento pedido) =>
        AssumirVersaoEmitida(TransicoesDaVersao.Publicar(EstadoDaConfiguracao.De(this), pedido));

    /// <summary>
    /// Retifica de uma vez um processo publicado, sem sessão editorial (ADR-0101/0103): emite o ato que
    /// emenda o ato criador da <paramref name="versaoAtual"/> e a sucede, e a versão anterior fica intocada.
    /// </summary>
    /// <param name="versaoAtual">A versão corrente, carregada pelo handler: <see cref="VersaoConfiguracao"/> é agregado próprio.</param>
    /// <param name="motivo">Justificativa obrigatória do ato (ADR-0101).</param>
    public Result<VersaoConfiguracao> Retificar(PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo) =>
        AssumirVersaoEmitida(TransicoesDaVersao.Retificar(EstadoDaConfiguracao.De(this), pedido, versaoAtual, motivo));

    /// <summary>Fecha a sessão editorial: congela a versão seguinte com a configuração editada e encerra a sessão (ADR-0110).</summary>
    public Result<VersaoConfiguracao> FecharRetificacao(
        PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, PrecondicaoIfMatch precondicao) =>
        PendenciaDaSessao(precondicao) is { } pendencia
            ? Result<VersaoConfiguracao>.Failure(pendencia)
            : AssumirVersaoEmitida(TransicoesDaVersao.Fechar(EstadoDaConfiguracao.De(this), pedido, versaoAtual, Rascunho!));

    private Result<VersaoConfiguracao> AssumirVersaoEmitida(Result<VersaoEmitida> emissao)
    {
        if (emissao.IsFailure)
        {
            return Result<VersaoConfiguracao>.Failure(emissao.Error!);
        }

        Status = StatusProcesso.Publicado;
        AddDomainEvent(emissao.Value!.Evento);
        Rascunho = emissao.Value.EncerraSessao ? null : Rascunho;
        return Result<VersaoConfiguracao>.Success(emissao.Value.Versao);
    }

    /// <summary>
    /// Descarta a sessão editorial: o administrador abriu e desistiu (ADR-0110). Só encerra a sessão; a
    /// reposição da configuração congelada é da Application e tem de vir antes, por
    /// <c>IRestauradorDeConfiguracao</c>, que a carimba por <see cref="RestaurarConfiguracaoCongelada"/>.
    /// </summary>
    public Result DescartarRetificacao(PrecondicaoIfMatch precondicao)
    {
        if ((PendenciaDaSessao(precondicao) ?? SessaoDeRetificacao.RecusaDoDescarte(Rascunho!, _versaoRestaurada)) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        (Rascunho, _versaoRestaurada) = (null, null);
        return Result.Success();
    }

    /// <summary>
    /// O carimbo da versão cuja configuração foi reposta e provada nesta unidade de trabalho, que o descarte exige.
    /// Transiente de propósito: um carimbo que sobrevivesse ao escopo autorizaria um descarte com base numa restauração antiga.
    /// </summary>
    private Guid? _versaoRestaurada;

    /// <summary>
    /// Repõe integralmente a configuração viva a partir do grafo de uma <see cref="VersaoConfiguracao"/>
    /// congelada deste processo (ADR-0110 D2) e carimba a versão reposta, que o descarte exige. Não é uma
    /// edição e não passa pela trava de mutação pós-publicação: devolve a configuração ao que a versão já dizia.
    /// </summary>
    /// <param name="versao">Versão congelada de onde o grafo foi reconstruído: deste processo, a que o handler elegeu como vigente.</param>
    /// <param name="grafo">As dimensões reconstruídas pelo codec do envelope (ADR-0110 D1).</param>
    public Result RestaurarConfiguracaoCongelada(VersaoConfiguracao versao, GrafoConfiguracao grafo)
    {
        if (SessaoDeRetificacao.RecusaDaRestauracao(EstadoDaConfiguracao.De(this), versao, grafo) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        AplicarGrafo(grafo);
        _versaoRestaurada = versao.Id;
        return Result.Success();
    }

    /// <summary>
    /// Uma sombra deste processo: mesma identidade, tipo e status, sem configuração e fora do change
    /// tracker. A fidelidade de uma reidratação só se demonstra recanonicalizando o agregado já reposto, e
    /// fazer isso na raiz viva a mutaria antes de saber se a reposição é boa. A localidade e a convenção de
    /// contagem são copiadas porque o envelope as lê da raiz projetada.
    /// </summary>
    public ProcessoSeletivo SombraParaVerificacao() => new()
    {
        Id = Id,
        Nome = Nome,
        TipoProcesso = TipoProcesso,
        Status = Status,
        OrigemCandidatos = OrigemCandidatos,
        UnidadeAdministradoraOrigemId = UnidadeAdministradoraOrigemId,
        UnidadeAdministradora = UnidadeAdministradora,
        Localidade = Localidade,
        AlgoritmoContagemPrazo = AlgoritmoContagemPrazo,
    };

    /// <summary>
    /// Repõe a configuração congelada, já validada por <see cref="ValidacaoDoGrafo.Validar"/>: depois
    /// da validação não há caminho de falha, e é isso que faz a restauração recusada não alterar nada
    /// (ADR-0110 D2).
    /// </summary>
    private void AplicarGrafo(GrafoConfiguracao grafo)
    {
        ReposicaoRealizada reposicao = ReposicaoDaConfiguracao.Repor(EstadoDaConfiguracao.De(this), grafo);
        AplicarMudanca(reposicao.Mudanca);
        AplicarEscalares(grafo, reposicao.ReferenciaTemporalFatos);
    }

    /// <summary>A localidade é a única reposição condicional: o grafo que não a traz (anterior à declaração) não apaga a viva.</summary>
    private void AplicarEscalares(GrafoConfiguracao grafo, ReferenciaTemporalFatos? referenciaTemporal)
    {
        OfertaAtendimento = Vincular(grafo.OfertaAtendimento);
        BonusRegional = Vincular(grafo.BonusRegional);
        AplicaBonusRegional = grafo.BonusRegional is not null;
        NumeroDeOpcoesDeCurso = grafo.NumeroDeOpcoesDeCurso;
        Cascata = Vincular(grafo.CascataRemanejamento);
        Classificacao = Vincular(grafo.Classificacao);
        ConfiguracaoDivulgacao = Vincular(grafo.ConfiguracaoDivulgacao);
        ConfiguracaoTaxaInscricao = Vincular(grafo.ConfiguracaoTaxaInscricao);
        Localidade = grafo.Localidade ?? Localidade;
        AlgoritmoContagemPrazo = grafo.AlgoritmoContagemPrazo;
        IdentificadorLegivel = grafo.IdentificadorLegivel;
        ReferenciaTemporalFatos = referenciaTemporal;
    }

    /// <summary>Abre a sessão editorial sobre a configuração de um certame publicado (ADR-0110 D3); o <see cref="Status"/> não muda.</summary>
    public Result<RascunhoRetificacao> AbrirRetificacao(
        string motivo,
        VersaoConfiguracao versaoBase,
        IdentificadorLegivel? identificadorDaVersaoBase,
        FatosDasVersoesPublicadas fatosDasVersoesPublicadas,
        string abertoPorSub,
        DateTimeOffset abertoEm)
    {
        Result<RascunhoRetificacao> sessao = SessaoDeRetificacao.Abrir(
            EstadoDaConfiguracao.De(this), motivo, versaoBase, identificadorDaVersaoBase, fatosDasVersoesPublicadas, abertoPorSub, abertoEm);
        Rascunho = sessao.Value ?? Rascunho;
        return sessao;
    }

    /// <summary>Altera o motivo da sessão em curso. Como toda mutação sob sessão, exige a precondição e incrementa a revisão.</summary>
    public Result AlterarMotivoRetificacao(string motivo, PrecondicaoIfMatch precondicao)
    {
        if (PendenciaDaSessao(precondicao) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        Result alterado = Rascunho!.AlterarMotivo(motivo);
        if (alterado.IsSuccess)
        {
            Rascunho.IncrementarRevisao();
        }

        return alterado;
    }

    /// <summary>
    /// O <c>ETag</c> forte da sessão editorial em curso, ou <see langword="null"/> quando
    /// não há sessão — é o que o cliente devolve no <c>If-Match</c> da próxima mutação.
    /// </summary>
    public string? ETagDaSessaoEditorial => Rascunho?.ETag;

    /// <summary>
    /// O que impede mutar a configuração agora, ou nulo (ADR-0110 D4/D5/D9). A precondição só é exigida
    /// sob sessão, porque em rascunho não há ETag a fornecer. Os handlers a antecipam antes de validar o
    /// payload, porque a precondição precede a regra de negócio; cada operação a repete por dentro.
    /// </summary>
    public DomainError? MutacaoBloqueada(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        return Rascunho is { } sessao
            ? Status.RecusaDeEdicaoSobSessao() ?? sessao.ConferirPrecondicao(precondicao)
            : Status.RecusaDeEdicaoSemSessao();
    }

    /// <summary>
    /// O que impede encerrar ou alterar a sessão: a sessão inexistente (409) precede a precondição
    /// (428/412), porque responder 412 para um rascunho que não existe mandaria o cliente recarregar um ETag inexistente.
    /// </summary>
    internal DomainError? PendenciaDaSessao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        return Rascunho is null ? RascunhoRetificacao.NaoAberta() : MutacaoBloqueada(precondicao);
    }

    /// <summary>
    /// O protocolo de edição (ADR-0110 D4/D5, ADR-0125 §5): a guarda de mutação, o plano calculado sem
    /// efeito, a aplicação do plano e o avanço da revisão da sessão. A aplicação só roda com o plano
    /// aprovado e nunca falha.
    /// </summary>
    private Result Editar<TPlano>(PrecondicaoIfMatch precondicao, Func<Result<TPlano>> planejar, Action<TPlano> aplicar)
    {
        Result<TPlano> plano = MutacaoBloqueada(precondicao) is { } bloqueio ? Result<TPlano>.Failure(bloqueio) : planejar();
        if (plano.IsSuccess)
        {
            aplicar(plano.Value!);
            Rascunho?.IncrementarRevisao();
        }

        return plano.IsSuccess ? Result.Success() : Result.ValidationFailure(plano.Errors);
    }

    private void Aplicar(IPlanoDaEdicao plano)
    {
        plano.AtualizarPartes();
        AplicarMudanca(plano.Mudanca);
    }

    private void AplicarMudanca(MudancaDaConfiguracao mudanca)
    {
        Trocar(_etapas, mudanca.Etapas);
        Trocar(_cronogramaFases, mudanca.CronogramaFases);
        Trocar(_documentosExigidos, mudanca.DocumentosExigidos);
        Trocar(_nosExigencia, mudanca.NosExigencia);
        Trocar(_campos, mudanca.Campos);
        Trocar(_gruposColetados, mudanca.GruposColetados);
        Trocar(_formularios, mudanca.Formularios);
        Trocar(_termosExigidos, mudanca.TermosExigidos);
        Trocar(_opcoesDeclaradas, mudanca.OpcoesDeclaradas);
        Trocar(_regrasDerivacao, mudanca.RegrasDerivacao);
        Trocar(_distribuicaoVagas, mudanca.DistribuicaoVagas);
        Trocar(_criteriosDesempate, mudanca.CriteriosDesempate);
    }

    private void Trocar<T>(List<T> lista, Troca<T> troca)
        where T : class, IParteDoProcesso
    {
        lista.RemoveAll(troca.Saem.Contains);
        foreach (T parte in troca.Entram)
        {
            parte.VincularProcesso(Id);
            lista.Add(parte);
        }
    }

    private T? Vincular<T>(T? parte)
        where T : class, IParteDoProcesso
    {
        parte?.VincularProcesso(Id);
        return parte;
    }

    private void Substituir<T>(List<T> lista, IEnumerable<T> novas)
        where T : class, IParteDoProcesso =>
        Trocar(lista, Troca.Substituindo(lista, novas));

    private void SubstituirOnde<T>(List<T> lista, Predicate<T> saem, IEnumerable<T> novas)
        where T : class, IParteDoProcesso =>
        Trocar(lista, Troca.Substituindo(lista.FindAll(saem), novas));
}
