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

    [Fact(DisplayName = "O código da etapa e o valor da exibição dela são recusados, cada um no seu campo")]
    public void Etapa_CodigoEValorDaExibicao_Recusa()
    {
        EtapaDoModelo etapa = Etapa(ComNulo, 0, QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)));

        CamposRecusados(Vazio with { Etapas = [etapa] }).Should().Equal("etapas[0].codigo", "etapas[0].exibicao");
    }

    [Fact(DisplayName = "O rótulo do campo de item é recusado no caminho do item")]
    public void Item_Rotulo_Recusa() =>
        CamposRecusados(Vazio with { Itens = [Item("RENDA"), Item("COR_RACA", 1).ComRotulo(ComNulo)] }).Should().Equal("itens[1].rotulo");

    [Fact(DisplayName = "A mensagem do impedimento e a condição dele são recusadas")]
    public void Item_Impedimento_Recusa()
    {
        ItemDoModelo campo = Item("CERTIFICADO").ComImpedimento(new Impedimento(QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)), ComNulo));

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal("itens[0].impedimento.quando", "itens[0].impedimento.mensagem");
    }

    [Fact(DisplayName = "No mesmo item, as restrições são recusadas antes da condição e da mensagem do impedimento")]
    public void Item_RestricaoEImpedimento_RecusaARestricaoAntesDoImpedimento()
    {
        RestricaoValor opcoes = new OpcoesPermitidas([new OpcoesCondicionadas(QuandoOValorE(ValorComNulo), [ComNulo])]);
        ItemDoModelo campo = Item("CERTIFICADO")
            .ComRestricoes([opcoes, new MunicipiosDaUf(ComNulo)])
            .ComImpedimento(new Impedimento(QuandoOValorE(ValorComNulo), ComNulo));

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal(
            "itens[0].restricoes[0].entradas[0].valores",
            "itens[0].restricoes[0].entradas[0].quando",
            "itens[0].restricoes[1].fatos",
            "itens[0].impedimento.quando",
            "itens[0].impedimento.mensagem");
    }

    [Fact(DisplayName = "O valor das opções permitidas, e a condição delas, são recusados")]
    public void Item_OpcoesPermitidas_Recusa()
    {
        RestricaoValor opcoes = new OpcoesPermitidas([new OpcoesCondicionadas(QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)), [ComNulo])]);

        CamposRecusados(Vazio with { Itens = [Item("COTA").ComRestricoes([opcoes])] })
            .Should().Equal("itens[0].restricoes[0].entradas[0].valores", "itens[0].restricoes[0].entradas[0].quando");
    }

    [Fact(DisplayName = "O fato citado pelas opções das respostas e o fato da UF dos municípios são recusados")]
    public void Item_RestricoesQueCitamFatos_Recusa()
    {
        ItemDoModelo campo = Item("MUNICIPIO").ComRestricoes([new OpcoesDasRespostas([ComNulo]), new MunicipiosDaUf(ComNulo)]);

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal("itens[0].restricoes[0].fatos", "itens[0].restricoes[1].fatos");
    }

    [Fact(DisplayName = "O rótulo do grupo e o código do campo de grupo são recusados no caminho do grupo")]
    public void Grupo_RotuloECampo_Recusa()
    {
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, null, Item("MAIOR_IDADE"), Item(ComNulo, 1).ComRotulo("Maior idade")) with { Rotulo = ComNulo };

        CamposRecusados(Vazio with { Grupos = [grupo] }).Should().Equal("grupos[0].rotulo", "grupos[0].subitens[1].fatoCodigo");
    }

    [Fact(DisplayName = "O código do termo, a exibição e a obrigatoriedade dele são recusados, cada um no seu campo")]
    public void Termo_CodigoExibicaoEObrigatoriedade_Recusa()
    {
        TermoDoModelo termo = Termo(ComNulo, 0, QuandoOValorE(ValorComNulo)) with { Obrigatoriedade = Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)) };

        CamposRecusados(Vazio with { Termos = [termo] }).Should().Equal("termos[0].codigo", "termos[0].exibicao", "termos[0].predicadoObrigatoriedade");
    }

    [Fact(DisplayName = "A exibição do item é recusada no campo da precondição")]
    public void Item_Exibicao_RecusaNaPrecondicao() =>
        CamposRecusados(Vazio with { Itens = [Item("RENDA", exibicao: QuandoOValorE(ValorComNulo))] }).Should().Equal("itens[0].precondicao");

    [Fact(DisplayName = "A obrigatoriedade do item é recusada no campo do predicado de obrigatoriedade")]
    public void Item_Obrigatoriedade_RecusaNoPredicadoDeObrigatoriedade()
    {
        ItemDoModelo campo = Item("RENDA").ComObrigatoriedade(Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)));

        CamposRecusados(Vazio with { Itens = [campo] }).Should().Equal("itens[0].predicadoObrigatoriedade");
    }

    [Fact(DisplayName = "A exibição e a obrigatoriedade do campo de grupo são recusadas na precondição e no predicado de obrigatoriedade")]
    public void Subitem_ExibicaoEObrigatoriedade_RecusaNosCamposDoContrato()
    {
        ItemDoModelo subitem = Item("SEM_RENDA", 1, QuandoOValorE(ValorComNulo)).ComObrigatoriedade(Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)));
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, null, Item("MAIOR_IDADE"), subitem);

        CamposRecusados(Vazio with { Grupos = [grupo] }).Should().Equal("grupos[0].subitens[1].precondicao", "grupos[0].subitens[1].predicadoObrigatoriedade");
    }

    [Fact(DisplayName = "A exibição do grupo é recusada na exibição e a obrigatoriedade no predicado de obrigatoriedade")]
    public void Grupo_ExibicaoEObrigatoriedade_RecusaNosCamposDoContrato()
    {
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, QuandoOValorE(ValorComNulo), Item("MAIOR_IDADE")) with
        {
            Obrigatoriedade = Obrigatoriedade.Quando(QuandoOValorE(ValorComNulo)),
        };

        CamposRecusados(Vazio with { Grupos = [grupo] }).Should().Equal("grupos[0].exibicao", "grupos[0].predicadoObrigatoriedade");
    }

    [Fact(DisplayName = "O documento exigido é recusado no código, na condição, na exigência por opção e na base legal")]
    public void Documento_CadaTexto_Recusa()
    {
        DocumentoExigidoDoModelo documento = Documento(ComNulo, QuandoOValorE(JsonSerializer.SerializeToElement(ComNulo)), new(ComNulo, [ComNulo])) with
        {
            BasesLegais = [new("Edital", "INTERNA_EDITAL", "PENDENTE", ComNulo)],
        };

        CamposRecusados(Vazio with { DocumentosExigidos = [documento] }).Should().Equal(
            "documentosExigidos[0].tipoDocumentoCodigo",
            "documentosExigidos[0].quando",
            "documentosExigidos[0].porOpcao.fatoCodigo",
            "documentosExigidos[0].porOpcao.exceto[0]",
            "documentosExigidos[0].basesLegais[0].observacao");
    }

    private static ConteudoDoModelo ConteudoComRegraNaoGravavelEmCadaParte()
    {
        PredicadoDnf ruim = QuandoOValorE(ValorComNulo);
        ItemDoModelo campo = Item("CERTIFICADO", exibicao: ruim)
            .ComObrigatoriedade(Obrigatoriedade.Quando(ruim))
            .ComImpedimento(new Impedimento(ruim, "Já possui o certificado."));
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, ruim, Item("MAIOR_IDADE")) with { Obrigatoriedade = Obrigatoriedade.Quando(ruim) };
        TermoDoModelo termo = Termo("LGPD", exibicao: ruim) with { Obrigatoriedade = Obrigatoriedade.Quando(ruim) };

        return Vazio with { Etapas = [Etapa(exibicao: ruim)], Itens = [campo], Grupos = [grupo], Termos = [termo], DocumentosExigidos = [Documento("LAUDO", ruim)] };
    }

    [Fact(DisplayName = "O texto ruim de toda regra de cada parte é recusado no caminho dela, e as partes seguem a ordem do conteúdo")]
    public void RegrasDeTodasAsPartes_RecusaNoCaminhoDeCadaUma_NaOrdemDasPartes() =>
        CamposRecusados(ConteudoComRegraNaoGravavelEmCadaParte()).Should().Equal(
            "etapas[0].exibicao",
            "itens[0].precondicao",
            "itens[0].predicadoObrigatoriedade",
            "itens[0].impedimento.quando",
            "grupos[0].exibicao",
            "grupos[0].predicadoObrigatoriedade",
            "termos[0].exibicao",
            "termos[0].predicadoObrigatoriedade",
            "documentosExigidos[0].quando");

    [Fact(DisplayName = "O pressuposto com caractere nulo é recusado na posição dele")]
    public void Pressuposto_Recusa() =>
        CamposRecusados(Vazio with { Pressupostos = ["CONCLUSAO_REGULAR", ComNulo] }).Should().Equal("pressupostos[1]");

    [Fact(DisplayName = "O valor da condição, dentro de lista ou de objeto, e o nome da propriedade do objeto são recusados")]
    public void Condicao_ValorEmListaOuObjeto_Recusa()
    {
        JsonElement lista = JsonSerializer.SerializeToElement(new[] { "A", ComNulo });
        JsonElement objeto = JsonSerializer.SerializeToElement(new[] { new Dictionary<string, string> { [ComNulo] = "b" } });

        CamposRecusados(Vazio with { Itens = [Item("A", exibicao: Quando("FATO", Operador.Em, lista)), Item("B", 1, Quando("FATO", Operador.Em, objeto))] })
            .Should().Equal("itens[0].precondicao", "itens[1].precondicao");
    }

    [Fact(DisplayName = "O código com surrogate sem par não é Unicode válido e é recusado, sem exceção")]
    public void CodigoComSurrogateSemPar_Recusa() =>
        CamposRecusados(Vazio with { Termos = [Termo("LGPD\uD800")] }).Should().Equal("termos[0].codigo");

    [Fact(DisplayName = "Mais de um texto ruim no mesmo campo é uma recusa só")]
    public void MaisDeUmTextoRuimNoMesmoCampo_UmaRecusa()
    {
        PredicadoDnf duasCondicoes = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (0, CondicaoDnf.Criar("A", Operador.Igual, JsonSerializer.SerializeToElement(ComNulo)).Value!),
            (0, CondicaoDnf.Criar("B", Operador.Igual, JsonSerializer.SerializeToElement(ComNulo)).Value!),
        ]).Value!;

        IEnumerable<FieldError> recusas = (Vazio with { Itens = [Item("RENDA", exibicao: duasCondicoes)] }).TextosNaoGravaveis();

        recusas.Should().ContainSingle().Which.Field.Should().Be("itens[0].precondicao");
    }
}
