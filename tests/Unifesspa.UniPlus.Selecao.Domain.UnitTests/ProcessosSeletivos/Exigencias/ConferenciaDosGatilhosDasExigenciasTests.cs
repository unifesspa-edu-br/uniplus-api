namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// Os fatos que o gatilho de uma exigência cita são conhecidos no formulário que a entrega
/// (UNI-REQ-0064, UNI-REQ-0077, UNI-REQ-0144). A exigência entregue por formulário facultativo é aceita, e
/// o fato produzido só em formulário facultativo é citado apenas pela exigência entregue por ele.
/// </summary>
public sealed class ConferenciaDosGatilhosDasExigenciasTests
{
    private readonly FaseCronograma _isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private ConhecimentoDosFatos Conhecimento(CatalogoDosFatosCitados catalogo, params Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta.ColetaDoFormulario[] coletas) =>
        new(coletas, catalogo, [_isencao, _inscricao, _habilitacao]);

    private static CatalogoDosFatosCitados SemCatalogo() => new(new Dictionary<string, string>(), [], new Dictionary<string, IReadOnlyCollection<string>>());

    private static GatilhoDaExigencia Gatilho(FormularioDeclarado formulario, FaseCronograma fase, params string[] fatos) =>
        new(formulario.Codigo, fase.Id, fatos);

