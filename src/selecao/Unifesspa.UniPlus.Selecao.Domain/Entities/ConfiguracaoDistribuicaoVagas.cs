namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração de distribuição de vagas de uma oferta de curso dentro do <see cref="ProcessoSeletivo"/>:
/// o que o admin declara (as vagas ofertadas base, a regra de distribuição, o percentual da reserva legal
/// no ramo da Lei de Cotas, a referência demográfica e o motor de ajuste) mais as modalidades que
/// participam da oferta, e o quadro de vagas que a regra produz sobre essa declaração (ADR-0115).
/// </summary>
/// <remarks>
/// <para>
/// O quadro é derivado e se materializa na mesma operação que a declaração: as conferências e a montagem
/// são as do domínio de vagas (<see cref="ConferenciaDaDistribuicaoDeVagas"/> e
/// <see cref="MontagemDoQuadroDeVagas"/>), e a entidade guarda o resultado na forma do ramo. A reserva
/// legal só existe na Lei de Cotas; as vagas retiradas no ajuste só existem no quadro fixado no edital.
/// </para>
/// <para>
/// Entidade interna do agregado <see cref="ProcessoSeletivo"/>: criada, substituída e persistida
/// exclusivamente pela raiz, via <see cref="ProcessoSeletivo.DefinirDistribuicaoVagas"/>.
/// </para>
/// </remarks>
public sealed class ConfiguracaoDistribuicaoVagas : EntityBase, IParteDoProcesso
{
    public Guid ProcessoSeletivoId { get; private set; }
    public Guid OfertaCursoOrigemId { get; private set; }
    public int VagasOfertadasBase { get; private set; }

    /// <summary>
    /// O percentual da reserva legal que a Lei de Cotas aplica sobre as vagas ofertadas base (art. 10, II);
    /// <see langword="null"/> no quadro fixado no edital, que não calcula por percentual.
    /// </summary>
    public decimal? PercentualDaReservaLegal { get; private set; }

    public ReferenciaRegra RegraDistribuicao { get; private set; } = null!;

    /// <summary>
    /// Snapshot da referência de reserva demográfica (Censo e percentuais): obrigatório na Lei de Cotas
    /// e ausente no quadro fixado no edital.
    /// </summary>
    public ReferenciaReservaDemograficaSnapshot? ReferenciaDemografica { get; private set; }

    /// <summary>
    /// A regra que rege a reconciliação do quadro: obrigatória na Lei de Cotas (art. 11, parágrafo único) e
    /// opcional no quadro fixado no edital.
    /// </summary>
    public ReferenciaRegra? RegraAjuste { get; private set; }

    /// <summary>
    /// O motor que reduz o quadro fixado cuja soma passou das vagas ofertadas base, como o edital o declarou;
    /// <see langword="null"/> quando não declarou motor e na Lei de Cotas, que não aceita motor.
    /// </summary>
    public ArgsRegraAjusteDistribuicao? AjusteDoQuadro { get; private set; }

    /// <summary>
    /// Grupo de área do ENEM do curso da oferta, copiado por valor do cadastro na definição (ADR-0061); nulo
    /// quando o curso não o declara. A exigência é de publicação, quando o processo calcula a média pelos
    /// pesos por área.
    /// </summary>
    public GrupoAreaEnemSnapshot? GrupoAreaEnem { get; private set; }

    /// <summary>
    /// O que nomeia a oferta para o candidato, copiado por valor do cadastro na definição (ADR-0061); nulo só
    /// em distribuição definida antes de a oferta ser nomeada aqui, e a publicação a recusa.
    /// </summary>
    public OfertaCursoSnapshot? OfertaCurso { get; private set; }

    /// <summary>A reserva que o percentual da lei dá sobre as vagas ofertadas base; só na Lei de Cotas.</summary>
    public int? ReservaLegalNominal { get; private set; }

    /// <summary>A reserva que o quadro concedeu, que passa da nominal quando a garantia mínima a ultrapassa; só na Lei de Cotas.</summary>
    public int? ReservaLegalEfetiva { get; private set; }

    /// <summary>O quanto a reserva efetiva passou da nominal, congelado junto do quadro; só na Lei de Cotas.</summary>
    public int? ExcedenteSobreReservaLegal { get; private set; }

    /// <summary>As vagas que a oferta não comportou e a escassez cortou das reservas; só na Lei de Cotas.</summary>
    public int? VagasCortadasPorEscassez { get; private set; }

    /// <summary>As vagas que o ajuste retirou do quadro fixado, por modalidade; só no quadro fixado no edital.</summary>
    public IReadOnlyList<VagasDaModalidade>? VagasRetiradasNoAjuste { get; private set; }

    public int TotalDeVagasDoEdital { get; private set; }

    private readonly List<ModalidadeSelecionada> _modalidades = [];
    public IReadOnlyCollection<ModalidadeSelecionada> Modalidades => _modalidades.AsReadOnly();

    private readonly List<VagaOfertada> _vagasOfertadas = [];

