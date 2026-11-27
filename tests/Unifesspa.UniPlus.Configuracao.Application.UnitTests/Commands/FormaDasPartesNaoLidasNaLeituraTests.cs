namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

/// <summary>O item, o termo e a página que a leitura recusa têm a forma básica conferida mesmo assim, e as duas recusas saem juntas.</summary>
public sealed class FormaDasPartesNaoLidasNaLeituraTests
{
    private static readonly Dictionary<string, string> SemFormatos = new(StringComparer.Ordinal);

    [Fact(DisplayName = "O item com tipo de campo inválido e rótulo vazio recusa os dois")]
    public void Ler_ItemComTipoInvalidoESemRotulo_RecusaOsDois()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0, "texto").ComRotulo(string.Empty).Construir();

        IReadOnlyList<string> campos = Ler(new ConteudoDoModeloInput(null, null, [item], null, null));

        campos.Should().Equal("itens[0].apresentacao.tipoDeCampo", "itens[0].apresentacao.rotulo");
    }

    [Fact(DisplayName = "O termo com obrigatoriedade inválida e código inválido recusa os dois")]
    public void Ler_TermoComObrigatoriedadeInvalidaECodigoInvalido_RecusaOsDois()
    {
        TermoExigidoInput termo = EntradaDoTermoExigidoDeTeste.Exigindo("TERMO RUIM").ComObrigatoriedade("TALVEZ").Construir();

        IReadOnlyList<string> campos = Ler(new ConteudoDoModeloInput(null, null, null, [termo], null));

        campos.Should().Equal("termos[0].regras.obrigatoriedade", "termos[0].codigo");
    }

    [Fact(DisplayName = "A página com tipo inválido e código inválido recusa os dois")]
    public void Ler_PaginaComTipoInvalidoECodigoInvalido_RecusaOsDois()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao("PAGINA RUIM").ComTitulo("Dados").Construir() with { Tipo = "secao" };

        IReadOnlyList<string> campos = Ler(new ConteudoDoModeloInput(null, [pagina], null, null, null));

        campos.Should().Equal("paginas[0].tipo", "paginas[0].codigo");
    }

    [Fact(DisplayName = "A página com exibição e tipo inválido não recusa a exibição por não saber o tipo")]
    public void Ler_PaginaComExibicaoETipoInvalido_NaoConfereAExibicaoPeloTipo()
    {
        PaginaDoFormularioInput comExibicao = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ExibidaQuando(Quando("CERTIFICADO", true)).Construir();
        PaginaDoFormularioInput pagina = comExibicao with { Tipo = "secao" };

        IReadOnlyList<string> campos = Ler(new ConteudoDoModeloInput(null, [pagina], null, null, null));

        campos.Should().Equal("paginas[0].tipo");
    }

    private static IReadOnlyList<string> Ler(ConteudoDoModeloInput entrada)
    {
        LeituraDoConteudo leitura = new(SemFormatos);
        leitura.Ler(entrada);
        return [.. leitura.Recusas.Select(static e => e.Field!)];
    }
}
