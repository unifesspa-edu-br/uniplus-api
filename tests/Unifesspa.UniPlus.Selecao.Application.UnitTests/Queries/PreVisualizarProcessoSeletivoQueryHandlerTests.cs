namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A pré-visualização do processo por perfil (UNI-REQ-0144, UNI-REQ-0145, UNI-REQ-0064): um processo
/// de Medicina com a inscrição, a habilitação com a composição familiar e as exigências da
/// habilitação, avaliado para três candidatos.
/// </summary>
public sealed class PreVisualizarProcessoSeletivoQueryHandlerTests
{
    private const string Composicao = "COMPOSICAO_FAMILIAR";

    private readonly ProcessoSeletivo _processo = ProcessoDeMedicina();

    [Fact(DisplayName = "Cotista indígena rural com menor sob guarda: pertencimento, trabalhador rural e uma certidão por menor")]
    public async Task Handle_CotistaIndigenaRuralComMenorSobGuarda_ExigeOsDocumentosDaCota()
    {
        PreVisualizacaoDoProcessoDto previa = await PreVisualizarAsync(Perfil(
            nacionalidade: "NATO",
            corRaca: "INDIGENA",
            convocacao: "LB_PPI",
            membros:
            [
                Membro("CANDIDATO", "URBANO", menorSobGuarda: false),
                Membro("IRMAO", "RURAL", menorSobGuarda: true),
            ]));

        Exigidos(previa).Should().BeEquivalentTo(["PERTENCIMENTO_INDIGENA", "TRABALHADOR_RURAL", "RG", "CERTIDAO_GUARDA@IRMAO", "COMPROVANTE_DA_MODALIDADE"]);
        previa.Documentos.Should().OnlyContain(static d => d.Finalidade!.Codigo == "INSCRICAO", "cada documento traz o formulário em que é apresentado");
    }

    [Fact(DisplayName = "Ampla concorrência: nenhum documento de cota; a identidade e o comprovante da modalidade derivada")]
    public async Task Handle_AmplaConcorrencia_SoODocumentoDeIdentidade()
    {
        PreVisualizacaoDoProcessoDto previa = await PreVisualizarAsync(Perfil(
            nacionalidade: "NATO",
            corRaca: "INDIGENA",
            convocacao: "AC",
            membros: [Membro("CANDIDATO", "URBANO", menorSobGuarda: false)]));

        Exigidos(previa).Should().BeEquivalentTo(["RG", "COMPROVANTE_DA_MODALIDADE"]);
    }

    [Fact(DisplayName = "Estrangeiro: passaporte exigido, RG não; não vê RG nem naturalidade, e o CPF é obrigatório")]
    public async Task Handle_Estrangeiro_PassaporteSemRgNemNaturalidade()
    {
        PreVisualizacaoDoProcessoDto previa = await PreVisualizarAsync(Perfil(
            nacionalidade: "ESTRANGEIRO",
            corRaca: "BRANCA",
            convocacao: "AC",
            membros: [Membro("CANDIDATO", "URBANO", menorSobGuarda: false)]));

        Exigidos(previa).Should().BeEquivalentTo(["PASSAPORTE"]);
        previa.Documentos.Where(static d => d.TipoDocumentoCodigo == "CERTIDAO_GUARDA")
            .Should().ContainSingle("a composição familiar não aparece a quem se declarou branco").Which.Situacao.Should().Be("NAO_EXIGIDO");
        ConferirQueORgEOPassaporteSaoAlternativas(previa);
        FormularioSimuladoDto inscricao = previa.Formularios.Single(static f => f.Finalidade.Codigo == "INSCRICAO");
        inscricao.Itens.Single(static i => i.FatoCodigo == "RG_NUMERO").Visivel.Codigo.Should().Be("FALSO");
        inscricao.Itens.Single(static i => i.FatoCodigo == "NATURALIDADE_UF").Visivel.Codigo.Should().Be("FALSO");
        inscricao.Itens.Single(static i => i.FatoCodigo == "CPF").Obrigatorio.Codigo.Should().Be("VERDADEIRO");
    }