    [Fact(DisplayName = "O gatilho que cita fato do formulário obrigatório já alcançado é aceito")]
    public void FatoDeObrigatorioAlcancado_Aceita()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            SemCatalogo(), ColetaDeTeste.Coleta(inscricao, "origem_escolar"), ColetaDeTeste.Coleta(habilitacao, "carteira"));

        ConferenciaDosGatilhosDasExigencias.Conferir(conhecimento, [Gatilho(habilitacao, _habilitacao, "origem_escolar")]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A exigência entregue por formulário facultativo cita o fato do próprio formulário")]
    public void ExigenciaEmFormularioFacultativo_CitaFatoDoProprioFormulario()
    {
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        ConhecimentoDosFatos conhecimento = Conhecimento(SemCatalogo(), ColetaDeTeste.Coleta(pedido, "fundamento_da_isencao"));

        ConferenciaDosGatilhosDasExigencias.Conferir(conhecimento, [Gatilho(pedido, _isencao, "fundamento_da_isencao")]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O fato só de formulário facultativo não é citado por exigência de outro formulário")]
    public void FatoDeFacultativo_NaoECitadoPorOutroFormulario()
    {
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            SemCatalogo(), ColetaDeTeste.Coleta(pedido, "fundamento_da_isencao"), ColetaDeTeste.Coleta(habilitacao, "carteira"));

        IReadOnlyList<FieldError> recusas = ConferenciaDosGatilhosDasExigencias.Conferir(
            conhecimento, [Gatilho(habilitacao, _habilitacao, "fundamento_da_isencao")]);

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigidoErrorCodes.FatoDeFormularioNaoAlcancado);
    }

    [Fact(DisplayName = "O fato cuja fase do catálogo é posterior à exigência é recusado")]
    public void FatoDeFasePosterior_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            new CatalogoDosFatosCitados(new Dictionary<string, string> { ["origem_escolar"] = "HABILITACAO" }, [], new Dictionary<string, IReadOnlyCollection<string>>()),
            ColetaDeTeste.Coleta(inscricao, "origem_escolar"));

        ConferenciaDosGatilhosDasExigencias.Conferir(conhecimento, [Gatilho(inscricao, _inscricao, "origem_escolar")])
            .Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
    }

    [Fact(DisplayName = "A fase do catálogo fora do cronograma é recusada")]
    public void FaseDoCatalogoForaDoCronograma_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            new CatalogoDosFatosCitados(new Dictionary<string, string> { ["origem_escolar"] = "OUTRA" }, [], new Dictionary<string, IReadOnlyCollection<string>>()),
            ColetaDeTeste.Coleta(inscricao, "origem_escolar"));

        ConferenciaDosGatilhosDasExigencias.Conferir(conhecimento, [Gatilho(inscricao, _inscricao, "origem_escolar")])
            .Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma);
    }

    [Fact(DisplayName = "O fato que o processo não coleta nem deriva é recusado")]
    public void FatoForaDoProcesso_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(SemCatalogo(), ColetaDeTeste.Coleta(inscricao, "origem_escolar"));

        ConferenciaDosGatilhosDasExigencias.Conferir(conhecimento, [Gatilho(inscricao, _inscricao, "fato_desconhecido")])
            .Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigidoErrorCodes.FatoNaoColetadoPeloProcesso);
    }

    [Fact(DisplayName = "O derivado com dependência desconhecida é recusado")]
    public void DerivadoComDependenciaDesconhecida_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            new CatalogoDosFatosCitados(new Dictionary<string, string>(), [], new Dictionary<string, IReadOnlyCollection<string>> { ["idade"] = ["fato_desconhecido"] }),
            ColetaDeTeste.Coleta(inscricao, "origem_escolar"));

        ConferenciaDosGatilhosDasExigencias.Conferir(conhecimento, [Gatilho(inscricao, _inscricao, "idade")])
            .Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigidoErrorCodes.FatoDeFormularioNaoAlcancado);
    }

    [Fact(DisplayName = "A recusa aponta a exigência e o fato pelo caminho, sem repetir o fato no texto")]
    public void Recusa_ApontaOCaminhoSemEcoarOFato()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(SemCatalogo(), ColetaDeTeste.Coleta(inscricao, "origem_escolar"));

        FieldError recusa = ConferenciaDosGatilhosDasExigencias
            .Conferir(conhecimento, [Gatilho(inscricao, _inscricao, "origem_escolar"), Gatilho(inscricao, _inscricao, "fato_desconhecido")]).Single();

        recusa.Field.Should().Be("exigencias[1].gatilho.fato_desconhecido");
        recusa.Error.Message.Should().NotContain("fato_desconhecido");
    }

    [Fact(DisplayName = "O campo de grupo repetível é citado só pela exigência que repete por esse grupo")]
    public void CampoDeGrupo_SoNaRepeticaoDoProprioGrupo()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(SemCatalogo(), ColetaDeTeste.ColetaComGrupo(inscricao, "dependentes", "nome_do_dependente"));

        ConferenciaDosGatilhosDasExigencias.Conferir(
            conhecimento, [new GatilhoDaExigencia(inscricao.Codigo, _inscricao.Id, ["nome_do_dependente"], "dependentes")]).Should().BeEmpty();
        ConferenciaDosGatilhosDasExigencias.Conferir(
            conhecimento, [new GatilhoDaExigencia(inscricao.Codigo, _inscricao.Id, ["nome_do_dependente"])])
            .Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigidoErrorCodes.FatoDeMembroForaDaRepeticao);
        ConferenciaDosGatilhosDasExigencias.Conferir(
            conhecimento, [new GatilhoDaExigencia(inscricao.Codigo, _inscricao.Id, ["nome_do_dependente"], "outro_grupo")])
            .Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigidoErrorCodes.FatoDeMembroForaDaRepeticao);
    }

    [Fact(DisplayName = "Exigência de formulário que o processo não tem não é conferida aqui")]
    public void FormularioInexistente_NaoEConferidoAqui()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        FormularioDeclarado inexistente = ColetaDeTeste.Obrigatorio("INEXISTENTE", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(SemCatalogo(), ColetaDeTeste.Coleta(inscricao, "origem_escolar"));

        ConferenciaDosGatilhosDasExigencias.Conferir(conhecimento, [Gatilho(inexistente, _habilitacao, "fato_desconhecido")]).Should().BeEmpty();
    }
}
