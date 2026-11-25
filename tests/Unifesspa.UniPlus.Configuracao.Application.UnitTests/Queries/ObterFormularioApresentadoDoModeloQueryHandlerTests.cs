namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O apresentado do modelo projeta o conteúdo como aplicado, com o catálogo vivo e o cadastro
/// institucional, no mesmo formato que o certame divulgado serve.
/// </summary>
public sealed class ObterFormularioApresentadoDoModeloQueryHandlerTests
{
    private const string Finalidade = "Verificação dos requisitos do processo seletivo.";

    private static readonly EtapaDoModelo Dados = new("DADOS", 0, TipoEtapaFormulario.Secao, null, "Dados", "Dados pessoais", "Atenção", null);
    private static readonly EtapaDoModelo Revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    private readonly IModeloFormularioRepository _repository = Substitute.For<IModeloFormularioRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly ICondicaoAtendimentoRepository _condicoes = Substitute.For<ICondicaoAtendimentoRepository>();
    private readonly IRecursoAcessibilidadeRepository _recursos = Substitute.For<IRecursoAcessibilidadeRepository>();
    private readonly ITipoDeficienciaRepository _tiposDeficiencia = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IBaseLegalBonusRegionalRepository _basesDoBonus = Substitute.For<IBaseLegalBonusRegionalRepository>();
    private readonly ITermoConsentimentoRepository _termos = Substitute.For<ITermoConsentimentoRepository>();
    private readonly OpcoesDoCadastroInstitucional _cadastro;