    private static void ConferirQueORgEOPassaporteSaoAlternativas(PreVisualizacaoDoProcessoDto previa)
    {
        DocumentoSimuladoDto rg = previa.Documentos.Single(static d => d.TipoDocumentoCodigo == "RG");
        rg.Alternativas.Should().ContainSingle("o RG está num grupo E dentro do grupo de alternativas da identidade")
            .Which.Should().Be(previa.Documentos.Single(static d => d.TipoDocumentoCodigo == "PASSAPORTE").Alternativas.Single());
    }

    [Fact(DisplayName = "Sem saber se a composição familiar aparece, o documento por membro sai indeterminado, sem ocorrência")]
    public async Task Handle_ComposicaoDeExibicaoIndeterminada_DocumentoPorMembroIndeterminado()
    {
        PreVisualizacaoDoProcessoInput perfil = Perfil(
            nacionalidade: "NATO", corRaca: "INDIGENA", convocacao: "LB_PPI", membros: [Membro("IRMAO", "URBANO", menorSobGuarda: true)]);
        Dictionary<string, JsonElement> semCorRaca = new(perfil.Respostas!, StringComparer.Ordinal);
        semCorRaca.Remove("COR_RACA");

        PreVisualizacaoDoProcessoDto previa = await PreVisualizarAsync(perfil with { Respostas = semCorRaca });

        previa.Documentos.Where(static d => d.TipoDocumentoCodigo == "CERTIDAO_GUARDA")
            .Should().ContainSingle().Which.Should().BeEquivalentTo(new { Situacao = "INDETERMINADO", OcorrenciaId = (string?)null });
    }

    [Fact(DisplayName = "Com a composição familiar visível e sem resposta, o documento por membro sai indeterminado, sem ocorrência")]
    public async Task Handle_ComposicaoVisivelSemResposta_DocumentoPorMembroIndeterminado()
    {
        PreVisualizacaoDoProcessoDto previa = await PreVisualizarAsync(Perfil(
            nacionalidade: "NATO", corRaca: "INDIGENA", convocacao: "LB_PPI", membros: null));

        previa.Documentos.Where(static d => d.TipoDocumentoCodigo == "CERTIDAO_GUARDA")
            .Should().ContainSingle().Which.Should().BeEquivalentTo(new { Situacao = "INDETERMINADO", OcorrenciaId = (string?)null });
    }

    [Fact(DisplayName = "Com a composição familiar visível e a lista vazia declarada, o documento por membro não é exigido")]
    public async Task Handle_ComposicaoVisivelComListaVazia_DocumentoPorMembroNaoExigido()
    {
        Result<PreVisualizacaoDoProcessoDto> resultado = await HandleAsync(
            Perfil(nacionalidade: "NATO", corRaca: "INDIGENA", convocacao: "LB_PPI", membros: []),
            ProcessoDeMedicina(minimoDaComposicao: 0));

        resultado.Value!.Documentos.Where(static d => d.TipoDocumentoCodigo == "CERTIDAO_GUARDA")
            .Should().ContainSingle().Which.Should().BeEquivalentTo(new { Situacao = "NAO_EXIGIDO", OcorrenciaId = (string?)null });
    }

    [Fact(DisplayName = "Com a composição familiar oculta e sem resposta, o documento por membro não é exigido")]
    public async Task Handle_ComposicaoOcultaSemResposta_DocumentoPorMembroNaoExigido()
    {
        PreVisualizacaoDoProcessoDto previa = await PreVisualizarAsync(Perfil(
            nacionalidade: "NATO", corRaca: "BRANCA", convocacao: "AC", membros: null));

        previa.Documentos.Where(static d => d.TipoDocumentoCodigo == "CERTIDAO_GUARDA")
            .Should().ContainSingle().Which.Should().BeEquivalentTo(new { Situacao = "NAO_EXIGIDO", OcorrenciaId = (string?)null });
    }

