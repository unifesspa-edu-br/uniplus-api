namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>
/// Os textos do conteúdo que o banco não grava — com caractere nulo ou Unicode inválido —, em cada
/// parte do conteúdo, inclusive nos valores e nos fatos citados pelas regras: a recusa aponta o campo.
/// </summary>
public sealed class ConteudoDoModeloTextosNaoGravaveisTests
{
    private const string ComNulo = "x\0";

    private static IEnumerable<string?> CamposRecusados(ConteudoDoModelo conteudo) => conteudo.TextosNaoGravaveis().Select(static e => e.Field);

    private static JsonElement ValorComNulo => JsonSerializer.SerializeToElement(ComNulo);

    private static PredicadoDnf QuandoOValorE(JsonElement valor) => Quando("FATO", Operador.Igual, valor);

    [Fact(DisplayName = "O conteúdo gravável não tem recusa, e o texto ausente não é recusado")]
    public void ConteudoGravavel_SemRecusa()
    {
        ConteudoDoModelo conteudo = Vazio with { Titulo = null, Itens = [Item("RENDA", exibicao: Quando("COR_RACA"))] };

        conteudo.TextosNaoGravaveis().Should().BeEmpty();
    }

    [Fact(DisplayName = "A recusa leva o código do texto não gravável")]
    public void Recusa_LevaOCodigoDoErro() =>
        (Vazio with { Titulo = ComNulo }).TextosNaoGravaveis().Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);

    [Fact(DisplayName = "O título com caractere nulo é recusado")]
    public void Titulo_ComNulo_Recusa() => CamposRecusados(Vazio with { Titulo = ComNulo }).Should().Equal("titulo");

    [Fact(DisplayName = "O valor da exibição é recusado; o código é conferido pela forma")]
    public void Pagina_ValorDaExibicao_Recusa()
    {
        PaginaDoModelo pagina = Pagina(ComNulo, 0, QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)));

        CamposRecusados(Vazio with { Paginas = [pagina] }).Should().Equal("paginas[0].exibicao");
    }

    [Fact(DisplayName = "O rótulo do campo de item é recusado no caminho do item")]
    public void Item_Rotulo_Recusa() =>
        CamposRecusados(Vazio with { Itens = [Item("RENDA"), Item("COR_RACA", 1).ComRotulo(ComNulo)] }).Should().Equal("itens[1].apresentacao.rotulo");

    [Fact(DisplayName = "A mensagem do impedimento e a condição dele são recusadas")]
    public void Item_Impedimento_Recusa()
    {
        ItemDoModelo campo = Item("CERTIFICADO").ComImpedimento(new Impedimento(QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)), ComNulo));

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal("itens[0].regras.impedimento.quando", "itens[0].regras.impedimento.mensagem");
    }

    [Fact(DisplayName = "No mesmo item, as restrições são recusadas antes da condição e da mensagem do impedimento")]
    public void Item_RestricaoEImpedimento_RecusaARestricaoAntesDoImpedimento()
    {
        RestricaoValor opcoes = new OpcoesPermitidas([new OpcoesCondicionadas(QuandoOValorE(ValorComNulo), [ComNulo])]);
        ItemDoModelo campo = Item("CERTIFICADO")
            .ComRestricoes([opcoes, new MunicipiosDaUf(ComNulo)])
            .ComImpedimento(new Impedimento(QuandoOValorE(ValorComNulo), ComNulo));

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal(
            "itens[0].regras.restricoes[0].entradas[0].valores",
            "itens[0].regras.restricoes[0].entradas[0].quando",
            "itens[0].regras.restricoes[1].fatos",
            "itens[0].regras.impedimento.quando",
            "itens[0].regras.impedimento.mensagem");
    }

    [Fact(DisplayName = "O valor das opções permitidas, e a condição delas, são recusados")]
    public void Item_OpcoesPermitidas_Recusa()
    {
        RestricaoValor opcoes = new OpcoesPermitidas([new OpcoesCondicionadas(QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)), [ComNulo])]);

        CamposRecusados(Vazio with { Itens = [Item("COTA").ComRestricoes([opcoes])] })
            .Should().Equal("itens[0].regras.restricoes[0].entradas[0].valores", "itens[0].regras.restricoes[0].entradas[0].quando");
    }

    [Fact(DisplayName = "O fato citado pelas opções das respostas e o fato da UF dos municípios são recusados")]
    public void Item_RestricoesQueCitamFatos_Recusa()
    {
        ItemDoModelo campo = Item("MUNICIPIO").ComRestricoes([new OpcoesDasRespostas([ComNulo]), new MunicipiosDaUf(ComNulo)]);

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal("itens[0].regras.restricoes[0].fatos", "itens[0].regras.restricoes[1].fatos");
    }

    [Fact(DisplayName = "O rótulo do grupo e o código do campo de grupo são recusados no caminho do grupo")]
    public void Grupo_RotuloECampo_Recusa()
    {
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, null, Item("MAIOR_IDADE"), Item(ComNulo, 1).ComRotulo("Maior idade")) with { Rotulo = ComNulo };

        CamposRecusados(Vazio with { Grupos = [grupo] }).Should().Equal("grupos[0].rotulo", "grupos[0].subitens[1].fatoCodigo");
    }

    [Fact(DisplayName = "A exibição e a obrigatoriedade são recusadas; o código é conferido pela forma")]
    public void Termo_ExibicaoEObrigatoriedade_Recusa()
    {
        TermoDoModelo termo = Termo(ComNulo, 0, QuandoOValorE(ValorComNulo)).ComObrigatoriedade(Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)));

        CamposRecusados(Vazio with { Termos = [termo] }).Should().Equal("termos[0].regras.exibicao", "termos[0].regras.obrigatoriedade.predicado");
    }

    [Fact(DisplayName = "A exibição do item é recusada no caminho das regras")]
    public void Item_Exibicao_RecusaNoCaminhoDasRegras() =>
        CamposRecusados(Vazio with { Itens = [Item("RENDA", exibicao: QuandoOValorE(ValorComNulo))] }).Should().Equal("itens[0].regras.exibicao");

    [Fact(DisplayName = "A obrigatoriedade do item é recusada no campo do predicado de obrigatoriedade")]
    public void Item_Obrigatoriedade_RecusaNoPredicadoDeObrigatoriedade()
    {
        ItemDoModelo campo = Item("RENDA").ComObrigatoriedade(Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)));

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal("itens[0].regras.obrigatoriedade.predicado");
    }

    [Fact(DisplayName = "A exibição e a obrigatoriedade do campo de grupo são recusadas na precondição e no predicado de obrigatoriedade")]
    public void Subitem_ExibicaoEObrigatoriedade_RecusaNosCamposDoContrato()
    {
        ItemDoModelo subitem = Item("SEM_RENDA", 1, QuandoOValorE(ValorComNulo)).ComObrigatoriedade(Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)));
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, null, Item("MAIOR_IDADE"), subitem);

        CamposRecusados(Vazio with { Grupos = [grupo] }).Should().Equal("grupos[0].subitens[1].regras.exibicao", "grupos[0].subitens[1].regras.obrigatoriedade.predicado");
    }

    [Fact(DisplayName = "A exibição do grupo é recusada na exibição e a obrigatoriedade no predicado de obrigatoriedade")]
    public void Grupo_ExibicaoEObrigatoriedade_RecusaNosCamposDoContrato()
    {
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, QuandoOValorE(ValorComNulo), Item("MAIOR_IDADE"))
            .ComObrigatoriedade(Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)));

        CamposRecusados(Vazio with { Grupos = [grupo] }).Should().Equal("grupos[0].regras.exibicao", "grupos[0].regras.obrigatoriedade.predicado");
    }

    [Fact(DisplayName = "O documento exigido é recusado no código, na condição, na exigência por opção e na base legal")]
    public void Documento_CadaTexto_Recusa()
    {
        DocumentoExigidoDoModelo documento = Documento(ComNulo, QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)), new(ComNulo, [ComNulo])) with
        {
            Indeferimento = new(false, "RETIRA_O_ACRESCIMO", [new("Edital", "INTERNA_EDITAL", "PENDENTE", ComNulo)]),
        };

        CamposRecusados(Vazio with { ConjuntoDeDocumentos = Conjunto(documento) }).Should().Equal(
            "conjuntoDeDocumentos.documentosExigidos[0].tipoDocumentoCodigo",
            "conjuntoDeDocumentos.documentosExigidos[0].quando",
            "conjuntoDeDocumentos.documentosExigidos[0].porOpcao.fatoCodigo",
            "conjuntoDeDocumentos.documentosExigidos[0].porOpcao.exceto[0]",
            "conjuntoDeDocumentos.documentosExigidos[0].indeferimento.basesLegais[0].observacao");
    }

    [Fact(DisplayName = "O documento de um grupo é recusado no caminho do grupo, e o indeferimento do grupo na consequência e na base legal")]
    public void GrupoDeDocumentos_CadaTexto_RecusaNoCaminhoDoGrupo()
    {
        GrupoDeDocumentosDoModelo grupo = GrupoDeDocumentos(1, Documento("RG"), Documento(ComNulo, QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)))) with
        {
            Indeferimento = new(ComNulo, [new("Edital", "INTERNA_EDITAL", "PENDENTE", ComNulo)]),
        };

        CamposRecusados(Vazio with { ConjuntoDeDocumentos = ConjuntoComGrupos(grupo) }).Should().Equal(
            "conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[1].tipoDocumentoCodigo",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[1].quando",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento.consequenciaDoDescumprimento",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento.basesLegais[0].observacao");
    }

    [Fact(DisplayName = "O grupo sem indeferimento não tem texto de indeferimento a recusar")]
    public void GrupoDeDocumentos_SemIndeferimento_SemRecusa() =>
        CamposRecusados(Vazio with { ConjuntoDeDocumentos = ConjuntoComGrupos(GrupoDeDocumentos(null, Documento("RG"))) }).Should().BeEmpty();

    private static ConteudoDoModelo ConteudoComRegraNaoGravavelEmCadaParte()
    {
        PredicadoDnf ruim = QuandoOValorE(ValorComNulo);
        ItemDoModelo campo = Item("CERTIFICADO", exibicao: ruim)
            .ComObrigatoriedade(Obrigatoriedade.Quando(ruim))
            .ComImpedimento(new Impedimento(ruim, "Já possui o certificado."));
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, ruim, Item("MAIOR_IDADE")).ComObrigatoriedade(Obrigatoriedade.Quando(ruim));
        TermoDoModelo termo = Termo("LGPD", exibicao: ruim).ComObrigatoriedade(Obrigatoriedade.Quando(ruim));

        return Vazio with { Paginas = [Pagina(exibicao: ruim)], Itens = [campo], Grupos = [grupo], Termos = [termo], ConjuntoDeDocumentos = Conjunto(Documento("LAUDO", ruim)) };
    }

    [Fact(DisplayName = "O texto ruim de toda regra de cada parte é recusado no caminho dela, e as partes seguem a ordem do conteúdo")]
    public void RegrasDeTodasAsPartes_RecusaNoCaminhoDeCadaUma_NaOrdemDasPartes() =>
        CamposRecusados(ConteudoComRegraNaoGravavelEmCadaParte()).Should().Equal(
            "paginas[0].exibicao",
            "itens[0].regras.exibicao",
            "itens[0].regras.obrigatoriedade.predicado",
            "itens[0].regras.impedimento.quando",
            "grupos[0].regras.exibicao",
            "grupos[0].regras.obrigatoriedade.predicado",
            "termos[0].regras.exibicao",
            "termos[0].regras.obrigatoriedade.predicado",
            "conjuntoDeDocumentos.documentosExigidos[0].quando");

    [Fact(DisplayName = "O pressuposto com caractere nulo é recusado na posição dele")]
    public void Pressuposto_Recusa() =>
        CamposRecusados(Vazio with { Pressupostos = ["CONCLUSAO_REGULAR", ComNulo] }).Should().Equal("pressupostos[1]");

    [Fact(DisplayName = "O valor da condição, dentro de lista ou de objeto, e o nome da propriedade do objeto são recusados")]
    public void Condicao_ValorEmListaOuObjeto_Recusa()
    {
        JsonElement lista = JsonSerializer.SerializeToElement(new[] { "A", ComNulo });
        JsonElement objeto = JsonSerializer.SerializeToElement(new[] { new Dictionary<string, string> { [ComNulo] = "b" } });

        CamposRecusados(Vazio with { Itens = [Item("A", exibicao: Quando("FATO", Operador.AlgumDe, lista)), Item("B", 1, Quando("FATO", Operador.AlgumDe, objeto))] })
            .Should().Equal("itens[0].regras.exibicao", "itens[1].regras.exibicao");
    }

    [Fact(DisplayName = "O código do termo é conferido pela forma, sem exceção")]
    public void CodigoDoTermoComSurrogateSemPar_ConferidoPelaForma() =>
        CamposRecusados(Vazio with { Termos = [Termo("LGPD\uD800")] }).Should().BeEmpty();

    [Fact(DisplayName = "Mais de um texto ruim no mesmo campo é uma recusa só")]
    public void MaisDeUmTextoRuimNoMesmoCampo_UmaRecusa()
    {
        PredicadoDnf duasCondicoes = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (0, CondicaoDnf.Criar("A", Operador.Igual, JsonSerializer.SerializeToElement(ComNulo)).Value!),
            (0, CondicaoDnf.Criar("B", Operador.Igual, JsonSerializer.SerializeToElement(ComNulo)).Value!),
        ]).Value!;

        IEnumerable<FieldError> recusas = (Vazio with { Itens = [Item("RENDA", exibicao: duasCondicoes)] }).TextosNaoGravaveis();

        recusas.Should().ContainSingle().Which.Field.Should().Be("itens[0].regras.exibicao");
    }
}
