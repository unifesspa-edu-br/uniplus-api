namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A aplicação do modelo de formulário ao processo (UNI-REQ-0144, ADR-0061): confere o modelo,
/// descarta e relata o que o catálogo mudou, segue a regra do par de finalidades e copia do catálogo
/// as derivações que a cópia cita e o processo não tem.
/// </summary>
public sealed class AplicarModeloFormularioCommandHandlerTests
{
    private readonly IProcessoSeletivoRepository _repository = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IModeloFormularioReader _modelos = Substitute.For<IModeloFormularioReader>();
    private readonly IFatoCandidatoReader _fatos = Substitute.For<IFatoCandidatoReader>();
    private readonly ITermoConsentimentoReader _termos = Substitute.For<ITermoConsentimentoReader>();
    private readonly ITipoDocumentoReader _tiposDocumento = Substitute.For<ITipoDocumentoReader>();
    private readonly IModeloDeDocumentoRepository _modelosDeDocumento = Substitute.For<IModeloDeDocumentoRepository>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
    private readonly ProcessoSeletivo _processo = ProcessoSeletivo.Criar(
        "PS Modelo", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private readonly List<FatoCandidatoView> _catalogo =
    [
        Declarado("QUILOMBOLA"),
        Declarado("CERTIFICADO"),
        Declarado("DATA_NASCIMENTO") with { Dominio = "DATA" },
        Declarado("MAIOR_IDADE") with { Escopo = "MEMBRO_GRUPO" },
        Declarado("SOB_GUARDA") with { Escopo = "MEMBRO_GRUPO", Ativo = false },
        Declarado("BAIXA_RENDA") with { Ativo = false },
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR", ["PRETA", "AMARELA", "INDIGENA"], "INSCRICAO",
            "CAMPO_FORMULARIO:COR_RACA", [new("PRETA", "Preta", 0, true), new("AMARELA", "Amarela", 1, false), new("INDIGENA", "Indígena", 2, false)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new(Guid.CreateVersion7(), "PERFIL", "Perfil", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["A", "B"], "INSCRICAO",
            "REGRA_DERIVACAO:PERFIL", [new("A", "A", 0, true), new("B", "B", 1, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new(Guid.CreateVersion7(), "FLAG", "Flag", null, "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO", "REGRA_DERIVACAO:FLAG", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new(Guid.CreateVersion7(), "FLAG_SEM_REGRA", "Flag sem regra", null, "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            "REGRA_DERIVACAO:FLAG_SEM_REGRA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new(Guid.CreateVersion7(), "COTISTA", "Cotista", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["SIM"], "INSCRICAO",
            "REGRA_DERIVACAO:COTISTA", [new("SIM", "Sim", 0, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new(Guid.CreateVersion7(), "SEM_REGRA", "Sem regra", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["X"], "INSCRICAO",
            "REGRA_DERIVACAO:SEM_REGRA", [new("X", "X", 0, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", null, "INSCRICAO",
            "REGRA_DERIVACAO:MODALIDADE", null, "MODALIDADE", Ativo: true, ClassificacaoProtecao: "PESSOAL"),
        new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", null, "NUMERICO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL"),
    ];

    public AplicarModeloFormularioCommandHandlerTests()
    {
        _repository.ObterParaMutacaoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns(_processo);
        _fatos.ListarAsync(Arg.Any<CancellationToken>()).Returns(_ => CatalogoDoConjuntoBasico.Com(_catalogo));
        _fatos.ListarRegrasPadraoAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, IReadOnlyList<RegraDerivacao>>(StringComparer.Ordinal)
        {
            ["PERFIL"] = [RegraDerivacao.Criar(Quando("QUILOMBOLA", true), "A").Value!],
            ["FLAG"] = [RegraDerivacao.CriarBooleana(Quando("QUILOMBOLA", true))],
            ["COTISTA"] = [RegraDerivacao.Criar(
                PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("MODALIDADE", Operador.Em, JsonSerializer.SerializeToElement(new[] { "AC" })).Value!)]).Value!,
                "SIM").Value!],
        });
        _termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact(DisplayName = "Modelo inexistente é recusado no campo do modelo")]
    public async Task Handle_ModeloInexistente_Recusa()
    {
        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Guid.NewGuid());

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(AplicacaoDeModeloErrorCodes.ModeloInexistente);
    }

    [Fact(DisplayName = "Modelo inativo e de outro tipo de processo é recusado pelos dois motivos")]
    public async Task Handle_ModeloInativoDeOutroTipo_RecusaOsDois()
    {
        ModeloFormularioView modelo = Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0)]) with { Ativo = false, TipoProcessoCodigo = "PSIQ" };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [AplicacaoDeModeloErrorCodes.ModeloInativo, AplicacaoDeModeloErrorCodes.TipoDeProcessoDiferente]);
    }

    [Fact(DisplayName = "Pressuposto que a inscrição do processo não coleta é recusado com o índice")]
    public async Task Handle_PressupostoAusente_Recusa()
    {
        ModeloFormularioView modelo = Modelo(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO", 0)], pressupostos: ["QUILOMBOLA"]);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("modelo.pressupostos[0]");
    }

    [Theory(DisplayName = "O termo da cópia cita a faixa etária quando a cópia coleta a data de nascimento")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_TermoCitaFaixaEtaria_PelaDataDeNascimento(bool coletaDataDeNascimento)
    {
        Guid termoId = Guid.NewGuid();
        Guid versaoId = Guid.NewGuid();
        _termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(
            [new VersaoTermoConsentimentoView(termoId, versaoId, "Consulta", "Autorizo.", "Lei 12.711/2012", "REGISTRO_DIGITAL_COM_LOG_IP", new string('b', 64))]);
        TermoExigidoInput termo = new(
            "CONSULTA", 0, termoId, versaoId, [[new CondicaoPrecondicaoInput("FAIXA_ETARIA", "MAIOR_IGUAL", JsonSerializer.SerializeToElement(18))]], "SEMPRE", null);
        FatoColetadoInput[] itens = coletaDataDeNascimento ? [Item("QUILOMBOLA", 0), Item("DATA_NASCIMENTO", 1, "DATA")] : [Item("QUILOMBOLA", 0)];

        // Na habilitação, que não tem a seção do conjunto básico, a data de nascimento é coletada só se o modelo a pede.
        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Habilitacao, itens, termos: [termo]));

        resultado.IsSuccess.Should().Be(coletaDataDeNascimento, resultado.Error?.Message);
    }

    [Fact(DisplayName = "O grupo repetível do modelo é copiado para o formulário do processo")]
    public async Task Handle_ModeloComGrupo_CopiaOGrupo()
    {
        ModeloFormularioView modelo = Modelo(FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0)], grupos: [Composicao(Campo("MAIOR_IDADE", 0))]);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _processo.GruposColetados.Single().Subitens.Single().FatoCodigo.Should().Be("MAIOR_IDADE");
    }

    [Fact(DisplayName = "Grupo com campo de fato desativado sai da cópia inteiro e volta no relatório")]
    public async Task Handle_GrupoComCampoDesativado_DescartaOGrupo()
    {
        ModeloFormularioView modelo = Modelo(
            FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0)], grupos: [Composicao(Campo("MAIOR_IDADE", 0), Campo("SOB_GUARDA", 1))]);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.Value!.Descartados.Should().BeEquivalentTo([new ParteDescartadaDto("GRUPO", "COMPOSICAO_FAMILIAR", "FATO_DESATIVADO")]);
        _processo.GruposColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O derivado citado só pelo campo do grupo recebe a regra padrão do catálogo")]
    public async Task Handle_DerivadoCitadoPeloCampoDoGrupo_CopiaARegra()
    {
        ModeloFormularioView modelo = Modelo(
            FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0)],
            grupos: [Composicao(Campo("MAIOR_IDADE", 0) with { Precondicao = [[new CondicaoPrecondicaoInput("PERFIL", "IGUAL", JsonSerializer.SerializeToElement("A"))]] })]);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.DerivacoesCopiadas.Should().Equal("PERFIL");
    }

    [Fact(DisplayName = "Item de fato desativado e termo de versão removida saem da cópia e voltam no relatório")]
    public async Task Handle_PartesQueOCatalogoMudou_DescartaERelata()
    {
        TermoExigidoInput termo = new("LGPD", 0, Guid.NewGuid(), Guid.NewGuid(), null, "SEMPRE", null);
        ModeloFormularioView modelo = Modelo(FinalidadeFormulario.Inscricao, [Item("BAIXA_RENDA", 0), Item("QUILOMBOLA", 1)], termos: [termo]);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Descartados.Should().BeEquivalentTo(
            [new ParteDescartadaDto("ITEM", "BAIXA_RENDA", "FATO_DESATIVADO"), new ParteDescartadaDto("TERMO", "LGPD", "VERSAO_DE_TERMO_REMOVIDA")]);
        _processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("QUILOMBOLA");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O fato desativado que o processo já coleta não é vínculo novo e fica na cópia")]
    public async Task Handle_FatoDesativadoJaColetado_Mantem()
    {
        _processo.DefinirItens([FatoColetado.Criar("BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!])
            .IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("BAIXA_RENDA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Descartados.Should().BeEmpty();
        _processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("BAIXA_RENDA");
    }

    [Fact(DisplayName = "O que o item descartado cita não pede derivação nem distribuição de vagas")]
    public async Task Handle_ItemDescartadoQueCitaDerivado_AplicaSemExigirDerivacao()
    {
        FatoColetadoInput descartado = Item("BAIXA_RENDA", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("SEM_REGRA", "EM", JsonSerializer.SerializeToElement(new[] { "X" }))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0), descartado]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Descartados.Should().ContainSingle().Which.Codigo.Should().Be("BAIXA_RENDA");
    }

    [Fact(DisplayName = "A recusa de um item aponta a posição dele no modelo, mesmo depois de um descarte")]
    public async Task Handle_RecusaDepoisDeDescarte_ApontaAPosicaoNoModelo()
    {
        ModeloFormularioView modelo = Modelo(FinalidadeFormulario.Inscricao, [Item("BAIXA_RENDA", 0), Item("QUILOMBOLA", 1) with { Rotulo = "" }]);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("modelo.itens[1].rotulo");
    }

    [Fact(DisplayName = "Fora da inscrição, o fato que a inscrição coleta fica nela e sai da cópia")]
    public async Task Handle_HabilitacaoComFatoDaInscricao_MantemNaInscricao()
    {
        _processo.DefinirItens([FatoColetado.Criar("QUILOMBOLA", 0, "Quilombola", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!])
            .IsSuccess.Should().BeTrue();
        ModeloFormularioView modelo = Modelo(FinalidadeFormulario.Habilitacao, [Item("QUILOMBOLA", 0), Item("CERTIFICADO", 1)]);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosMantidosNaInscricao.Should().Equal("QUILOMBOLA");
        _processo.FatosColetados.Should().Contain(static f => f.FatoCodigo == "QUILOMBOLA" && f.Finalidade == FinalidadeFormulario.Inscricao)
            .And.Contain(static f => f.FatoCodigo == "CERTIFICADO" && f.Finalidade == FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "Na inscrição, o fato de outra finalidade é trazido e relatado")]
    public async Task Handle_InscricaoComFatoDeOutraFinalidade_TrazERelata()
    {
        _processo.DefinirItens(
            [FatoColetado.Criar("CERTIFICADO", 0, "Certificado", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!],
            finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosTrazidosParaAInscricao.Should().Equal("CERTIFICADO");
        _processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.Finalidade.Should().Be(FinalidadeFormulario.Inscricao);
    }

    [Fact(DisplayName = "O derivado que a cópia cita recebe as regras padrão do catálogo; o já configurado fica como está")]
    public async Task Handle_DerivadoCitado_CopiaOuMantem()
    {
        FatoColetadoInput exibido = Item("CERTIFICADO", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("PERFIL", "EM", JsonSerializer.SerializeToElement(new[] { "A" }))]] };

        Result<AplicacaoDeModeloDto> copia = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0), exibido]));
        Result<AplicacaoDeModeloDto> reaplicacao = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0), exibido]));

        copia.IsSuccess.Should().BeTrue(copia.Error?.Message);
        copia.Value!.DerivacoesCopiadas.Should().Equal("PERFIL");
        reaplicacao.Value!.DerivacoesMantidas.Should().Equal("PERFIL");
        _processo.RegrasDerivacao.Should().ContainSingle().Which.CodigoFato.Should().Be("PERFIL");
    }

    [Fact(DisplayName = "O derivado booleano citado é copiado com as regras padrão, que não contribuem código")]
    public async Task Handle_DerivadoBooleanoCitado_CopiaAsRegrasPadrao()
    {
        FatoColetadoInput porFlag = Item("CERTIFICADO", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("FLAG", "IGUAL", JsonSerializer.SerializeToElement(true))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0), porFlag]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _processo.RegrasDerivacao.Should().ContainSingle().Which.Booleano.Should().BeTrue();
    }

    [Fact(DisplayName = "Derivado sem regra padrão nem configuração é recusado")]
    public async Task Handle_DerivadoSemRegra_Recusa()
    {
        FatoColetadoInput porSemRegra = Item("CERTIFICADO", 1, "SELECAO_UNICA") with
        {
            Precondicao = [[new CondicaoPrecondicaoInput("SEM_REGRA", "EM", JsonSerializer.SerializeToElement(new[] { "X" }))]],
        };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0), porSemRegra]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.DerivadoSemRegra);
    }

    [Fact(DisplayName = "Os valores desativados no catálogo citados nas regras são recusados juntos, como vínculo novo")]
    public async Task Handle_ValoresDesativadosNasRegras_RecusaTodos()
    {
        FatoColetadoInput porAmarela = Item("QUILOMBOLA", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("AMARELA"))]] };
        FatoColetadoInput porIndigena = Item("CERTIFICADO", 2) with { Precondicao = [[new CondicaoPrecondicaoInput("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("INDIGENA"))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Habilitacao, [Item("COR_RACA", 0, "SELECAO_UNICA"), porAmarela, porIndigena]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(VinculoCatalogoErrorCodes.ValorDesativado, VinculoCatalogoErrorCodes.ValorDesativado);
    }

    [Fact(DisplayName = "A recusa do estado final aponta o termo pela posição no modelo, depois de um descarte")]
    public async Task Handle_RecusaDoAgregadoDepoisDeDescarte_ApontaAPosicaoNoModelo()
    {
        _processo.DefinirItens(
            [FatoColetado.Criar("CERTIFICADO", 0, "Certificado", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!],
            finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();
        Guid termoId = Guid.NewGuid();
        Guid versaoId = Guid.NewGuid();
        _termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new VersaoTermoConsentimentoView(termoId, versaoId, "Termo", "Texto", "Base", "LEITURA_E_ACEITE", "hash")]);
        TermoExigidoInput removido = new("LGPD", 0, Guid.NewGuid(), Guid.NewGuid(), null, "SEMPRE", null);
        TermoExigidoInput citaIsencao = new("IMAGEM", 1, termoId, versaoId, [[new CondicaoPrecondicaoInput("CERTIFICADO", "IGUAL", JsonSerializer.SerializeToElement(true))]], "SEMPRE", null);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(
            Modelo(FinalidadeFormulario.Habilitacao, [Item("QUILOMBOLA", 0)], termos: [removido, citaIsencao]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("modelo.termos[1]");
    }

    [Fact(DisplayName = "A modalidade citada pela regra padrão de um derivado também pede a distribuição de vagas antes")]
    public async Task Handle_ModalidadePelaRegraPadrao_PedeADistribuicao()
    {
        FatoColetadoInput porCotista = Item("QUILOMBOLA", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("COTISTA", "EM", JsonSerializer.SerializeToElement(new[] { "SIM" }))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0), porCotista]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.SemDistribuicaoDeVagas);
    }

    [Fact(DisplayName = "Modelo que cita a modalidade num processo sem distribuição de vagas pede a distribuição antes")]
    public async Task Handle_ModalidadeSemDistribuicaoDeVagas_PedeADistribuicao()
    {
        FatoColetadoInput porModalidade = Item("QUILOMBOLA", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("MODALIDADE", "EM", JsonSerializer.SerializeToElement(new[] { "AC" }))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0), porModalidade]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.SemDistribuicaoDeVagas);
    }

    [Fact(DisplayName = "O modelo de inscrição sem a seção dos dados básicos é copiado com ela em primeiro")]
    public async Task Handle_ModeloDeInscricaoSemASecao_CopiaComASecao()
    {
        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Etapas.MinBy(static e => e.Ordem)!.Codigo.Should().Be(ConjuntoBasicoDaInscricao.CodigoDaSecao);
        _processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
        _processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.Ordem.Should().Be(FormularioDeTeste.PrimeiraOrdemDeInscricao);
    }

    [Fact(DisplayName = "O modelo de inscrição gravado com a seção dos dados básicos e um item que cita um deles é aplicado")]
    public async Task Handle_ModeloDeInscricaoComASecao_CitaDadoBasico()
    {
        FatoColetadoInput porCor = Item("QUILOMBOLA", FormularioDeTeste.PrimeiraOrdemDeInscricao) with
        {
            Precondicao = [[new CondicaoPrecondicaoInput("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("PRETA"))]],
        };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [.. ConjuntoBasicoDaInscricao.Itens, porCor]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.FatosCitados.Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "O impedimento do item do modelo é copiado, e só a resposta anterior que ele cita é dependência do campo")]
    public async Task Handle_ItemComImpedimento_CopiaOImpedimento()
    {
        const string Mensagem = "Quem é quilombola e se declarou preto concorre pelo processo próprio.";
        FatoColetadoInput impedido = Item("QUILOMBOLA", FormularioDeTeste.PrimeiraOrdemDeInscricao) with
        {
            Impedimento = new ImpedimentoInput(
                [[
                    new CondicaoPrecondicaoInput("QUILOMBOLA", "IGUAL", JsonSerializer.SerializeToElement(true)),
                    new CondicaoPrecondicaoInput("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("PRETA")),
                ]],
                Mensagem),
        };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [.. ConjuntoBasicoDaInscricao.Itens, impedido]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FatoColetado copiado = _processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Subject;
        copiado.Impedimento!.Mensagem.Should().Be(Mensagem);
        copiado.FatosCitados.Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "O modelo de inscrição que altera um dado básico é recusado na posição do item no modelo")]
    public async Task Handle_ModeloDeInscricaoComDadoBasicoAlterado_Recusa()
    {
        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0), Item("DATA_NASCIMENTO", 1, "DATA")]));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "modelo.itens[1]",
            Error = new { Code = EstruturaFormularioErrorCodes.DadoBasicoAlterado },
        });
    }

    [Fact(DisplayName = "O modelo copia o conjunto de documentos exigidos para o processo, na fase de inscrição, e a nova aplicação troca só os da finalidade do modelo")]
    public async Task Handle_ModeloComDocumentos_CopiaETrocaOsDaFinalidade()
    {
        FaseCronograma fase = FaseDoCronograma("INSCRICAO", "RESULTADO_FINAL");
        DeclararAtendimentoEspecializado();
        ModeloFormularioView modelo = ModeloComDocumentos(
            DocumentoDoModelo("LAUDO_MEDICO", "PCD"), DocumentoDoModelo("LAUDO_MEDICO", "LACTANTE"), DocumentoDoModelo("CERTIDAO_NASCIMENTO", "LACTANTE"));
        DocumentoExigido daHabilitacao = DocumentoExigido.Criar(
            _processo.CronogramaFases.Single(static f => f.Codigo == "RESULTADO_FINAL").Id, Guid.CreateVersion7(), "HISTORICO_ENSINO_MEDIO", "Histórico", "ESCOLARIDADE",
            Aplicabilidade.Geral, obrigatorio: true, null, [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;
        _processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(daHabilitacao, 0).Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> primeira = await AplicarAsync(modelo);
        Result<AplicacaoDeModeloDto> segunda = await AplicarAsync(modelo);

        primeira.IsSuccess.Should().BeTrue(string.Join("; ", primeira.Errors.Select(static e => $"{e.Field}: {e.Error.Code}")));
        segunda.IsSuccess.Should().BeTrue();
        _processo.DocumentosExigidos.Select(static d => (d.TipoDocumentoCodigo, d.Finalidade)).Should().BeEquivalentTo(
        [
            ("HISTORICO_ENSINO_MEDIO", (FinalidadeFormulario?)null),
            ("LAUDO_MEDICO", FinalidadeFormulario.Inscricao),
            ("LAUDO_MEDICO", FinalidadeFormulario.Inscricao),
            ("CERTIDAO_NASCIMENTO", FinalidadeFormulario.Inscricao),
        ]);
        DocumentoExigido laudo = _processo.DocumentosExigidos.First(static d => d.TipoDocumentoCodigo == "LAUDO_MEDICO");
        laudo.ExigidoNaFaseId.Should().Be(fase.Id);
        laudo.ConsequenciaIndeferimento.Should().Be("REMOVE_VANTAGEM");
        laudo.Condicoes.Select(static c => (c.Fato, c.Clausula)).Should().BeEquivalentTo(
            [("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 0), ("CONDICAO_ATENDIMENTO", 0)]);
    }

    [Fact(DisplayName = "O derivado que só a condição de um documento do modelo cita recebe a regra padrão do catálogo")]
    public async Task Handle_DerivadoCitadoSoPeloDocumento_CopiaARegraPadrao()
    {
        FaseDoCronograma("INSCRICAO");
        DeclararAtendimentoEspecializado();

        DocumentoExigidoDoModeloInput documento = new(
            "INSCRICAO", "LAUDO_MEDICO", false,
            [[new CondicaoPrecondicaoInput("FLAG", "IGUAL", JsonSerializer.SerializeToElement(true))]], "REMOVE_VANTAGEM", []);

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(
            FinalidadeFormulario.Inscricao,
            [.. ConjuntoBasicoDaInscricao.Itens, Item("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 100), Item("QUILOMBOLA", 101)],
            documentos: [documento]));

        resultado.IsSuccess.Should().BeTrue(string.Join("; ", resultado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}")));
        resultado.Value!.DerivacoesCopiadas.Should().Contain("FLAG");
    }

    [Theory(DisplayName = "O filtro de oferta poda só a cláusula que afirma valor não ofertado; a de outro operador segue para o validador")]
    [InlineData("IGUAL", 0)]
    [InlineData("DIFERENTE", 1)]
    public void ClausulasOfertadas_PodaSoAAfirmacaoDeValorNaoOfertado(string operador, int esperadas)
    {
        IReadOnlyDictionary<string, DominioDeValores> ofertados = new Dictionary<string, DominioDeValores>(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = DominioDeValores.OfertadoPeloProcesso(["PCD"]),
        };
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> quando =
            [[new CondicaoPrecondicaoInput("CONDICAO_ATENDIMENTO", operador, JsonSerializer.SerializeToElement("LACTANTE"))]];

        AplicarModeloFormularioCommandHandler.ClausulasOfertadas(quando, ofertados)!.Should().HaveCount(esperadas);
    }

    [Fact(DisplayName = "O documento do modelo numa fase que o cronograma não tem é recusado, e nada é gravado")]
    public async Task Handle_DocumentoEmFaseInexistente_RecusaSemGravar()
    {
        FaseDoCronograma("RESULTADO_FINAL");
        DeclararAtendimentoEspecializado();

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(ModeloComDocumentos(DocumentoDoModelo("LAUDO_MEDICO", "PCD")));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "modelo.documentosExigidos[0].faseCodigo",
            Error = new { Code = AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente },
        });
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>O cronograma do processo com as fases dos códigos; só a de inscrição coleta inscrição. Devolve a primeira.</summary>
    private FaseCronograma FaseDoCronograma(params string[] codigos)
    {
        FaseCronograma[] fases =
        [
            .. codigos.Select((codigo, i) => FaseCronograma.Criar(
                i + 1, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: codigo == "INSCRICAO", coletaSolicitacaoIsencao: false, inicio: null, fim: null,
                produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
                bancasRequeridas: [], regraRecurso: null).Value!),
        ];
        _processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return fases[0];
    }

    private void DeclararAtendimentoEspecializado()
    {
        _catalogo.Add(Declarado("SOLICITA_ATENDIMENTO_ESPECIALIZADO"));
        _catalogo.Add(new FatoCandidatoView(Guid.CreateVersion7(), "CONDICAO_ATENDIMENTO", "Condição", null, "CATEGORICO", "DECLARADO", "MULTIVALORADO", ["PCD", "LACTANTE"], "INSCRICAO",
            "CAMPO_FORMULARIO:CONDICAO_ATENDIMENTO", [new("PCD", "PcD", 0, true), new("LACTANTE", "Lactante", 1, true)], "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL"));
        List<TipoDocumentoView> tipos = [.. new[] { "LAUDO_MEDICO", "CERTIDAO_NASCIMENTO" }.Select(static codigo => new TipoDocumentoView(Guid.CreateVersion7(), codigo, codigo, "SAUDE"))];
        _tiposDocumento.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => tipos.Single(t => t.Codigo == call.Arg<string>()));
        _tiposDocumento.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => tipos.Single(t => t.Id == call.Arg<Guid>()));
    }

    private static ModeloFormularioView ModeloComDocumentos(params DocumentoExigidoDoModeloInput[] documentos) =>
        Modelo(
            FinalidadeFormulario.Inscricao,
            [.. ConjuntoBasicoDaInscricao.Itens, Item("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 100), Item("CONDICAO_ATENDIMENTO", 101, "SELECAO_MULTIPLA")],
            documentos: documentos);

    private static DocumentoExigidoDoModeloInput DocumentoDoModelo(string tipo, string condicao) =>
        new("INSCRICAO", tipo, Obrigatorio: false,
            [[
                new CondicaoPrecondicaoInput("SOLICITA_ATENDIMENTO_ESPECIALIZADO", "IGUAL", JsonSerializer.SerializeToElement(true)),
                new CondicaoPrecondicaoInput("CONDICAO_ATENDIMENTO", "IGUAL", JsonSerializer.SerializeToElement(condicao)),
            ]],
            "REMOVE_VANTAGEM", []);

    private Task<Result<AplicacaoDeModeloDto>> AplicarAsync(ModeloFormularioView modelo)
    {
        _modelos.ObterAsync(modelo.Id, Arg.Any<CancellationToken>()).Returns(modelo);
        return AplicarAsync(modelo.Id);
    }

    [Fact(DisplayName = "Modelo que cita a opção pelas cotas num processo sem cota da lei ofertada é recusado, sem copiar a opção")]
    public async Task Handle_OpcaoPelasCotasSemCotaOfertada_Recusa()
    {
        Ofertar(Ofertada("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula));
        FatoColetadoInput porOpcao = Item("QUILOMBOLA", 1) with { Precondicao = [[new CondicaoPrecondicaoInput(OpcaoPelasCotasDaLei.Fato, "IGUAL", JsonSerializer.SerializeToElement(true))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0), porOpcao]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.SemCotaDaLeiOfertada);
        _processo.RegrasDerivacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com cota da lei ofertada, a opção pelas cotas vem da oferta e o que ela cita recebe a regra padrão")]
    public async Task Handle_OpcaoPelasCotasComCotaOfertada_CopiaODeQueElaDepende()
    {
        Ofertar(
            Ofertada("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Ofertada("LI_EP", NaturezaLegalModalidade.CotaReservada, Quando(OpcaoPelasCotasDaLei.Fato, true)));
        FatoColetadoInput porOpcao = Item("QUILOMBOLA", 2) with { Precondicao = [[new CondicaoPrecondicaoInput(OpcaoPelasCotasDaLei.Fato, "IGUAL", JsonSerializer.SerializeToElement(true))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(
            FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0), Item(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 1), porOpcao]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.DerivacoesMantidas.Should().Contain(OpcaoPelasCotasDaLei.Fato);
        resultado.Value.DerivacoesCopiadas.Should().Equal(OrigemEscolar.FatoEgresso);
        _processo.RegrasDerivacao.Select(static r => r.CodigoFato).Should().Equal(OrigemEscolar.FatoEgresso);
    }

    private void Ofertar(params ModalidadeSelecionada[] modalidades)
    {
        _catalogo.Add(new FatoCandidatoView(Guid.CreateVersion7(), OpcaoPelasCotasDaLei.Fato, "Optou", null, "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            $"REGRA_DERIVACAO:{OpcaoPelasCotasDaLei.Fato}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL"));
        _catalogo.Add(new FatoCandidatoView(Guid.CreateVersion7(), OrigemEscolar.FatoEgresso, "Egresso", null, "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            $"REGRA_DERIVACAO:{OrigemEscolar.FatoEgresso}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL"));
        _catalogo.Add(Declarado(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica));
        _fatos.ListarRegrasPadraoAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, IReadOnlyList<RegraDerivacao>>(StringComparer.Ordinal)
        {
            [OpcaoPelasCotasDaLei.Fato] = OpcaoPelasCotasDaLei.RegrasDaOpcao,
            [OrigemEscolar.FatoEgresso] = [RegraDerivacao.CriarBooleana(Quando("CERTIFICADO", true))],
        });
        _processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 10 * modalidades.Length, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
                null, null, modalidades, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static ModalidadeSelecionada Ofertada(string codigo, NaturezaLegalModalidade natureza, PredicadoDnf regra) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.ResidualDoVo : ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, regra, null, "base legal", quantidadeDeclarada: 10).Value!;

    private Task<Result<AplicacaoDeModeloDto>> AplicarAsync(Guid modeloId) =>
        AplicarModeloFormularioCommandHandler.Handle(
            new AplicarModeloFormularioCommand(_processo.Id, modeloId, PrecondicaoIfMatch.Ausente),
            _repository, _modelos, _fatos, _termos, _tiposDocumento, _modelosDeDocumento, _unitOfWork, CancellationToken.None);

    private static ModeloFormularioView Modelo(
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetadoInput> itens,
        IReadOnlyList<TermoExigidoInput>? termos = null,
        IReadOnlyList<string>? pressupostos = null,
        IReadOnlyList<GrupoColetadoInput>? grupos = null,
        IReadOnlyList<DocumentoExigidoDoModeloInput>? documentos = null) =>
        new(Guid.NewGuid(), "MODELO", "Modelo", null, EstruturaFormulario.ParaToken(finalidade), null, true,
            new ConteudoDoModeloInput(
                "Formulário",
                [new EtapaFormularioInput("DADOS", 0, "SECAO", null, "Dados", null, null), new EtapaFormularioInput("REVISAO", 1, "BLOCO", "REVISAO_E_ACEITE", "Revisão", null, null)],
                itens, termos ?? [], pressupostos ?? [], grupos ?? [], documentos));

    private static GrupoColetadoInput Composicao(params FatoColetadoInput[] campos) =>
        new("COMPOSICAO_FAMILIAR", 1, "Composição familiar", "DADOS", 1, 10, null, "SEMPRE", null, campos);

    private static FatoColetadoInput Campo(string fato, int ordem) => Item(fato, ordem) with { EtapaCodigo = null };

    private static FatoColetadoInput Item(string fato, int ordem, string tipo = "BOOLEANO") =>
        new(fato, ordem, fato, tipo, "SEMPRE", null, EtapaCodigo: "DADOS");

    private static FatoCandidatoView Declarado(string codigo) =>
        new(Guid.CreateVersion7(), codigo, codigo, null, "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO", $"CAMPO_FORMULARIO:{codigo}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL");

    private static PredicadoDnf Quando(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
}