    /// <summary>As linhas do quadro, uma por modalidade selecionada, na ordem em que as modalidades foram declaradas.</summary>
    public IReadOnlyCollection<VagaOfertada> VagasOfertadas => _vagasOfertadas.AsReadOnly();

    /// <summary>
    /// O quadro de vagas na forma do ramo: com a conferência da reserva legal na Lei de Cotas, com as vagas
    /// retiradas no ajuste no quadro fixado no edital.
    /// </summary>
    public QuadroDeVagas Quadro => ReservaLegalNominal is { } nominal
        ? new QuadroDeVagasDaLeiDeCotas(
            VagasDoQuadro(),
            TotalDeVagasDoEdital,
            new ConferenciaDaReservaLegal(nominal, ReservaLegalEfetiva!.Value, VagasCortadasPorEscassez!.Value))
        : new QuadroDeVagasFixadoNoEdital(VagasDoQuadro(), TotalDeVagasDoEdital, VagasRetiradasNoAjuste ?? []);

    private ConfiguracaoDistribuicaoVagas() { }

    /// <summary>
    /// Cria a configuração de distribuição de vagas de uma oferta. Acumula (ADR-0125) as recusas da forma,
    /// da conferência da distribuição, da coerência entre as modalidades e do grupo de área do ENEM; só depois
    /// monta o quadro, que pode recusar o que depende do cálculo.
    /// </summary>
    /// <param name="ofertaCursoOrigemId">A oferta de curso a que a distribuição pertence.</param>
    /// <param name="declarada">O que o admin declara para a oferta, com todos os dados aceitos.</param>
    /// <param name="fatos">O que os cadastros dizem sobre a oferta; <see langword="null"/> quando não há nenhum.</param>
    /// <exception cref="InvalidOperationException">A declaração traz dado que o cadastro recusou.</exception>
    public static Result<ConfiguracaoDistribuicaoVagas> Criar(
        Guid ofertaCursoOrigemId,
        DistribuicaoDeVagasDeclarada declarada,
        FatosDaOfertaNoCadastro? fatos = null)
    {
        ArgumentNullException.ThrowIfNull(declarada);
        declarada.ExigirSemRecusados();
        fatos ??= FatosDaOfertaNoCadastro.Nenhum;

        IReadOnlyList<FieldError> recusas =
        [
            .. ConferenciaDaDistribuicaoDeVagas.ConferirForma(declarada),
            .. ConferirDeclaracao(declarada, fatos),
        ];

        return recusas.Count > 0
            ? Result<ConfiguracaoDistribuicaoVagas>.ValidationFailure(recusas)
            : MontarEMaterializar(ofertaCursoOrigemId, declarada, fatos, ConferirGrupoAreaEnem(fatos.GrupoAreaEnem).Value);
    }

    /// <summary>
    /// O que a declaração, depois da forma, tem de cumprir: a conferência da distribuição, as referências entre
    /// as modalidades aceitas e o grupo de área do ENEM. Uma regra deixa de rodar só quando um dado dela foi
    /// recusado na resolução do cadastro.
    /// </summary>
    /// <param name="declarada">O que o admin declara para a oferta.</param>
    /// <param name="fatos">O que os cadastros dizem sobre a oferta; <see langword="null"/> quando não há nenhum.</param>
    public static IReadOnlyList<FieldError> ConferirDeclaracao(
        DistribuicaoDeVagasDeclarada declarada, FatosDaOfertaNoCadastro? fatos = null)
    {
        ArgumentNullException.ThrowIfNull(declarada);
        fatos ??= FatosDaOfertaNoCadastro.Nenhum;

        return
        [
            .. ConferenciaDaDistribuicaoDeVagas.Conferir(declarada, fatos.VagasAnuaisAutorizadas, fatos.ModalidadesAdmitidas),
            .. ConferenciaDasReferenciasEntreModalidades.Conferir(declarada),
            .. ConferirGrupoAreaEnem(fatos.GrupoAreaEnem).Errors,
        ];
    }

    // Ausente é legítimo (curso sem grupo declarado); presente e inválido não designa grupo nenhum e
    // deixaria a resolução de pesos sem chave.
    private static Result<GrupoAreaEnemSnapshot?> ConferirGrupoAreaEnem((string? Codigo, string? Rotulo)? informado)
    {
        if (informado is not { } grupo)
        {
            return Result<GrupoAreaEnemSnapshot?>.Success(null);
        }

        Result<GrupoAreaEnemSnapshot> criado = GrupoAreaEnemSnapshot.Criar(grupo.Codigo, grupo.Rotulo);
        return criado.IsFailure
            ? Result<GrupoAreaEnemSnapshot?>.ValidationFailure([new FieldError("grupoAreaEnem", criado.Error!)])
            : Result<GrupoAreaEnemSnapshot?>.Success(criado.Value);
    }

