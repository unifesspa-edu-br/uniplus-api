namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Fatos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// O fato é conhecido no ponto quando o produtor é o formulário do ponto ou um que ele alcança, a fase do
/// catálogo não é posterior ao momento do ponto e o derivado tem todas as dependências conhecidas
/// (UNI-REQ-0144, UNI-REQ-0077). O ciclo de dependência não é conhecido.
/// </summary>
public sealed class ConhecimentoDosFatosTests
{
    private const string OrigemEscolar = "origem_escolar";

    private readonly FaseCronograma _isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private FaseCronograma[] Cronograma => [_isencao, _inscricao, _habilitacao];

    private static CatalogoDosFatosCitados Catalogo(
        Dictionary<string, string>? fases = null, Dictionary<string, IReadOnlyCollection<string>>? derivados = null, string[]? declarados = null) =>
        new(fases ?? [], declarados ?? [], derivados ?? []);

    private ConhecimentoDosFatos Conhecimento(CatalogoDosFatosCitados catalogo, params ColetaDoFormulario[] coletas) =>
        new(coletas, catalogo, Cronograma);

    [Fact(DisplayName = "O fato do próprio formulário é conhecido no ponto, mesmo facultativo")]
    public void ProdutorEOProprioFormulario_EConhecido()
    {
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        ConhecimentoDosFatos conhecimento = Conhecimento(Catalogo(), ColetaDeTeste.Coleta(pedido, "renda"));

        conhecimento.NoPonto("renda", new PontoDeUso(_isencao, pedido)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "O fato de formulário obrigatório já alcançado é conhecido")]
    public void ProdutorObrigatorioAlcancado_EConhecido()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(new() { [OrigemEscolar] = "INSCRICAO" }),
            ColetaDeTeste.Coleta(inscricao, OrigemEscolar),
            ColetaDeTeste.Coleta(habilitacao, "outro"));

        conhecimento.NoPonto(OrigemEscolar, new PontoDeUso(_habilitacao, habilitacao)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "O pedido de isenção em fase própria antes da inscrição conhece a origem escolar da inscrição")]
    public void IsencaoOrdenadaAntesDaInscricao_ConheceAOrigemEscolar()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(new() { [OrigemEscolar] = "INSCRICAO" }),
            ColetaDeTeste.Coleta(inscricao, OrigemEscolar),
            ColetaDeTeste.Coleta(pedido, "renda"));

        conhecimento.NoPonto(OrigemEscolar, new PontoDeUso(_isencao, pedido)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "O fato cuja fase do catálogo é posterior ao ponto não é conhecido")]
    public void FaseDoCatalogoPosterior_NaoEConhecido()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(new() { [OrigemEscolar] = "HABILITACAO" }), ColetaDeTeste.Coleta(inscricao, OrigemEscolar));

        conhecimento.NoPonto(OrigemEscolar, new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.ConhecidoEmFasePosterior);
    }

    [Fact(DisplayName = "A fase do catálogo fora do cronograma recusa o fato")]
    public void FaseDoCatalogoForaDoCronograma_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(new() { [OrigemEscolar] = "FASE_QUE_NAO_EXISTE" }), ColetaDeTeste.Coleta(inscricao, OrigemEscolar));

        conhecimento.NoPonto(OrigemEscolar, new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.FaseDoCatalogoForaDoCronograma);
    }

    [Fact(DisplayName = "O fato só de formulário facultativo de outro ponto não é conhecido")]
    public void ProdutorFacultativoDeOutroPonto_NaoEAlcancado()
    {
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(Catalogo(), ColetaDeTeste.Coleta(pedido, "renda"), ColetaDeTeste.Coleta(habilitacao, "outro"));

        conhecimento.NoPonto("renda", new PontoDeUso(_habilitacao, habilitacao)).Should().Be(ConhecimentoDoFatoNoPonto.RespostaNaoAlcancada);
    }

    [Fact(DisplayName = "O fato que ninguém produz, deriva nem situa no catálogo está fora do processo")]
    public void FatoSemOrigem_ForaDoProcesso()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(Catalogo(), ColetaDeTeste.Coleta(inscricao, OrigemEscolar));

        conhecimento.NoPonto("fato_desconhecido", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.ForaDoProcesso);
    }

    [Theory(DisplayName = "O fato do catálogo sem formulário produtor vale pela fase do catálogo")]
    [InlineData("INSCRICAO", ConhecimentoDoFatoNoPonto.Conhecido)]
    [InlineData("HABILITACAO", ConhecimentoDoFatoNoPonto.ConhecidoEmFasePosterior)]
    public void FatoDoCatalogoSemProdutor_ValePelaFase(string faseDoCatalogo, ConhecimentoDoFatoNoPonto esperado)
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(Catalogo(new() { ["grupo_da_convocacao"] = faseDoCatalogo }), ColetaDeTeste.Coleta(inscricao, OrigemEscolar));

        conhecimento.NoPonto("grupo_da_convocacao", new PontoDeUso(_inscricao, inscricao)).Should().Be(esperado);
    }

    [Fact(DisplayName = "O derivado com todas as dependências conhecidas é conhecido")]
    public void DerivadoComDependenciasConhecidas_EConhecido()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(derivados: new() { ["idade"] = ["data_de_nascimento"] }), ColetaDeTeste.Coleta(inscricao, "data_de_nascimento"));

        conhecimento.NoPonto("idade", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "O derivado com dependência desconhecida não é conhecido")]
    public void DerivadoComDependenciaDesconhecida_NaoEConhecido()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(derivados: new() { ["idade"] = ["data_de_nascimento", "fato_desconhecido"] }), ColetaDeTeste.Coleta(inscricao, "data_de_nascimento"));

        conhecimento.NoPonto("idade", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.DependenciaDesconhecida);
    }

    [Fact(DisplayName = "O ciclo de dependência entre derivados não é conhecido")]
    public void CicloDeDependencia_NaoEConhecido()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(derivados: new() { ["a"] = ["b"], ["b"] = ["a"] }), ColetaDeTeste.Coleta(inscricao, OrigemEscolar));

        conhecimento.NoPonto("a", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.DependenciaDesconhecida);
    }

    [Fact(DisplayName = "O fato declarado que nenhum formulário coleta está fora do processo, mesmo situado no catálogo")]
    public void FatoDeclaradoSemFormulario_ForaDoProcesso()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(new() { ["renda"] = "INSCRICAO" }, declarados: ["renda"]), ColetaDeTeste.Coleta(inscricao, OrigemEscolar));

        conhecimento.NoPonto("renda", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.ForaDoProcesso);
    }

    [Fact(DisplayName = "O fato declarado e coletado pelo formulário alcançado é conhecido")]
    public void FatoDeclaradoComFormulario_EConhecido()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(new() { [OrigemEscolar] = "INSCRICAO" }, declarados: [OrigemEscolar]), ColetaDeTeste.Coleta(inscricao, OrigemEscolar));

        conhecimento.NoPonto(OrigemEscolar, new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "O fato com formulário produtor e derivado confere as dependências")]
    public void ProdutorEDerivado_ConfereAsDependencias()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(derivados: new() { ["idade"] = ["fato_desconhecido"] }), ColetaDeTeste.Coleta(inscricao, "idade"));

        conhecimento.NoPonto("idade", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.DependenciaDesconhecida);
    }

    [Fact(DisplayName = "O ciclo de dependência não é encoberto pelo formulário produtor")]
    public void ProdutorEmCiclo_NaoEConhecido()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(derivados: new() { ["a"] = ["b"], ["b"] = ["a"] }), ColetaDeTeste.Coleta(inscricao, "a", "b"));

        conhecimento.NoPonto("a", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.DependenciaDesconhecida);
    }

    [Fact(DisplayName = "O fato da escolha de fundamentos é conhecido no próprio pedido de isenção")]
    public void EscolhaDeFundamentos_EConhecidaNoProprioPedido()
    {
        FormularioDeclarado pedido = ColetaDeTeste.Facultativo("PEDIDO", _isencao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(), ColetaDeTeste.Coleta(pedido, "renda") with { FatoDaEscolhaDeFundamentos = "fundamento_da_isencao" });

        conhecimento.NoPonto("fundamento_da_isencao", new PontoDeUso(_isencao, pedido)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "O campo de grupo repetível é fato do formulário que o coleta")]
    public void CampoDeGrupo_EFatoDoFormularioQueOColeta()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = Conhecimento(Catalogo(), ColetaDeTeste.ColetaComGrupo(inscricao, "dependentes", "nome_do_dependente"));

        conhecimento.NoPonto("nome_do_dependente", new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "A fase do catálogo vem da cópia entregue, não do cadastro")]
    public void CatalogoCopiado_NaoMudaComOCadastro()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(inscricao, OrigemEscolar);
        PontoDeUso ponto = new(_inscricao, inscricao);

        Conhecimento(Catalogo(new() { [OrigemEscolar] = "INSCRICAO" }), coleta).NoPonto(OrigemEscolar, ponto).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
        Conhecimento(Catalogo(new() { [OrigemEscolar] = "HABILITACAO" }), coleta).NoPonto(OrigemEscolar, ponto).Should().Be(ConhecimentoDoFatoNoPonto.ConhecidoEmFasePosterior);
    }

    [Fact(DisplayName = "Os fatos conhecidos antes do formulário são os dos formulários que ele alcança, sem os dele")]
    public void ConhecidosAntesDoFormulario_SaoOsDosFormulariosAlcancados()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Catalogo(), ColetaDeTeste.Coleta(inscricao, OrigemEscolar, "sexo"), ColetaDeTeste.Coleta(habilitacao, "carteira"));

        conhecimento.ConhecidosAntesDoFormulario(new PontoDeUso(_habilitacao, habilitacao)).Should().BeEquivalentTo(OrigemEscolar, "sexo");
    }
}
