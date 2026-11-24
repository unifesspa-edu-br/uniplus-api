namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDoModeloDeFormulario;

/// <summary>
/// Os textos que o banco não grava (caractere nulo, surrogate sem par, código que cresce na normalização) são recusados no cadastro.
/// </summary>
public sealed class ModeloFormularioTextoNaoGravavelTests
{
    private static void DeveRecusarTextoNaoGravavelEm(Result<ModeloFormulario> modelo, string campo)
    {
        FieldError recusa = modelo.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be(campo);
        recusa.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    private static PredicadoDnf QuandoOValorE(Operador operador, JsonElement valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("CONCLUSAO_REGULAR", operador, valor).Value!)]).Value!;

    [Fact(DisplayName = "Texto com caractere nulo na descrição é recusado antes de chegar ao banco, com o código de texto não gravável")]
    public void TextoComCaractereNulo_NaDescricao_Recusa()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação", "texto\0", FinalidadeFormulario.Habilitacao, null, Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);

        DeveRecusarTextoNaoGravavelEm(modelo, "descritivo.descricao");
    }

    [Fact(DisplayName = "Texto com caractere nulo no rótulo do item é recusado no caminho do conteúdo")]
    public void TextoComCaractereNulo_NoRotuloDoItem_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0).ComRotulo("Certificado\0")]));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].apresentacao.rotulo");
    }

    [Fact(DisplayName = "Caractere nulo num valor citado por regra do item é recusado antes de chegar ao banco")]
    public void TextoComCaractereNulo_NoValorDaRegra_Recusa()
    {
        PredicadoDnf valorNulo = QuandoOValorE(Operador.Igual, JsonSerializer.SerializeToElement("A\0"));

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, exibicao: valorNulo)], "CONCLUSAO_REGULAR"));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].regras.exibicao");
    }

    [Fact(DisplayName = "Caractere nulo num valor citado pela obrigatoriedade do item é recusado no predicado de obrigatoriedade")]
    public void TextoComCaractereNulo_NoValorDaObrigatoriedade_Recusa()
    {
        PredicadoDnf valorNulo = QuandoOValorE(Operador.Igual, JsonSerializer.SerializeToElement("A\0"));

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, Obrigatoriedade.Quando(valorNulo))], "CONCLUSAO_REGULAR"));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].regras.obrigatoriedade.predicado");
    }

    [Fact(DisplayName = "Caractere nulo no nome de propriedade de um valor JSON citado por regra é recusado")]
    public void TextoComCaractereNulo_NoNomeDePropriedadeDoValor_Recusa()
    {
        JsonElement objetoNulo = JsonSerializer.SerializeToElement(new[] { new Dictionary<string, string> { ["a\0"] = "b" } });

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0, exibicao: QuandoOValorE(Operador.AlgumDe, objetoNulo))], "CONCLUSAO_REGULAR"));

        DeveRecusarTextoNaoGravavelEm(modelo, "conteudo.itens[0].regras.exibicao");
    }

    private static Result<ModeloFormulario> CriarComDocumento(string tipo, string valor, string observacao)
    {
        PredicadoDnf quando = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("CERTIFICADO", Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
        DocumentoExigidoDoModelo documento = new(
            "INSCRICAO", tipo, quando, new IndeferimentoDoDocumentoDoModelo(false, "REMOVE_VANTAGEM", [new("Edital", "INTERNA_EDITAL", "PENDENTE", observacao)]),
            RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos);

        return Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0)]) with { ConjuntoDeDocumentos = ConteudosDoModeloDeTeste.Conjunto(documento) });
    }

    private static Result<ModeloFormulario> CriarComRequisitos(RequisitosDoDocumentoDoModelo requisitos) =>
        Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("CERTIFICADO", 0)]) with { ConjuntoDeDocumentos = ConteudosDoModeloDeTeste.Conjunto(ConteudosDoModeloDeTeste.Documento("LAUDO", requisitos: requisitos)) });

    [Fact(DisplayName = "Caractere nulo no código da fase da referência do prazo de emissão é recusado no caminho dela")]
    public void DocumentoComCaractereNulo_NaFaseDoPrazoDeEmissao_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(
            CriarComRequisitos(ConteudosDoModeloDeTeste.RequisitosAPartirDasFases("FASE\0", "FASE")),
            "conteudo.conjuntoDeDocumentos.documentosExigidos[0].requisitos.prazoDeEmissao.referencia.faseCodigo");

    [Fact(DisplayName = "Caractere nulo no código da fase da referência da validade é recusado no caminho dela")]
    public void DocumentoComCaractereNulo_NaFaseDaValidade_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(
            CriarComRequisitos(ConteudosDoModeloDeTeste.RequisitosAPartirDasFases("FASE", "FASE\0")),
            "conteudo.conjuntoDeDocumentos.documentosExigidos[0].requisitos.validade.referencia.faseCodigo");

    [Fact(DisplayName = "Caractere nulo no código do tipo do documento exigido do conjunto padrão é recusado")]
    public void DocumentoComCaractereNulo_NoCodigoDoTipo_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(CriarComDocumento("LAUDO\0", "A", "x"), "conteudo.conjuntoDeDocumentos.documentosExigidos[0].tipoDocumentoCodigo");

    [Fact(DisplayName = "Caractere nulo no valor da condição do documento exigido é recusado")]
    public void DocumentoComCaractereNulo_NaCondicao_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(CriarComDocumento("LAUDO", "A\0", "x"), "conteudo.conjuntoDeDocumentos.documentosExigidos[0].quando");

    [Fact(DisplayName = "Caractere nulo na base legal do documento exigido é recusado")]
    public void DocumentoComCaractereNulo_NaBaseLegal_Recusa() =>
        DeveRecusarTextoNaoGravavelEm(CriarComDocumento("LAUDO", "A", "x\0"), "conteudo.conjuntoDeDocumentos.documentosExigidos[0].indeferimento.basesLegais[0].observacao");

    [Fact(DisplayName = "Código com surrogate sem par é recusado, sem exceção na normalização")]
    public void CodigoComSurrogateSemPar_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO\uD800", 0).ComRotulo("Certificado")]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Fact(DisplayName = "Código de termo com surrogate sem par é recusado, sem exceção na conferência de unicidade")]
    public void CodigoDeTermoComSurrogateSemPar_Recusa()
    {
        TermoDoModelo termo = new("LGPD\uD800", 0, Guid.NewGuid(), Guid.NewGuid(), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre));

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, new ConteudoDoModelo("Habilitação", [Dados, Revisao], [Item("CERTIFICADO", 0)], [termo], [], []));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Fact(DisplayName = "O tamanho do código do modelo é o do código normalizado, que é o gravado")]
    public void CodigoQueCresceNaNormalizacao_Recusa()
    {
        string codigo = string.Concat(Enumerable.Repeat("\u0958", ModeloFormulario.CodigoMaxLength));

        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            codigo, "Habilitação", null, FinalidadeFormulario.Habilitacao, null, Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.CodigoTamanho);
    }
}