    private static Result<ConfiguracaoDistribuicaoVagas> MontarEMaterializar(
        Guid ofertaCursoOrigemId,
        DistribuicaoDeVagasDeclarada declarada,
        FatosDaOfertaNoCadastro fatos,
        GrupoAreaEnemSnapshot? grupo)
    {
        Result<QuadroDeVagas> quadro = MontagemDoQuadroDeVagas.Montar(declarada);
        if (quadro.IsFailure)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(quadro.Error!);
        }

        Result<List<VagaOfertada>> vagas = MaterializarVagas(quadro.Value!, declarada.Modalidades);
        return vagas.IsFailure
            ? Result<ConfiguracaoDistribuicaoVagas>.Failure(vagas.Error!)
            : Result<ConfiguracaoDistribuicaoVagas>.Success(
                Compor(ofertaCursoOrigemId, declarada, fatos, grupo, quadro.Value!, vagas.Value!));
    }

    private static ConfiguracaoDistribuicaoVagas Compor(
        Guid ofertaCursoOrigemId,
        DistribuicaoDeVagasDeclarada declarada,
        FatosDaOfertaNoCadastro fatos,
        GrupoAreaEnemSnapshot? grupo,
        QuadroDeVagas quadro,
        List<VagaOfertada> vagas)
    {
        ConfiguracaoDistribuicaoVagas configuracao = DaDeclaracao(ofertaCursoOrigemId, declarada, fatos, grupo);
        configuracao.RegistrarQuadro(quadro, declarada);
        configuracao.Vincular(declarada.Modalidades, vagas);
        return configuracao;
    }

    private static ConfiguracaoDistribuicaoVagas DaDeclaracao(
        Guid ofertaCursoOrigemId,
        DistribuicaoDeVagasDeclarada declarada,
        FatosDaOfertaNoCadastro fatos,
        GrupoAreaEnemSnapshot? grupo) => new()
        {
            OfertaCursoOrigemId = ofertaCursoOrigemId,
            VagasOfertadasBase = declarada.VagasOfertadasBase,
            RegraDistribuicao = declarada.RegraDeDistribuicao!,
            RegraAjuste = declarada.RegraDeAjuste,
            ReferenciaDemografica = declarada.ReferenciaDemografica,
            GrupoAreaEnem = grupo,
            OfertaCurso = fatos.OfertaCurso,
        };

    private static Result<List<VagaOfertada>> MaterializarVagas(
        QuadroDeVagas quadro, IReadOnlyList<ModalidadeSelecionada> modalidades)
    {
        List<VagaOfertada> vagas = [];
        foreach (VagasDaModalidade linha in quadro.Vagas)
        {
            Guid origem = modalidades.First(m => string.Equals(m.Codigo, linha.ModalidadeCodigo, StringComparison.Ordinal)).ModalidadeOrigemId;
            Result<VagaOfertada> vaga = VagaOfertada.Criar(origem, linha.ModalidadeCodigo, linha.Quantidade);
            if (vaga.IsFailure)
            {
                return Result<List<VagaOfertada>>.Failure(vaga.Error!);
            }

            vagas.Add(vaga.Value!);
        }

        return Result<List<VagaOfertada>>.Success(vagas);
    }

    private void RegistrarQuadro(QuadroDeVagas quadro, DistribuicaoDeVagasDeclarada declarada)
    {
        TotalDeVagasDoEdital = quadro.TotalDeVagasDoEdital;
        switch (quadro)
        {
            case QuadroDeVagasDaLeiDeCotas leiDeCotas:
                PercentualDaReservaLegal = declarada.PercentualDaReservaLegal;
                RegistrarConferenciaDaReservaLegal(leiDeCotas.ConferenciaDaReservaLegal);
                break;
            case QuadroDeVagasFixadoNoEdital fixado:
                AjusteDoQuadro = declarada.AjusteDoQuadro;
                VagasRetiradasNoAjuste = fixado.VagasRetiradasNoAjuste;
                break;
            default:
                throw new InvalidOperationException($"Forma de quadro sem registro: {quadro.GetType().Name}.");
        }
    }

    private void RegistrarConferenciaDaReservaLegal(ConferenciaDaReservaLegal conferencia)
    {
        ReservaLegalNominal = conferencia.ReservaLegalNominal;
        ReservaLegalEfetiva = conferencia.ReservaLegalEfetiva;
        ExcedenteSobreReservaLegal = conferencia.ExcedenteSobreAReservaLegal;
        VagasCortadasPorEscassez = conferencia.VagasCortadasPorEscassez;
    }

    private void Vincular(IReadOnlyList<ModalidadeSelecionada> modalidades, List<VagaOfertada> vagas)
    {
        foreach (ModalidadeSelecionada modalidade in modalidades)
        {
            modalidade.VincularConfiguracao(Id);
            _modalidades.Add(modalidade);
        }

        foreach (VagaOfertada vaga in vagas)
        {
            vaga.VincularConfiguracao(Id);
            _vagasOfertadas.Add(vaga);
        }
    }

    private List<VagasDaModalidade> VagasDoQuadro() =>
        [.. _vagasOfertadas.Select(static vaga => new VagasDaModalidade(vaga.ModalidadeCodigo, vaga.Quantidade))];

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
