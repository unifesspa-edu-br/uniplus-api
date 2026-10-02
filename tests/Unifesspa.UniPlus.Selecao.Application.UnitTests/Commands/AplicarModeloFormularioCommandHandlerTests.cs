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
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
    private readonly ProcessoSeletivo _processo = ProcessoSeletivo.Criar(
        "PS Modelo", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private readonly List<FatoCandidatoView> _catalogo =
    [
        Declarado("QUILOMBOLA"),
        Declarado("CERTIFICADO"),
        Declarado("DATA_NASCIMENTO") with { Dominio = "DATA" },
        Declarado("MAIOR_IDADE") with { Escopo = "MEMBRO_GRUPO" },
        Declarado("SOB_GUARDA") with { Escopo = "MEMBRO_GRUPO", Ativo = false },
        Declarado("BAIXA_RENDA") with { Ativo = false },
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR", ["PRETA", "AMARELA", "INDIGENA"], "INSCRICAO",
            "CAMPO_INSCRICAO:COR_RACA", [new("PRETA", "Preta", 0, true), new("AMARELA", "Amarela", 1, false), new("INDIGENA", "Indígena", 2, false)], "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "PERFIL", "Perfil", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["A", "B"], "INSCRICAO",
            "REGRA_DERIVACAO:PERFIL", [new("A", "A", 0, true), new("B", "B", 1, true)], "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "FLAG", "Flag", null, "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO", "REGRA_DERIVACAO:FLAG", null, null, Ativo: true),
        new(Guid.CreateVersion7(), "FLAG_SEM_REGRA", "Flag sem regra", null, "BOOLEANO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            "REGRA_DERIVACAO:FLAG_SEM_REGRA", null, null, Ativo: true),
        new(Guid.CreateVersion7(), "COTISTA", "Cotista", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["SIM"], "INSCRICAO",
            "REGRA_DERIVACAO:COTISTA", [new("SIM", "Sim", 0, true)], "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "SEM_REGRA", "Sem regra", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", ["X"], "INSCRICAO",
            "REGRA_DERIVACAO:SEM_REGRA", [new("X", "X", 0, true)], "GLOBAL", Ativo: true),
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", null, "INSCRICAO",
            "REGRA_DERIVACAO:MODALIDADE", null, "MODALIDADE", Ativo: true),
        new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", null, "NUMERICO", "DERIVADO", "ESCALAR", null, "INSCRICAO",
            "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true),
    ];

    public AplicarModeloFormularioCommandHandlerTests()
    {
        _repository.ObterParaMutacaoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns(_processo);
        _fatos.ListarAsync(Arg.Any<CancellationToken>()).Returns(_ => _catalogo);
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

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, itens, termos: [termo]));

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
        _processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Equal("QUILOMBOLA");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O fato desativado que o processo já coleta não é vínculo novo e fica na cópia")]
    public async Task Handle_FatoDesativadoJaColetado_Mantem()
    {
        _processo.DefinirItens([FatoColetado.Criar("BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!])
            .IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("BAIXA_RENDA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Descartados.Should().BeEmpty();
        _processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Equal("BAIXA_RENDA");
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
        _processo.DefinirItens([FatoColetado.Criar("QUILOMBOLA", 0, "Quilombola", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!])
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
            [FatoColetado.Criar("CERTIFICADO", 0, "Certificado", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!],
            finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("CERTIFICADO", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosTrazidosParaAInscricao.Should().Equal("CERTIFICADO");
        _processo.FatosColetados.Should().ContainSingle().Which.Finalidade.Should().Be(FinalidadeFormulario.Inscricao);
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

    [Fact(DisplayName = "Derivado booleano e derivado sem regra padrão nem configuração são recusados")]
    public async Task Handle_DerivadoBooleanoOuSemRegra_Recusa()
    {
        FatoColetadoInput porFlag = Item("CERTIFICADO", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("FLAG_SEM_REGRA", "IGUAL", JsonSerializer.SerializeToElement(true))]] };
        FatoColetadoInput porSemRegra = Item("COR_RACA", 2, "SELECAO_UNICA") with
        {
            Precondicao = [[new CondicaoPrecondicaoInput("SEM_REGRA", "EM", JsonSerializer.SerializeToElement(new[] { "X" }))]],
        };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("QUILOMBOLA", 0), porFlag, porSemRegra]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(
            [AplicacaoDeModeloErrorCodes.DerivadoBooleano, AplicacaoDeModeloErrorCodes.DerivadoSemRegra]);
    }

    [Fact(DisplayName = "Os valores desativados no catálogo citados nas regras são recusados juntos, como vínculo novo")]
    public async Task Handle_ValoresDesativadosNasRegras_RecusaTodos()
    {
        FatoColetadoInput porAmarela = Item("QUILOMBOLA", 1) with { Precondicao = [[new CondicaoPrecondicaoInput("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("AMARELA"))]] };
        FatoColetadoInput porIndigena = Item("CERTIFICADO", 2) with { Precondicao = [[new CondicaoPrecondicaoInput("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("INDIGENA"))]] };

        Result<AplicacaoDeModeloDto> resultado = await AplicarAsync(Modelo(FinalidadeFormulario.Inscricao, [Item("COR_RACA", 0, "SELECAO_UNICA"), porAmarela, porIndigena]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(VinculoCatalogoErrorCodes.ValorDesativado, VinculoCatalogoErrorCodes.ValorDesativado);
    }

    [Fact(DisplayName = "A recusa do estado final aponta o termo pela posição no modelo, depois de um descarte")]
    public async Task Handle_RecusaDoAgregadoDepoisDeDescarte_ApontaAPosicaoNoModelo()
    {
        _processo.DefinirItens(
            [FatoColetado.Criar("CERTIFICADO", 0, "Certificado", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!],
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

    private Task<Result<AplicacaoDeModeloDto>> AplicarAsync(ModeloFormularioView modelo)
    {
        _modelos.ObterAsync(modelo.Id, Arg.Any<CancellationToken>()).Returns(modelo);
        return AplicarAsync(modelo.Id);
    }

    private Task<Result<AplicacaoDeModeloDto>> AplicarAsync(Guid modeloId) =>
        AplicarModeloFormularioCommandHandler.Handle(
            new AplicarModeloFormularioCommand(_processo.Id, modeloId, PrecondicaoIfMatch.Ausente),
            _repository, _modelos, _fatos, _termos, _unitOfWork, CancellationToken.None);

    private static ModeloFormularioView Modelo(
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetadoInput> itens,
        IReadOnlyList<TermoExigidoInput>? termos = null,
        IReadOnlyList<string>? pressupostos = null,
        IReadOnlyList<GrupoColetadoInput>? grupos = null) =>
        new(Guid.NewGuid(), "MODELO", "Modelo", null, EstruturaFormulario.ParaToken(finalidade), null, true,
            new ConteudoDoModeloInput(
                "Formulário",
                [new EtapaFormularioInput("DADOS", 0, "SECAO", null, "Dados", null, null), new EtapaFormularioInput("REVISAO", 1, "BLOCO", "REVISAO_E_ACEITE", "Revisão", null, null)],
                itens, termos ?? [], pressupostos ?? [], grupos ?? []));

    private static GrupoColetadoInput Composicao(params FatoColetadoInput[] campos) =>
        new("COMPOSICAO_FAMILIAR", 1, "Composição familiar", "DADOS", 1, 10, null, "SEMPRE", null, campos);

    private static FatoColetadoInput Campo(string fato, int ordem) => Item(fato, ordem) with { EtapaCodigo = null };

    private static FatoColetadoInput Item(string fato, int ordem, string tipo = "BOOLEANO") =>
        new(fato, ordem, fato, tipo, "SEMPRE", null, EtapaCodigo: "DADOS");

    private static FatoCandidatoView Declarado(string codigo) =>
        new(Guid.CreateVersion7(), codigo, codigo, null, "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO", $"CAMPO_INSCRICAO:{codigo}", null, null, Ativo: true);

    private static PredicadoDnf Quando(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
}
