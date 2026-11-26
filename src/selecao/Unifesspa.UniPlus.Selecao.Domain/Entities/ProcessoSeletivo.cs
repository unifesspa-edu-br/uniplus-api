namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fada;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
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

    public SituacaoDoProcessoSeletivo Situacao { get; private set; }

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
    private IEnumerable<FatoColetado> Itens => Declaracao().Coleta.Itens;

    private readonly List<GrupoColetado> _gruposColetados = [];

    /// <summary>Os grupos repetíveis (UNI-REQ-0146), donos dos seus campos, que não entram em <see cref="FatosColetados"/>.</summary>
    public IReadOnlyCollection<GrupoColetado> GruposColetados => _gruposColetados.AsReadOnly();

    /// <summary>Os itens e os campos dos grupos: todo campo que o processo apresenta ao candidato.</summary>
    public IReadOnlyCollection<FatoColetado> Campos => _campos.AsReadOnly();

    private readonly List<OpcaoDeclaradaFato> _opcoesDeclaradas = [];

    /// <summary>As opções que o processo declara para os fatos de fonte do processo; as do atendimento especializado vêm dele.</summary>
    public IReadOnlyCollection<OpcaoDeclaradaFato> OpcoesDeclaradas => _opcoesDeclaradas.AsReadOnly();

    /// <summary>As opções do processo para o fato: as do atendimento especializado, quando ele gere o fato, ou as declaradas.</summary>
    public IReadOnlyList<OpcaoDoProcesso> OpcoesDoProcesso(string fatoCodigo) =>
        RegrasDasOpcoesDeclaradas.OpcoesDoProcesso(Declaracao(), fatoCodigo);

    /// <summary>
    /// Substitui as opções declaradas de um fato de fonte do processo, sem deixar de fora um código que uma exigência viva ainda cita.
    /// </summary>
    /// <param name="agregadosDoFato">Os agregados sobre grupo repetível cujo fato de membro é este, lidos do catálogo.</param>
    public Result DefinirOpcoesDeclaradas(
        string fatoCodigo, IReadOnlyList<OpcaoDeclaradaFato> opcoes, IReadOnlyCollection<string>? agregadosDoFato = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fatoCodigo);
        ArgumentNullException.ThrowIfNull(opcoes);

        return Editar(
            () => RegrasDasOpcoesDeclaradas.ConferirSubstituicao(Declaracao(), fatoCodigo, opcoes, agregadosDoFato).Com(opcoes),
            novas => SubstituirOnde(_opcoesDeclaradas, o => string.Equals(o.FatoCodigo, fatoCodigo, StringComparison.Ordinal), novas));
    }

    private readonly List<ConfiguracaoDerivacaoFato> _regrasDerivacao = [];

    /// <summary>As regras de derivação configuradas no processo; as da oferta de vagas entram em <see cref="DerivacoesEfetivas"/>.</summary>
    public IReadOnlyCollection<ConfiguracaoDerivacaoFato> RegrasDerivacao => _regrasDerivacao.AsReadOnly();

    /// <summary>As derivações configuradas e as que a oferta de vagas define.</summary>
    public IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesEfetivas() =>
        Declaracao().DerivacoesEfetivas();

    private readonly List<RegraDeComprovacao> _regrasDeComprovacao = [];

    /// <summary>Todas as regras de comprovação configuradas para o processo.</summary>
    public IReadOnlyCollection<RegraDeComprovacao> RegrasDeComprovacao => _regrasDeComprovacao.AsReadOnly();

    /// <summary>Regras de comprovação que não pertencem a um grupo.</summary>
    public IEnumerable<RegraDeComprovacao> RegrasDeComprovacaoSemGrupo => _regrasDeComprovacao.Where(static n => n.RegraQueContemId is null);

    /// <summary>Âncora que resolve FAIXA_ETARIA; sem ela nenhum gatilho por idade pode existir.</summary>
    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; private set; }

    public AtendimentoEspecializado? AtendimentoEspecializado { get; private set; }

    private readonly List<ConfiguracaoDistribuicaoVagas> _distribuicaoVagas = [];
    public IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas => _distribuicaoVagas.AsReadOnly();

    /// <summary>Fator de Atenuação das Desigualdades de Acesso (FADA) (UNI-REQ-0015); só existe quando o processo declara que o aplica.</summary>
    public ConfiguracaoDoFada? Fada { get; private set; }

    public DeclaracaoDaProvaPresencial? ProvaPresencial { get; private set; }

    private readonly List<CadastroDeReservaDaChamada> _cadastroDeReserva = [];

    /// <summary>O cadastro de reserva por chamada (UNI-REQ-0354, UNI-REQ-0355); sem declaração, o processo não tem reserva em nenhuma chamada.</summary>
    public CadastroDeReservaPorChamada CadastroDeReserva => CadastroDeReservaPorChamada.DeItensConferidos(_cadastroDeReserva);

    /// <summary>Como o edital prevê a manifestação de interesse depois da 1ª chamada (UNI-REQ-0503); nulo é a manifestação não prevista, que não impede a publicação.</summary>
    public ManifestacaoDeInteresse? ManifestacaoDeInteresse { get; private set; }

    /// <summary>Nulo é "ainda não declarado" e bloqueia a publicação; verdadeiro exige <see cref="Fada"/>.</summary>
    public bool? AplicaFada { get; private set; }

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

    private ProcessoSeletivo() { }

    /// <summary>
    /// A configuração como está agora, com cópias das coleções: uma mutação posterior da raiz não altera a declaração tirada.
    /// </summary>
    public ConfiguracaoDeclarada Declaracao() => Declaracao([]);

    public ConfiguracaoDeclarada Declaracao(IReadOnlyList<MotivoDeDecisaoDeclarado> motivos) => new(
        new IdentidadeDoProcessoDeclarada(Nome, OrigemCandidatos, IdentificadorLegivel, TipoProcesso, UnidadeAdministradora),
        new CronogramaDeclarado(Localidade, AlgoritmoContagemPrazo, [.. OrdemCanonicaDaConfiguracao.Fases(_cronogramaFases)]),
        new AvaliacaoDeclarada([.. OrdemCanonicaDaConfiguracao.Etapas(_etapas)], [.. _criteriosDesempate], Classificacao),
        new VagasDeclaradas([.. _distribuicaoVagas], NumeroDeOpcoesDeCurso, AplicaFada, Fada, Cascata, AtendimentoEspecializado),
        new ExigenciasDeclaradas([.. _documentosExigidos], [.. _regrasDeComprovacao]),
        new ColetaDeclarada([.. _campos], [.. _gruposColetados], [.. _formularios], [.. _termosExigidos], [.. _opcoesDeclaradas], [.. _regrasDerivacao], ReferenciaTemporalFatos),
        new DivulgacaoETaxaDeclaradas(ConfiguracaoDivulgacao, ConfiguracaoTaxaInscricao))
    { ProvaPresencial = ProvaPresencial, MotivosDeDecisao = [.. motivos] };

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
            Situacao = SituacaoDoProcessoSeletivo.Rascunho,
        };
    }

    /// <summary>Substitui as etapas pontuadas. O caráter e o peso definem o divisor da média; a <c>Ordem</c> é só apresentação.</summary>
    public Result DefinirEtapas(IReadOnlyList<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return Editar(
            () => RegrasDasEtapas.ConferirSubstituicao(Declaracao(), etapas).Com(etapas),
            novas => Substituir(_etapas, novas));
    }

    /// <summary>Define o atendimento especializado, sem deixar de fora um código que uma condição viva ainda cita.</summary>
    public Result DefinirAtendimentoEspecializado(AtendimentoEspecializado atendimento)
    {
        ArgumentNullException.ThrowIfNull(atendimento);

        return Editar(
            () => RegrasDoAtendimentoEspecializado.Recusa(Declaracao(), atendimento).Com(atendimento),
            nova => AtendimentoEspecializado = Vincular(nova));
    }

    /// <summary>Substitui a distribuição de vagas: uma configuração por oferta de curso, sem repetir a oferta.</summary>
    public Result DefinirDistribuicaoVagas(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas,
        FatosDeModalidade fatosDeModalidade)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        return Editar(
            () => RegrasDaDistribuicaoDeVagas.ConferirSubstituicao(Declaracao(), distribuicaoVagas, fatosDeModalidade).Com(distribuicaoVagas),
            novas => Substituir(_distribuicaoVagas, novas));
    }

    /// <summary>Declara quantos cursos o candidato escolhe (1 ou 2), sem superar o de ofertas já gravadas.</summary>
    public Result DefinirOpcoesDeCurso(int numero) =>
        Editar(
            () => RegrasDaDistribuicaoDeVagas.ConferirOpcoesDeCurso(Declaracao(), numero).Com(numero),
            declarado => NumeroDeOpcoesDeCurso = declarado);

    /// <summary>Declara se o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA) (UNI-REQ-0015); a declaração e a configuração não podem divergir.</summary>
    public Result DefinirFada(bool aplica, ConfiguracaoDoFada? configuracao) =>
        Editar(
            () => RegrasDoFada.Recusa(Declaracao(), aplica, configuracao).Com((aplica, configuracao)),
            declaracao => (Fada, AplicaFada) = (Vincular(declaracao.configuracao), declaracao.aplica));

    public Result DefinirProvaPresencial(bool temProvaPresencial, IReadOnlyList<MunicipioDeProvaInformado> municipios, int limiteDeMunicipios) =>
        Editar(() => DeclaracaoDaProvaPresencial.Declarar(temProvaPresencial, municipios, limiteDeMunicipios),
            declaracao => ProvaPresencial = declaracao);

    /// <summary>Substitui o cadastro de reserva por chamada inteiro; a lista vazia deixa o processo sem reserva em nenhuma chamada.</summary>
    public Result DefinirCadastroDeReserva(IReadOnlyList<CadastroDeReservaInformado?> chamadas) =>
        Editar(() => CadastroDeReservaPorChamada.Declarar(chamadas), SubstituirCadastroDeReserva);

    /// <summary>Declara como o edital prevê a manifestação de interesse pelo código de contrato; sem código, a manifestação não é prevista.</summary>
    public Result DefinirManifestacaoDeInteresse(string? codigo) =>
        Editar(() => DeclaracaoDaManifestacaoDeInteresse.Declarar(codigo), declarada => ManifestacaoDeInteresse = declarada);

    /// <summary>Define ou remove a divulgação pública (UNI-REQ-0050); nula volta ao padrão minimizado, que é publicável.</summary>
    public Result DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao? configuracao) =>
        Editar(() => PlanejamentoDaEdicao.Livre(configuracao), nova => ConfiguracaoDivulgacao = Vincular(nova));

    /// <summary>Define ou remove a taxa e os fundamentos de isenção; nula volta a "não declarado", que bloqueia a publicação.</summary>
    public Result DefinirTaxaInscricao(ConfiguracaoTaxaInscricao? configuracao) =>
        Editar(() => PlanejamentoDaEdicao.Livre(configuracao), nova => ConfiguracaoTaxaInscricao = Vincular(nova));

    /// <summary>Define ou remove a cascata de remanejamento.</summary>
    public Result DefinirCascataRemanejamento(ConfiguracaoCascataRemanejamento? cascata) =>
        Editar(() => PlanejamentoDaEdicao.Livre(cascata), nova => Cascata = Vincular(nova));

    /// <summary>Substitui os critérios de desempate; todo <c>etapa_ref</c> precisa existir entre as etapas.</summary>
    public Result DefinirCriteriosDesempate(IReadOnlyList<CriterioDesempate> criterios)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        return Editar(
            () => RegrasDoDesempate.ConferirSubstituicao(Declaracao(), criterios).Com(criterios),
            novos => Substituir(_criteriosDesempate, novos));
    }

    /// <summary>Define a classificação, conferindo o que depende de outras dimensões (etapa de cada eliminação, quadro de pesos do desempate).</summary>
    public Result DefinirClassificacao(ConfiguracaoClassificacao classificacao)
    {
        ArgumentNullException.ThrowIfNull(classificacao);

        return Editar(
            () => RegrasDaClassificacao.Planejar(Declaracao(), classificacao),
            plano =>
            {
                AplicarMudanca(plano.Mudanca);
                Classificacao = Vincular(plano.Classificacao);
            });
    }

    /// <summary>Define o formulário de uma finalidade; o que nasce com <paramref name="itensDaCriacao"/> nasce com eles, numa operação só.</summary>
    public Result DefinirFormulario(
        FinalidadeFormulario finalidade, Guid? faseId, string? titulo, IReadOnlyList<PaginaDoFormulario> paginas,
        IReadOnlyList<FatoColetado>? itensDaCriacao = null, ConsequenciaDoDescumprimento? consequenciaDaFaltaDeEnvio = null) =>
        DefinirFormulario(FormularioDeclarado.DaFinalidade(finalidade, faseId), titulo, paginas, itensDaCriacao, consequenciaDaFaltaDeEnvio);

    /// <summary>Define o formulário do código declarado; o que nasce com <paramref name="itensDaCriacao"/> nasce com eles, numa operação só.</summary>
    public Result DefinirFormulario(
        FormularioDeclarado declarado, string? titulo, IReadOnlyList<PaginaDoFormulario> paginas,
        IReadOnlyList<FatoColetado>? itensDaCriacao = null, ConsequenciaDoDescumprimento? consequenciaDaFaltaDeEnvio = null)
    {
        ArgumentNullException.ThrowIfNull(declarado);
        ArgumentNullException.ThrowIfNull(paginas);

        return Editar(
            () => RegrasDosFormularios.PlanejarDefinicao(Declaracao(), declarado, titulo, paginas, itensDaCriacao, consequenciaDaFaltaDeEnvio),
            Aplicar);
    }

    public Result RemoverFormulario(FinalidadeFormulario finalidade) =>
        RemoverFormulario(CodigoDaFinalidade(finalidade));

    /// <summary>Remove o formulário do código, com os itens e os termos dele.</summary>
    public Result RemoverFormulario(CodigoDoFormulario codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return Editar(() => RegrasDosFormularios.PlanejarRemocao(Declaracao(), codigo), AplicarMudanca);
    }

    public Result AplicarModeloDeFormulario(CopiaDeModeloDeFormulario copia)
    {
        ArgumentNullException.ThrowIfNull(copia);

        return Editar(() => AplicacaoDeModeloDeFormulario.Planejar(Declaracao(), copia), Aplicar);
    }

    public Result DefinirTermosDoFormulario(
        FinalidadeFormulario finalidade, IReadOnlyList<TermoExigidoFormulario> termos) =>
        DefinirTermosDoFormulario(CodigoDaFinalidade(finalidade), termos);

    /// <summary>Substitui os termos exigidos pelo formulário do código.</summary>
    public Result DefinirTermosDoFormulario(CodigoDoFormulario codigo, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(termos);

        return Editar(() => RegrasDosFormularios.PlanejarTermos(Declaracao(), codigo, termos), AplicarMudanca);
    }

    /// <summary>O formulário da finalidade, quando o processo o tem.</summary>
    public FormularioProcesso? FormularioDe(FinalidadeFormulario finalidade) =>
        _formularios.FirstOrDefault(f => f.Finalidade == finalidade);

    /// <summary>O formulário do código, quando o processo o tem.</summary>
    public FormularioProcesso? FormularioDe(CodigoDoFormulario codigo) =>
        _formularios.FirstOrDefault(f => f.Codigo == codigo);

    // Provisório até a retirada da finalidade: a operação que ainda recebe a finalidade fala do formulário pelo código dela.
    private static CodigoDoFormulario CodigoDaFinalidade(FinalidadeFormulario finalidade) =>
        CodigoDoFormulario.Criar(finalidade.Codigo()).Value!;

    /// <summary>Substitui o cronograma. O grafo de precedências é parâmetro: o domínio não consulta a Configuração.</summary>
    public Result DefinirCronogramaFases(
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(precedencias);

        return Editar(
            () => RegrasDoCronograma.Planejar(Declaracao(), fases, precedencias),
            Aplicar);
    }

    /// <summary>Substitui as regras de comprovação, conferindo a fase dos documentos no cronograma.</summary>
    public Result DefinirDocumentosExigidos(IReadOnlyList<RegraDeComprovacao> regrasSemGrupo)
    {
        ArgumentNullException.ThrowIfNull(regrasSemGrupo);

        return Editar(
            () => RegrasDasExigencias.Planejar(Declaracao(), regrasSemGrupo),
            AplicarMudanca);
    }

    /// <summary>
    /// Substitui os itens do formulário de uma finalidade, com as mesmas regras de
    /// <see cref="DefinirFatosColetados(CodigoDoFormulario, IReadOnlyList{FatoColetado}, IReadOnlyList{GrupoColetado}?)"/>.
    /// </summary>
    public Result DefinirFatosColetados(
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetado> fatosColetados,
        IReadOnlyList<GrupoColetado>? grupos = null) =>
        DefinirFatosColetados(CodigoDaFinalidade(finalidade), fatosColetados, grupos);

    /// <summary>
    /// Substitui os itens do formulário do código — quais fatos ele coleta, em que ordem e sob qual
    /// pré-condição cada campo aparece — sem mexer nos dos outros formulários. Sem grupos informados, os do
    /// formulário ficam como estão; a lista vazia os remove. Editável só em rascunho.
    /// </summary>
    public Result DefinirFatosColetados(
        CodigoDoFormulario codigo,
        IReadOnlyList<FatoColetado> fatosColetados,
        IReadOnlyList<GrupoColetado>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(fatosColetados);

        return Editar(() => RegrasDaColeta.Planejar(Declaracao(), codigo, fatosColetados, grupos), AplicarMudanca);
    }

    /// <summary>Refaz a cópia dos itens, a das unidades das fases, a proteção das exigências e os nomes dos municípios de prova vindos do Geo, nessa ordem, antes de canonicalizar; a recusa dos itens deixa o resto como está.</summary>
    public Result RecopiarAoCongelar(CopiasDoCongelamento copias)
    {
        ArgumentNullException.ThrowIfNull(copias);

        Result itens = RecopiarItensDoCatalogo(copias.CopiasDoCatalogo, copias.FatosJaColetados);
        if (itens.IsSuccess)
        {
            RecopiarUnidadesDasFases(copias.UnidadesVivas);
            AplicarProtecaoVigente(copias.ProtecaoVigente);
            ProvaPresencial = ProvaPresencial?.ComNomesDoGeo(copias.MunicipiosDoGeo);
        }

        return itens;
    }

    internal Result RecopiarItensDoCatalogo(
        IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        Result<PlanoDaRecopiaDoCatalogo> plano = RegrasDaColeta.PlanejarRecopia(Declaracao(), catalogo, fatosJaColetados);
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

    public Result DefinirRegrasDerivacao(IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao)
    {
        ArgumentNullException.ThrowIfNull(regrasDerivacao);

        return Editar(
            () => RegrasDaColeta.ConferirDerivacoes(Declaracao(), regrasDerivacao).Com(regrasDerivacao),
            novas => Substituir(_regrasDerivacao, novas));
    }

    /// <summary>Define a âncora de <c>FAIXA_ETARIA</c>; nula é válida enquanto nenhuma exigência tem gatilho por idade.</summary>
    public Result DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos? referencia) =>
        Editar(
            () => ReferenciaTemporalDosFatos.RecusaDaFaseForaDoCronograma(Declaracao(), referencia).Com(referencia),
            nova => ReferenciaTemporalFatos = nova);

    /// <summary>Declara ou troca o identificador legível; não há remoção, o certame sempre tem um.</summary>
    public Result DefinirIdentificadorLegivel(IdentificadorLegivel identificador)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador.Valor, nameof(identificador));

        return Editar(
            () => PlanejamentoDaEdicao.Livre(identificador),
            novo => IdentificadorLegivel = novo);
    }

    /// <summary>Redeclara a localidade (UNI-REQ-0111). A atribuição é incondicional: a igualdade considera só o código IBGE, e comparar antes descartaria a correção de um nome divergente.</summary>
    public Result DefinirLocalidade(LocalidadeRegente localidade) =>
        Editar(
            () => ContagemDePrazos.RecusaDaLocalidadeAusente(localidade).Com(localidade),
            nova => Localidade = nova);

    /// <summary>Declara a convenção de contagem dos prazos (UNI-REQ-0112), já resolvida contra o rol de regras.</summary>
    public Result DefinirAlgoritmoContagemPrazo(ReferenciaRegra algoritmo) =>
        Editar(
            () => ContagemDePrazos.RecusaDaConvencaoAusente(algoritmo).Com(algoritmo),
            nova => AlgoritmoContagemPrazo = nova);

    /// <summary>Congela em cada exigência a proteção efetiva calculada pelo contexto; a exigência que o contexto não traz mantém a cópia.</summary>
    internal void AplicarProtecaoVigente(ProtecaoVigenteDasExigencias protecao) =>
        ProtecaoDasExigencias.Planejar(Declaracao(), protecao).Aplicar();

    /// <summary>
    /// Publica o processo em rascunho (UNI-REQ-0019): confere a transição, abre
    /// a versão 1 a partir dos bytes canônicos que a Application produziu (o Domain não canonicaliza,
    /// ADR-0042) e transita a situação, tudo em memória e atomicamente.
    /// </summary>
    public Result<VersaoConfiguracao> Publicar(PedidoDeCongelamento pedido) =>
        AssumirVersaoEmitida(TransicoesDaVersao.Publicar(Id, Situacao, pedido));

    /// <summary>
    /// Retifica um processo publicado (ADR-0101/0103): emite o ato que emenda o ato criador da
    /// <paramref name="versaoAtual"/> e a sucede, e a versão anterior fica intocada.
    /// </summary>
    /// <param name="versaoAtual">A versão corrente, carregada pelo handler: <see cref="VersaoConfiguracao"/> é agregado próprio.</param>
    /// <param name="motivo">Justificativa obrigatória do ato (ADR-0101).</param>
    public Result<VersaoConfiguracao> Retificar(PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo) =>
        AssumirVersaoEmitida(TransicoesDaVersao.Retificar(Id, Situacao, pedido, versaoAtual, motivo));

    private Result<VersaoConfiguracao> AssumirVersaoEmitida(Result<VersaoEmitida> emissao)
    {
        if (emissao.IsFailure)
        {
            return Result<VersaoConfiguracao>.Failure(emissao.Error!);
        }

        Situacao = SituacaoDoProcessoSeletivo.Publicado;
        AddDomainEvent(emissao.Value!.Evento);
        return Result<VersaoConfiguracao>.Success(emissao.Value.Versao);
    }

    /// <summary>
    /// O que impede mutar a configuração por dimensão agora, ou nulo: só o rascunho edita, e o processo publicado muda
    /// por retificação, que traz o documento inteiro. Os handlers a antecipam antes de validar o payload, porque a recusa
    /// de estado precede a regra de negócio; cada operação a repete por dentro.
    /// </summary>
    public DomainError? MutacaoBloqueada() => Situacao.RecusaDeEdicaoForaDoRascunho();

    /// <summary>
    /// O protocolo de edição (ADR-0125 §5): a guarda de mutação, o plano calculado sem efeito e a aplicação do plano.
    /// A aplicação só roda com o plano aprovado e nunca falha.
    /// </summary>
    private Result Editar<TPlano>(Func<Result<TPlano>> planejar, Action<TPlano> aplicar)
    {
        Result<TPlano> plano = MutacaoBloqueada() is { } bloqueio ? Result<TPlano>.Failure(bloqueio) : planejar();
        if (plano.IsSuccess)
        {
            aplicar(plano.Value!);
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
        Trocar(_regrasDeComprovacao, mudanca.RegrasDeComprovacao);
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

    private void SubstituirCadastroDeReserva(CadastroDeReservaPorChamada declarado)
    {
        _cadastroDeReserva.Clear();
        _cadastroDeReserva.AddRange(declarado.Chamadas);
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