    public ObterFormularioApresentadoDoModeloQueryHandlerTests()
    {
        _cadastro = new OpcoesDoCadastroInstitucional(_condicoes, _recursos, _tiposDeficiencia, _basesDoBonus);
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => []);
    }

    [Fact(DisplayName = "Modelo inexistente não tem apresentado nem consulta o catálogo")]
    public async Task Handle_ModeloInexistente_DevolveNuloSemConsultarOCatalogo()
    {
        FormularioApresentado? resultado = await ObterAsync(Guid.NewGuid());

        resultado.Should().BeNull();
        await _fatos.DidNotReceiveWithAnyArgs().ListarTodosAsync(default);
    }

    [Fact(DisplayName = "As seções e os campos saem na ordem, com a finalidade e o título do modelo")]
    public async Task Handle_Modelo_ProjetaSecoesECamposNaOrdem()
    {
        ModeloFormulario modelo = Modelo(itens: [Item("SEGUNDO", 1), Item("PRIMEIRO", 0)]);

        FormularioApresentado resultado = (await ObterAsync(modelo))!;

        resultado.Finalidade.Should().Be("HABILITACAO");
        resultado.Titulo.Should().Be("Habilitação");
        resultado.Coleta.Itens.Select(static c => (c.FatoCodigo, c.Ordem, c.EtapaCodigo)).Should().Equal(("PRIMEIRO", 0, "DADOS"), ("SEGUNDO", 1, "DADOS"));
        resultado.Etapas.Should().Equal(
            new SecaoRenderizavel("DADOS", "DADOS", 0, "SECAO", null, new ApresentacaoDaEtapaDto("Dados", "Dados pessoais", "Atenção")),
            new SecaoRenderizavel("REVISAO", null, 1, "BLOCO", "REVISAO_E_ACEITE", new ApresentacaoDaEtapaDto("Revisão", null, null)));
    }

    [Fact(DisplayName = "O campo de seleção global oferta os valores ativos do catálogo, e o que o processo declara sai nulo")]
    public async Task Handle_CampoDeSelecao_OfertaOsValoresDoCatalogo()
    {
        FatoCandidato cor = Fato("COR_RACA", FonteValoresFato.Catalogo);
        cor.AdicionarValorDominio("PRETA", "Preta", 0, ativo: true).IsSuccess.Should().BeTrue();
        cor.AdicionarValorDominio("AMARELA", "Amarela", 1, ativo: false).IsSuccess.Should().BeTrue();
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [cor, Fato("MODALIDADE", FonteValoresFato.OpcoesDoProcesso)]);

        FormularioApresentado resultado = (await ObterAsync(Modelo(itens: [Item("COR_RACA", 0), Item("MODALIDADE", 1)])))!;

        resultado.Coleta.Itens.Single(static c => c.FatoCodigo == "COR_RACA").ValoresSelecionaveis.Should().Equal(new ValorSelecionavel("PRETA", "Preta", 0));
        resultado.Coleta.Itens.Single(static c => c.FatoCodigo == "MODALIDADE").ValoresSelecionaveis.Should().BeNull();
    }

    [Fact(DisplayName = "O campo do cadastro institucional oferta o cadastro vivo, que o processo recorta")]
    public async Task Handle_CampoDoCadastroInstitucional_OfertaOCadastroVivo()
    {
        _condicoes.ListarVivosParaLeituraAsync(Arg.Any<CancellationToken>()).Returns([CondicaoAtendimentoEspecializado.Criar("TEA", "Autismo", null).Value!]);
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [Fato("CONDICAO_ATENDIMENTO", FonteValoresFato.OpcoesDoProcesso)]);

        FormularioApresentado resultado = (await ObterAsync(Modelo(itens: [Item("CONDICAO_ATENDIMENTO", 0)])))!;

        resultado.Coleta.Itens.Single().ValoresSelecionaveis.Should().Equal(new ValorSelecionavel("TEA", "Autismo", 0));
    }

    [Fact(DisplayName = "O termo sai com o conteúdo da versão escolhida, na ordem do modelo")]
    public async Task Handle_Termo_SaiComOConteudoDaVersaoEscolhida()
    {
        TermoConsentimento termo = TermoPromovido(out TermoConsentimentoVersao versao);
        _termos.ObterPorIdParaLeituraAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);
        ModeloFormulario modelo = Modelo(termos: [new("ACEITE_DADOS", 3, termo.Id, versao.Id, new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre))]);

        FormularioApresentado resultado = (await ObterAsync(modelo))!;

        resultado.Termos.Should().ContainSingle().Which.Should().Be(new TermoApresentado(
            "ACEITE_DADOS",
            "ACEITE_DADOS",
            3,
            termo.Id,
            versao.Id,
            new ConteudoDoTermoDto(termo.Nome, versao.Texto, versao.BaseLegal, CodigoComRotulo<FormaAceite>.De(FormaAceite.RegistroSemEnderecoIp), versao.Hash)));
    }

    [Fact(DisplayName = "O conteúdo do termo serializa a forma de aceite com código e rótulo")]
    public void ConteudoDoTermo_Serializado_ExibeCodigoERotulo()
    {
        ConteudoDoTermoDto conteudo = new("Termo", "Texto", "Base", CodigoComRotulo<FormaAceite>.De(FormaAceite.RegistroSemEnderecoIp), "hash");
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(conteudo, JsonSerializerOptions.Web));
        JsonElement forma = json.RootElement.GetProperty("formaAceite");
        forma.GetProperty("codigo").GetString().Should().Be("REGISTRO_SEM_ENDERECO_IP");
        forma.GetProperty("rotulo").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "A versão do termo que não está mais no catálogo é defeito do cadastro, não estado a esconder")]
    public async Task Handle_VersaoDoTermoAusente_Lanca()
    {
        TermoConsentimento termo = TermoPromovido(out _);
        _termos.ObterPorIdParaLeituraAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);
        ModeloFormulario modelo = Modelo(termos: [new("ACEITE_DADOS", 0, termo.Id, Guid.NewGuid(), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre))]);

        Func<Task> obter = () => ObterAsync(modelo);

        await obter.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ACEITE_DADOS*");
    }

    [Fact(DisplayName = "O grupo repetível sai com os campos de cada ocorrência na ordem")]
    public async Task Handle_Grupo_ProjetaOsSubcamposNaOrdem()
    {
        GrupoDoModelo familia = new(
            "FAMILIA", 1, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, 3, IncluiCandidato: false), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre),
            [Subitem("SEGUNDO", 1), Subitem("PRIMEIRO", 0)]);

        FormularioApresentado resultado = (await ObterAsync(Modelo(grupos: [familia])))!;

        GrupoApresentado grupo = resultado.Coleta.Grupos.Should().ContainSingle().Which;
        (grupo.Codigo, grupo.Ordem, grupo.EtapaCodigo, grupo.Rotulo, grupo.Ocorrencias)
            .Should().Be(("FAMILIA", 1, "DADOS", "Composição familiar", new OcorrenciasDoGrupoDto(1, 3, false)));
        grupo.Subitens.Select(static c => c.FatoCodigo).Should().Equal("PRIMEIRO", "SEGUNDO");
    }

    [Fact(DisplayName = "O modelo de inscrição sai com o conjunto básico do candidato, como o formulário do processo o terá")]
    public async Task Handle_ModeloDeInscricao_SaiComOConjuntoBasico()
    {
        ModeloFormulario modelo = Modelo(FinalidadeFormulario.Inscricao, itens: [Item("CERTIFICADO", 100)]);

        FormularioApresentado resultado = (await ObterAsync(modelo))!;

        resultado.Finalidade.Should().Be("INSCRICAO");
        resultado.Coleta.Itens.Should().Contain(static c => c.FatoCodigo == "CERTIFICADO").And.HaveCountGreaterThan(1);
    }

    [Fact(DisplayName = "As regras recortadas citam os pressupostos que o modelo declara")]
    public async Task Handle_PressupostoDoModelo_SaiNosPressupostos()
    {
        ModeloFormulario modelo = Modelo(itens: [Item("CERTIFICADO", 0).ComExibicao(Quando("CONCLUSAO_REGULAR", true))], pressupostos: ["CONCLUSAO_REGULAR"]);

        FormularioApresentado resultado = (await ObterAsync(modelo))!;

        resultado.Pressupostos.Fatos.Select(static p => p.FatoCodigo).Should().Equal("CONCLUSAO_REGULAR");
        resultado.Pressupostos.DataReferenciaFatos.Should().BeNull();
        resultado.Pressupostos.FusoDoEnvio.Should().BeNull();
    }

    private Task<FormularioApresentado?> ObterAsync(ModeloFormulario modelo)
    {
        _repository.ObterPorIdParaLeituraAsync(modelo.Id, Arg.Any<CancellationToken>()).Returns(modelo);
        return ObterAsync(modelo.Id);
    }

    private Task<FormularioApresentado?> ObterAsync(Guid id) =>
        ObterFormularioApresentadoDoModeloQueryHandler.Handle(
            new ObterFormularioApresentadoDoModeloQuery(id), _repository, _fatos, _cadastro, _termos, CancellationToken.None);

    private static TermoConsentimento TermoPromovido(out TermoConsentimentoVersao versao)
    {
        DateTimeOffset agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);
        TermoConsentimento termo = TermoConsentimento.Criar("Termo LGPD", "Texto do termo", "Lei 13.709/2018", "REGISTRO_SEM_ENDERECO_IP").Value!;
        termo.MarcarRevisado("usuario.revisor", agora).IsSuccess.Should().BeTrue();
        versao = termo.Promover("usuario.promotor", agora).Value!;
        return termo;
    }

    private static ModeloFormulario Modelo(
        FinalidadeFormulario finalidade = FinalidadeFormulario.Habilitacao,
        IReadOnlyList<ItemDoModelo>? itens = null,
        IReadOnlyList<TermoDoModelo>? termos = null,
        IReadOnlyList<string>? pressupostos = null,
        IReadOnlyList<GrupoDoModelo>? grupos = null)
    {
        Kernel.Results.Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "MODELO_DE_TESTE", "Modelo de teste", null, finalidade, null,
            new ConteudoDoModelo("Habilitação", [Dados, Revisao], itens ?? [], termos ?? [], pressupostos ?? [], grupos ?? []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        return modelo.Value!;
    }

    private static ItemDoModelo Item(string fato, int ordem) =>
        new(fato, ordem, "DADOS", new ApresentacaoDoCampo(fato, TipoDeCampo.EscolhaUnica), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));

    private static ItemDoModelo Subitem(string fato, int ordem) => Item(fato, ordem) with { EtapaCodigo = null };

    private static PredicadoDnf Quando(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    private static FatoCandidato Fato(string codigo, FonteValoresFato fonte) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, fonte, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
}