    [Fact(DisplayName = "A ocorrência sem identidade, ou com identidade repetida no grupo, é recusada no campo dela")]
    public async Task Handle_OcorrenciaSemIdentidadePropria_Recusa()
    {
        PreVisualizacaoDoProcessoInput perfil = Perfil(
            nacionalidade: "NATO", corRaca: "BRANCA", convocacao: "AC",
            membros: [Membro("CANDIDATO", "URBANO", false), Membro("CANDIDATO", "RURAL", false), Membro(" ", "RURAL", false)]);

        Result<PreVisualizacaoDoProcessoDto> resultado = await HandleAsync(perfil);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ($"grupos.{Composicao}[1].id", PreVisualizarProcessoSeletivoQueryHandler.OcorrenciaInvalida),
            ($"grupos.{Composicao}[2].id", PreVisualizarProcessoSeletivoQueryHandler.OcorrenciaInvalida),
        ]);
    }

    [Theory(DisplayName = "A resposta que cumpre o impedimento marca o item como impedido; o item traz a mensagem ao candidato")]
    [InlineData(true, "VERDADEIRO")]
    [InlineData(false, "FALSO")]
    public async Task Handle_ItemComImpedimento_MarcaImpedidoComAMensagem(bool vinculo, string impedido)
    {
        const string Mensagem = "Quem tem vínculo com o PARFOR não pode se inscrever neste processo.";
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PSIQ 2027");
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [.. FormularioDeTeste.DadosBasicos(), VinculoParforComImpedimento(Mensagem)])
            .IsSuccess.Should().BeTrue();

        Result<PreVisualizacaoDoProcessoDto> resultado = await HandleAsync(
            new(new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["VINCULO_PARFOR"] = Json(vinculo) }, null, null, null), processo);

        resultado.Value!.Formularios.Single(static f => f.Finalidade.Codigo == "INSCRICAO").Itens.Single(static i => i.FatoCodigo == "VINCULO_PARFOR")
            .Should().BeEquivalentTo(new { Impedido = new { Codigo = impedido }, MensagemDoImpedimento = Mensagem });
    }

    private static FatoColetado VinculoParforComImpedimento(string mensagem) =>
        FatoColetadoDeTeste.Coletando("VINCULO_PARFOR", FormularioDeTeste.PrimeiraOrdemDeInscricao)
            .ComRotulo("Vínculo com o PARFOR")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .NaSecao(FormularioDeTeste.Secao)
            .ComImpedimento(new Impedimento(PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("VINCULO_PARFOR", Operador.Igual, Json(true)).Value!)]).Value!, mensagem))
            .Construir();

    [Fact(DisplayName = "Cada formulário avaliado diz de cada seção se ela aparece")]
    public async Task Handle_Secoes_DizemSeAparecem()
    {
        PreVisualizacaoDoProcessoDto previa = await PreVisualizarAsync(Perfil(
            nacionalidade: "NATO", corRaca: "INDIGENA", convocacao: "AC", membros: [Membro("CANDIDATO", "URBANO", menorSobGuarda: false)]));

        FormularioSimuladoDto inscricao = previa.Formularios.Single(static f => f.Finalidade.Codigo == "INSCRICAO");
        inscricao.Secoes.Should().NotBeEmpty();
        inscricao.Secoes.Should().OnlyContain(static s => s.Visivel.Codigo == "VERDADEIRO", "nenhuma seção do formulário de teste tem exibição condicional");
    }

    [Fact(DisplayName = "Processo inexistente é recusado como não encontrado")]
    public async Task Handle_ProcessoInexistente_NaoEncontrado()
    {
        Result<PreVisualizacaoDoProcessoDto> resultado = await PreVisualizarProcessoSeletivoQueryHandler.Handle(
            new PreVisualizarProcessoSeletivoQuery(Guid.NewGuid(), new(null, null, null, null)),
            Substitute.For<IProcessoSeletivoRepository>(), Leitor(), CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    private async Task<PreVisualizacaoDoProcessoDto> PreVisualizarAsync(PreVisualizacaoDoProcessoInput perfil)
    {
        Result<PreVisualizacaoDoProcessoDto> resultado = await HandleAsync(perfil);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }

    private Task<Result<PreVisualizacaoDoProcessoDto>> HandleAsync(PreVisualizacaoDoProcessoInput perfil, ProcessoSeletivo? processo = null)
    {
        processo ??= _processo;
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return PreVisualizarProcessoSeletivoQueryHandler.Handle(
            new PreVisualizarProcessoSeletivoQuery(processo.Id, perfil), repository, Leitor(), CancellationToken.None);
    }

    /// <summary>Os documentos exigidos, pelo código, com a identidade da ocorrência quando repetidos por membro.</summary>
    private static IEnumerable<string> Exigidos(PreVisualizacaoDoProcessoDto previa) =>
        previa.Documentos.Where(static d => d.Situacao == "EXIGIDO")
            .Select(static d => d.OcorrenciaId is null ? d.TipoDocumentoCodigo : $"{d.TipoDocumentoCodigo}@{d.OcorrenciaId}");

    /// <summary>
    /// Um candidato com a inscrição e a habilitação concluídas; sem membros, a composição familiar
    /// fica sem resposta.
    /// </summary>
    private static PreVisualizacaoDoProcessoInput Perfil(
        string nacionalidade, string corRaca, string convocacao, IReadOnlyList<OcorrenciaSimuladaInput>? membros) =>
        new(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NACIONALIDADE"] = Json(nacionalidade),
                ["COR_RACA"] = Json(corRaca),
            },
            membros is null
                ? null
                : new Dictionary<string, IReadOnlyList<OcorrenciaSimuladaInput>>(StringComparer.Ordinal) { [Composicao] = membros },
            [
                new EtapaConcluidaInput("INSCRICAO", ConjuntoBasicoDaInscricao.CodigoDaSecao),
                new EtapaConcluidaInput("INSCRICAO", FormularioDeTeste.Secao),
                new EtapaConcluidaInput("HABILITACAO", FormularioDeTeste.Secao),
            ],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["MODALIDADE_CONVOCACAO"] = Json(convocacao) });

    private static OcorrenciaSimuladaInput Membro(string id, string categoriaRenda, bool menorSobGuarda) =>
        new(id, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["CATEGORIA_RENDA"] = Json(categoriaRenda),
            ["MENOR_SOB_GUARDA"] = Json(menorSobGuarda),
        });

    private static JsonElement Json(object valor) => JsonSerializer.SerializeToElement(valor);

    /// <summary>
    /// O processo de Medicina: a inscrição com o conjunto básico; a habilitação com a composição
    /// familiar; e as exigências da habilitação — pertencimento indígena para quem foi convocado em
    /// vaga para pretos, pardos e indígenas e se declarou indígena, declaração de trabalhador rural
    /// quando a família tem renda rural, certidão de guarda por menor sob guarda e o documento de
    /// identidade conforme a nacionalidade. A composição familiar aparece a quem se declarou preto,
    /// pardo ou indígena, e a ampla concorrência, que aqui só vale para quem se declarou indígena, pede
    /// um comprovante dela.
    /// </summary>
    /// <param name="minimoDaComposicao">O mínimo de membros; zero admite declarar que não há membros.</param>
    private static ProcessoSeletivo ProcessoDeMedicina(int minimoDaComposicao = 1)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Medicina 2027", out Guid fase);
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, FormularioDeTeste.Etapas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [], [ComposicaoFamiliar(minimoDaComposicao)]).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(
            [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(
                PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, Json("INDIGENA")).Value!)]).Value!)],
            FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos(DocumentosDaHabilitacaoDeMedicina(fase)).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static GrupoColetado ComposicaoFamiliar(int minimoDaComposicao) => GrupoColetadoDeTeste.Agrupando(Composicao, 0)
        .ComOcorrencias(minimoDaComposicao, null)
        .ExibidoQuando(PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("COR_RACA", Operador.AlgumDe, Json(new[] { "PRETA", "PARDA", "INDIGENA" })).Value!)]).Value!)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComCampos(
            FatoColetadoDeTeste.Coletando("CATEGORIA_RENDA", 0)
                .ComRotulo("Categoria de renda")
                .DoTipo(TipoDeCampo.EscolhaUnica)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
            FatoColetadoDeTeste.Coletando("MENOR_SOB_GUARDA", 1)
                .ComRotulo("Menor sob guarda")
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir())
        .DaFinalidade(FinalidadeFormulario.Habilitacao)
        .Construir();

    private static RegraDeComprovacao[] DocumentosDaHabilitacaoDeMedicina(Guid fase) =>
    [
        RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase, "PERTENCIMENTO_INDIGENA",
            Condicao(0, "MODALIDADE_CONVOCACAO", Operador.AlgumDe, new[] { "LB_PPI", "LI_PPI" }),
            Condicao(0, "COR_RACA", Operador.Igual, "INDIGENA")), 0).Value!,
        RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase, "TRABALHADOR_RURAL", Condicao(0, "CATEGORIAS_RENDA_FAMILIA", Operador.AlgumDe, new[] { "RURAL" })), 1).Value!,
        RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase, "CERTIDAO_GUARDA", Condicao(0, "MENOR_SOB_GUARDA", Operador.Igual, true)), 2, grupoDoFormularioParaRepeticao: Composicao).Value!,
        RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 3, quantidadeMinima: 1, consequencia: null, basesLegais: [],
        [
            RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, quantidadeMinima: null, consequencia: null, basesLegais: [],
                [RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase, "RG", Condicao(0, "NACIONALIDADE", Operador.Diferente, "ESTRANGEIRO")), 0).Value!]).Value!,
            RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase, "PASSAPORTE", Condicao(0, "NACIONALIDADE", Operador.Igual, "ESTRANGEIRO")), 1).Value!,
        ]).Value!,
        RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase, "COMPROVANTE_DA_MODALIDADE", Condicao(0, "MODALIDADE", Operador.AlgumDe, new[] { "AC" })), 5).Value!,
    ];

    private static DocumentoExigido Documento(Guid fase, string codigo, params CondicaoGatilho[] condicoes) => DocumentoExigidoDeTeste.Novo()
        .NaFase(fase)
        .ComTipo(codigo, codigo, "PESSOAL")
        .Condicional(condicoes)
        .ComBasesLegais([DocumentoExigidoBaseLegal.Criar("Edital, Anexo III", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!])
        .NaFinalidade(FinalidadeFormulario.Inscricao)
        .Criar().Value!;

    private static CondicaoGatilho Condicao(int clausula, string fato, Operador operador, object valor) =>
        CondicaoGatilho.Criar(clausula, fato, operador, JsonSerializer.SerializeToElement(valor)).Value!;

    /// <summary>O catálogo com o conjunto básico, a modalidade da convocação, os campos de membro, o agregado da renda e o vínculo com o PARFOR.</summary>
    private static IFatoCandidatoReader Leitor()
    {
        IFatoCandidatoReader leitor = Substitute.For<IFatoCandidatoReader>();
        leitor.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
        [
            .. CadastrosVivos.FatosDeModalidade(),
            Membro("CATEGORIA_RENDA", TipoDoDado.Opcao, ["URBANO", "RURAL"]),
            Membro("MENOR_SOB_GUARDA", TipoDoDado.SimOuNao, null),
            new(Guid.CreateVersion7(), "VINCULO_PARFOR", "Vínculo com o PARFOR", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.SimOuNao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), null, "INSCRICAO",
                "CAMPO_FORMULARIO:VINCULO_PARFOR", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
            new(Guid.CreateVersion7(), "CATEGORIAS_RENDA_FAMILIA", "Categorias de renda da família", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores),
                null, "HABILITACAO", "RESUMO_DE_GRUPO:CATEGORIA_RENDA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        ]));
        return leitor;
    }

    private static FatoCandidatoView Membro(string codigo, TipoDoDado dominio, IReadOnlyList<string>? valores) => new(
        Guid.CreateVersion7(), codigo, codigo, "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(dominio), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), valores, "HABILITACAO", $"CAMPO_FORMULARIO:{codigo}",
        valores?.Select(static (v, ordem) => new FatoValorDominioViewItem(v, v, ordem, true)).ToList(), valores is null ? null : CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo),
        Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: "MEMBRO_GRUPO", ProtecaoAprovada: true);
}
