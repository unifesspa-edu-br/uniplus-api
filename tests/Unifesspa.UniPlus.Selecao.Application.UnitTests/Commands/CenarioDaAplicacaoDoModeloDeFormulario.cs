namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo, o catálogo de fatos e os leitores que o handler de aplicação do modelo de formulário consulta.
/// Os testes descrevem o que o catálogo e o modelo têm; a chamada ao handler fica toda aqui, de modo que uma
/// mudança de assinatura dele se faz num lugar só.
/// </summary>
internal sealed class CenarioDaAplicacaoDoModeloDeFormulario
{
    private readonly IProcessoSeletivoRepository _repository = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IModeloFormularioReader _modelos = Substitute.For<IModeloFormularioReader>();
    private readonly ITipoDocumentoReader _tiposDocumento = Substitute.For<ITipoDocumentoReader>();
    private readonly IModeloDeDocumentoRepository _modelosDeDocumento = Substitute.For<IModeloDeDocumentoRepository>();

    public CenarioDaAplicacaoDoModeloDeFormulario()
    {
        _repository.ObterParaMutacaoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns(Processo);
        Fatos.ListarAsync(Arg.Any<CancellationToken>()).Returns(_ => CatalogoDoConjuntoBasico.Com(Catalogo));
        Fatos.ListarRegrasPadraoAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, IReadOnlyList<RegraDerivacao>>(StringComparer.Ordinal)
        {
            ["PERFIL"] = [RegraDerivacao.Criar(Quando("QUILOMBOLA", true), "A").Value!],
            ["FLAG"] = [RegraDerivacao.CriarBooleana(Quando("QUILOMBOLA", true))],
            ["COTISTA"] = [RegraDerivacao.Criar(
                PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("MODALIDADE", Operador.Em, JsonSerializer.SerializeToElement(new[] { "AC" })).Value!)]).Value!,
                "SIM").Value!],
        });
        Termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    public ProcessoSeletivo Processo { get; } = ProcessoSeletivo.Criar(
        "PS Modelo", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    public IFatoCandidatoReader Fatos { get; } = Substitute.For<IFatoCandidatoReader>();

    public ITermoConsentimentoReader Termos { get; } = Substitute.For<ITermoConsentimentoReader>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public List<FatoCandidatoView> Catalogo { get; } =
    [
        Declarado("QUILOMBOLA"),
        Declarado("CERTIFICADO"),
        Declarado("DATA_NASCIMENTO") with { Dominio = "DATA" },
        Declarado("MAIOR_IDADE") with { Escopo = "MEMBRO_GRUPO" },
        Declarado("SOB_GUARDA") with { Escopo = "MEMBRO_GRUPO", Ativo = false },
        Declarado("BAIXA_RENDA") with { Ativo = false },
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR", ["PRETA", "AMARELA", "INDIGENA"], "INSCRICAO",
            "CAMPO_FORMULARIO:COR_RACA", [new("PRETA", "Preta", 0, true), new("AMARELA", "Amarela", 1, false), new("INDIGENA", "Indígena", 2, false)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "PERFIL", "Perfil", "Descrição do fato.", "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["A", "B"], "INSCRICAO",
            "REGRA_DERIVACAO:PERFIL", [new("A", "A", 0, true), new("B", "B", 1, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FLAG", "Flag", "Descrição do fato.", "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO", "REGRA_DERIVACAO:FLAG", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FLAG_SEM_REGRA", "Flag sem regra", "Descrição do fato.", "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            "REGRA_DERIVACAO:FLAG_SEM_REGRA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "COTISTA", "Cotista", "Descrição do fato.", "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["SIM"], "INSCRICAO",
            "REGRA_DERIVACAO:COTISTA", [new("SIM", "Sim", 0, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "SEM_REGRA", "Sem regra", "Descrição do fato.", "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["X"], "INSCRICAO",
            "REGRA_DERIVACAO:SEM_REGRA", [new("X", "X", 0, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", "Descrição do fato.", "CATEGORICO", "DERIVADO", "MULTIVALORADO", null, "INSCRICAO",
            "REGRA_DERIVACAO:MODALIDADE", null, "MODALIDADE", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", "Descrição do fato.", "NUMERICO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    public static ModeloFormularioView Modelo(
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetadoInput> itens,
        IReadOnlyList<TermoExigidoInput>? termos = null,
        IReadOnlyList<string>? pressupostos = null,
        IReadOnlyList<GrupoColetadoInput>? grupos = null,
        IReadOnlyList<DocumentoExigidoDoModeloInput>? documentos = null) =>
        new(Guid.NewGuid(), "MODELO", "Modelo", null, EstruturaFormulario.ParaToken(finalidade), null, true,
            new ConteudoDoModeloInput(
                "Formulário",
                [
                    EntradaDaEtapaFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
                    EntradaDaEtapaFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão").Construir(),
                ],
                itens, termos ?? [], pressupostos ?? [], grupos ?? [], documentos));

    public static ModeloFormularioView ModeloComDocumentos(params DocumentoExigidoDoModeloInput[] documentos) =>
        Modelo(
            FinalidadeFormulario.Inscricao,
            [.. ConjuntoBasicoDaInscricao.Itens, Item("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 100), Item("CONDICAO_ATENDIMENTO", 101, "SELECAO_MULTIPLA")],
            documentos: documentos);

    public static DocumentoExigidoDoModeloInput DocumentoDoModelo(string tipo, string condicao) =>
        new("INSCRICAO", tipo, Obrigatorio: false,
            [[
                new CondicaoPrecondicaoInput("SOLICITA_ATENDIMENTO_ESPECIALIZADO", "IGUAL", JsonSerializer.SerializeToElement(true)),
                new CondicaoPrecondicaoInput("CONDICAO_ATENDIMENTO", "IGUAL", JsonSerializer.SerializeToElement(condicao)),
            ]],
            "REMOVE_VANTAGEM", []);

    public static GrupoColetadoInput Composicao(params FatoColetadoInput[] campos) =>
        EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("Composição familiar").NaSecao("DADOS")
            .ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE").ComCampos(campos).Construir();

    public static FatoColetadoInput Campo(string fato, int ordem) => EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem).Construir();

    public static FatoColetadoInput Item(string fato, int ordem, string tipo = "BOOLEANO") => Montar(fato, ordem, tipo).Construir();

    /// <summary>O item na seção <c>DADOS</c>, ainda sem fechar, para o teste que lhe dá regras ou apresentação.</summary>
    public static EntradaDoFatoColetadoDeTeste Montar(string fato, int ordem, string tipo = "BOOLEANO") =>
        EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem).DoTipo(tipo).NaSecao("DADOS");

    public static FatoColetadoInput ItemQueCita(FatoColetadoInput item, string fato, string operador, object valor) =>
        item with { Regras = item.Regras! with { Exibicao = [[new CondicaoPrecondicaoInput(fato, operador, JsonSerializer.SerializeToElement(valor))]] } };

    public static FatoColetado FatoBooleanoJaColetado(string codigo, string rotulo) =>
        FatoColetadoDeTeste.Coletando(codigo, 0).ComRotulo(rotulo).ComObrigatoriedade(Obrigatoriedade.Sempre).Construir();

    public static FatoCandidatoView Declarado(string codigo) =>
        new(Guid.CreateVersion7(), codigo, codigo, "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO", $"CAMPO_FORMULARIO:{codigo}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    public static PredicadoDnf Quando(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    public static ModalidadeSelecionada Ofertada(string codigo, NaturezaLegalModalidade natureza, PredicadoDnf regra) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.RestanteDasVagasOfertadas : ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, regra, null, "base legal", quantidadeDeclarada: 10).Value!;

    /// <summary>O cronograma do processo com as fases dos códigos; só a de inscrição coleta inscrição. Devolve a primeira.</summary>
    public FaseCronograma FaseDoCronograma(params string[] codigos)
    {
        FaseCronograma[] fases =
        [
            .. codigos.Select((codigo, i) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
                new OrigemCanonicaDaFase(
                    Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: codigo == "INSCRICAO",
                    ColetaSolicitacaoIsencao: false),
                new DecisoesDeCronogramaDaFase(i + 1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
                new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
                bancasRequeridas: []).Value!),
        ];
        Processo.DefinirCronogramaFases(fases, []).IsSuccess.Should().BeTrue();
        return fases[0];
    }

    public void DeclararAtendimentoEspecializado()
    {
        Catalogo.Add(Declarado("SOLICITA_ATENDIMENTO_ESPECIALIZADO"));
        Catalogo.Add(new FatoCandidatoView(Guid.CreateVersion7(), "CONDICAO_ATENDIMENTO", "Condição", "Descrição do fato.", "CATEGORICO", "DECLARADO", "MULTIVALORADO", ["PCD", "LACTANTE"], "INSCRICAO",
            "CAMPO_FORMULARIO:CONDICAO_ATENDIMENTO", [new("PCD", "PcD", 0, true), new("LACTANTE", "Lactante", 1, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true));
        List<TipoDocumentoView> tipos = [.. new[] { "LAUDO_MEDICO", "CERTIDAO_NASCIMENTO" }.Select(static codigo => new TipoDocumentoView(Guid.CreateVersion7(), codigo, codigo, "SAUDE", ClassificacaoDeProtecao.Pessoal, "Comprovação documental do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL", ProtecaoAprovada: true))];
        _tiposDocumento.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => tipos.Single(t => t.Codigo == call.Arg<string>()));
        _tiposDocumento.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => tipos.Single(t => t.Id == call.Arg<Guid>()));
    }

    public void Ofertar(params ModalidadeSelecionada[] modalidades)
    {
        DeclararFatosDaOpcaoPelasCotas();
        Processo.DefinirDistribuicaoVagas(
            [DistribuicaoDeVagasCom(modalidades)], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    public Task<Result<AplicacaoDeModeloDto>> AplicarAsync(ModeloFormularioView modelo)
    {
        _modelos.ObterAsync(modelo.Id, Arg.Any<CancellationToken>()).Returns(modelo);
        return AplicarAsync(modelo.Id);
    }

    public Task<Result<AplicacaoDeModeloDto>> AplicarAsync(Guid modeloId) =>
        AplicarModeloFormularioCommandHandler.Handle(
            new AplicarModeloFormularioCommand(Processo.Id, modeloId),
            _repository, _modelos, Fatos, Termos, _tiposDocumento, _modelosDeDocumento, UnitOfWork, CancellationToken.None);

    private static FatoCandidatoView DerivadoBooleano(string codigo, string rotulo) =>
        new(Guid.CreateVersion7(), codigo, rotulo, "Descrição do fato.", "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            $"REGRA_DERIVACAO:{codigo}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static ConfiguracaoDistribuicaoVagas DistribuicaoDeVagasCom(ModalidadeSelecionada[] modalidades) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 10 * modalidades.Length, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private void DeclararFatosDaOpcaoPelasCotas()
    {
        Catalogo.Add(DerivadoBooleano(OpcaoPelasCotasDaLei.Fato, "Optou"));
        Catalogo.Add(DerivadoBooleano(OrigemEscolar.FatoEgresso, "Egresso"));
        Catalogo.Add(Declarado(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica));
        Fatos.ListarRegrasPadraoAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, IReadOnlyList<RegraDerivacao>>(StringComparer.Ordinal)
        {
            [OpcaoPelasCotasDaLei.Fato] = OpcaoPelasCotasDaLei.RegrasDaOpcao,
            [OrigemEscolar.FatoEgresso] = [RegraDerivacao.CriarBooleana(Quando("CERTIFICADO", true))],
        });
    }
}
