namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// O texto que o banco não grava, com o caractere nulo (U+0000), é recusado também na parte que a leitura recusa, uma vez por
/// campo e sem repetir o texto enviado; o texto dentro de condição malformada não é conferido.
/// </summary>
public sealed class TextosNaoGravaveisNasPartesNaoLidasTests
{
    private const string Nulo = "\u0000";

    [Fact(DisplayName = "O rótulo com caractere nulo em item de tipo inexistente é recusado depois do tipo")]
    public void Ler_RotuloComNuloEmItemNaoLido_RecusaOTexto()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0, "texto").ComRotulo($"Rótulo{Nulo}").Construir();

        DaLeitura(Conteudo(item)).Should().Equal(
            ("itens[0].apresentacao.tipoDeCampo", ItemFormularioErrorCodes.TipoDeCampoInvalido),
            ("itens[0].apresentacao.rotulo", ModeloFormularioErrorCodes.TextoNaoGravavel));
    }

    [Fact(DisplayName = "A descrição com caractere nulo em página não lida é recusada")]
    public void Ler_DescricaoComNuloEmPaginaNaoLida_RecusaOTexto()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ComDescricao($"Descrição{Nulo}").Construir() with { Tipo = "secao" };

        DaLeitura(new ConteudoDoModeloInput(null, [pagina], null, null, null)).Should().Equal(
            ("paginas[0].tipo", EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio),
            ("paginas[0].apresentacao.descricao", ModeloFormularioErrorCodes.TextoNaoGravavel));
    }

    [Fact(DisplayName = "O código de documento com caractere nulo em documento não lido recusa o texto e o formato")]
    public void Ler_CodigoComNuloEmDocumentoNaoLido_RecusaOTextoEOFormato()
    {
        DocumentoExigidoDoModeloInput documento = new(
            $"FASE{Nulo}", "LAUDO_MEDICO", [[]], new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [], false));

        DaLeitura(Conteudo(Item("CERTIFICADO", 0)) with { ConjuntoDeDocumentos = new ConjuntoDeDocumentosDoModeloInput([documento], []) }).Select(static r => r.Codigo)
            .Should().Equal("ClausulaDnf.ClausulaVazia", ModeloFormularioErrorCodes.TextoNaoGravavel, ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido);
    }

    [Fact(DisplayName = "O texto dentro da condição malformada de item não lido não é conferido")]
    public void Ler_NuloDentroDeCondicaoMalformada_NaoRecusaOTexto()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0).ExibidoQuando([[], [new CondicaoPrecondicaoInput($"FATO{Nulo}", "IGUAL", System.Text.Json.JsonSerializer.SerializeToElement(true))]]).Construir();

        DaLeitura(Conteudo(item)).Select(static r => r.Codigo).Should().NotContain(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Fact(DisplayName = "Dois textos com nulo no mesmo campo da parte não lida geram uma recusa só")]
    public void Ler_DoisTextosComNuloNoMesmoCampo_RecusaUmaVez()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0).ComObrigatoriedade("INEXISTENTE")
            .ExibidoQuando([[new CondicaoPrecondicaoInput($"FATO{Nulo}", "IGUAL", System.Text.Json.JsonSerializer.SerializeToElement($"valor{Nulo}"))]]).Construir();

        DaLeitura(Conteudo(item)).Count(static r => r.Codigo == ModeloFormularioErrorCodes.TextoNaoGravavel).Should().Be(1);
    }

    [Fact(DisplayName = "O rótulo do grupo e o do campo de grupo com nulo, em grupo não lido, são recusados cada um uma vez")]
    public void Ler_RotulosComNuloEmGrupoNaoLido_RecusaCadaUm()
    {
        FatoColetadoInput campo = Montar("MAIOR_IDADE", 0).ComRotulo($"Campo{Nulo}").NaSecao(null).Construir();
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo($"Grupo{Nulo}").NaSecao("DADOS").ComOcorrencias(1, 10)
            .ComObrigatoriedade("TALVEZ").ComCampos(campo).Construir();

        DaLeitura(Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [grupo] })
            .Where(static r => r.Codigo == ModeloFormularioErrorCodes.TextoNaoGravavel).Select(static r => r.Campo).Should().Equal(
                "grupos[0].rotulo", "grupos[0].subitens[0].apresentacao.rotulo");
    }

    [Fact(DisplayName = "A recusa do texto não repete o texto enviado")]
    public void Ler_TextoComNuloEmItemNaoLido_NaoRepeteOTexto()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0, "texto").ComRotulo($"Segredo{Nulo}").Construir();
        LeituraDoConteudo leitura = Ler(Conteudo(item));

        leitura.Recusas.Select(static e => e.Error.Message).Should().OnlyContain(static m => !m.Contains("Segredo", StringComparison.Ordinal));
    }
}
