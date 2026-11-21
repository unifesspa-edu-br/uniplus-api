namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

public sealed class RegrasDasDescricoesDeErrosTests
{
    private const string Codigo = "uniplus.exemplo.regra_nova";
    private const string OutroCodigo = "uniplus.exemplo.regra_antiga";
    private const string TerceiroCodigo = "uniplus.exemplo.terceira_regra";

    [Theory]
    [InlineData("Envie o PAYLOAD.", "termo de implementação fora de código")]
    [InlineData("Confira o end**point**.", "termo de implementação fora de código")]
    [InlineData(@"Confira \`payload\`.", "termo de implementação fora de código")]
    [InlineData("Regra RN87", "apelido antigo de requisito")]
    [InlineData("Regra R**N**87", "apelido antigo de requisito")]
    [InlineData("Regra REQ-193", "apelido antigo de requisito")]
    [InlineData("Identificador 000.000.000-00", "CPF")]
    [InlineData("Identificador 00000000000", "CPF")]
    [InlineData("Identificador 000000000000", "sequência de 11 ou mais dígitos")]
    [InlineData("Escreva para pessoa@example.invalid", "e-mail")]
    [InlineData("pessoa@[127.0.0.1]", "e-mail")]
    [InlineData("<section>Texto</section>", "HTML ou MDX")]
    [InlineData("Texto <Componente />", "HTML ou MDX")]
    [InlineData("Texto {renderizar()}", "HTML ou MDX")]
    [InlineData("{usuario}", "HTML ou MDX")]
    [InlineData(@"\`{usuario}\`", "HTML ou MDX")]
    [InlineData("{1 + 1}", "HTML ou MDX")]
    [InlineData("{a.b}", "HTML ou MDX")]
    [InlineData("{**usuario**}", "HTML ou MDX")]
    [InlineData("```json\n{\"id\": \"00000000000\"}\n```", "CPF")]
    [InlineData("```text\npessoa@example.invalid\n```", "e-mail")]
    [InlineData("```js\nvalor\n```", "citação ou bloco de código")]
    [InlineData("# Seção", "título")]
    [InlineData("Seção\n=====", "título")]
    [InlineData(":::warning\nTexto\n:::", "caixa de aviso")]
    [InlineData("[Guia](http://example.invalid)", "link fora de HTTPS ou do portal")]
    [InlineData("<http://example.invalid>", "link fora de HTTPS ou do portal")]
    [InlineData("[Guia](//example.invalid)", "link fora de HTTPS ou do portal")]
    [InlineData("[Guia](./outro.mdx)", "link fora de HTTPS ou do portal")]
    [InlineData("[Guia](javascript:abrir)", "link fora de HTTPS ou do portal")]
    [InlineData("> Texto", "citação ou bloco de código")]
    [InlineData("A primeira parte.\n\nA segunda parte.", "bloco fora do Markdown simples")]
    public void Conferir_QuandoBlocoViolaRegra_IdentificaCodigoESecao(string texto, string regra)
    {
        DescricaoDeErro entrada = Entrada() with { ComoResolver = [texto] };

        string[] falhas = ConferenciaDasDescricoesDeErros.Conferir(Documento(entrada), [Codigo]);

        falhas.Should().Contain($"{Codigo}: comoResolver, {regra}.").And
            .NotContain(falha => falha.Contains(texto, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("checagem")]
    [InlineData("agregado")]
    [InlineData("endpoint")]
    [InlineData("envelope")]
    [InlineData("snapshot")]
    [InlineData("handler")]
    [InlineData("value object")]
    [InlineData("codec")]
    [InlineData("guid")]
    [InlineData("query")]
    [InlineData("enum")]
    [InlineData("UUID")]
    [InlineData("bug")]
    [InlineData("validator")]
    [InlineData("janela")]
    [InlineData("janelas")]
    [InlineData("âncora")]
    [InlineData("ancorar")]
    [InlineData("ancorada")]
    [InlineData("portão")]
    [InlineData("portões")]
    [InlineData("gate")]
    [InlineData("gates")]
    public void Conferir_QuandoTermoEstaForaDeCodigo_RecusaMasAceitaEmCodigo(string termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        DescricaoDeErro fora = Entrada() with { ComoResolver = [$"Informe {termo.ToUpperInvariant()}."] };
        DescricaoDeErro emCodigo = Entrada() with { ComoResolver = [$"Informe `{termo}`."] };

        ConferenciaDasDescricoesDeErros.Conferir(Documento(fora), [Codigo])
            .Should().Contain($"{Codigo}: comoResolver, termo de implementação fora de código.");
        ConferenciaDasDescricoesDeErros.Conferir(Documento(emCodigo), [Codigo]).Should().BeEmpty();
    }

    [Fact]
    public void Conferir_QuandoBlocosUsamMarkdownSimples_AceitaAEntrada()
    {
        DescricaoDeErro entrada = Entrada() with
        {
            OQueAconteceu = ["A solicitação viola UNI-REQ-0001."],
            ComoResolver = ["- Confira o campo.\n- Tente de novo.", "| Campo | Valor |\n| --- | --- |\n| CPF | ***.999.999-** |"],
            OndeAparece = ["Consulte o [catálogo](/erros/) ou a [referência](https://example.invalid/guia).", "```json\n{\"campo\": 1}\n```", "Texto `{usuario}`.", "Use `endpoint` e `UUID`.", "```text\npayload handler\n```", "```text\n{a.b}\n```"],
        };

        ConferenciaDasDescricoesDeErros.Conferir(Documento(entrada), [Codigo]).Should().BeEmpty();
    }

    [Fact]
    public void Conferir_QuandoSecoesOuRequisitosFalham_JuntaOcorrencias()
    {
        DescricaoDeErro entrada = Entrada() with { Requisitos = ["REQ-193"], OQueAconteceu = null, ComoResolver = [null] };

        string[] falhas = ConferenciaDasDescricoesDeErros.Conferir(Documento(entrada), [Codigo]);

        falhas.Should().Equal(
            $"{Codigo}: requisitos devem usar UNI-REQ-NNNN.",
            $"{Codigo}: oQueAconteceu deve conter pelo menos um bloco.",
            $"{Codigo}: comoResolver, bloco vazio.");
    }

    [Fact]
    public void Conferir_QuandoParesNaoConferem_JuntaTodasAsDivergencias()
    {
        DocumentoDeDescricoesDeErros documento = Documento(Entrada(OutroCodigo), Entrada(OutroCodigo));

        string[] falhas = ConferenciaDasDescricoesDeErros.Conferir(documento, [Codigo, TerceiroCodigo]);

        falhas.Should().Equal(
            $"{Codigo}: código sem descrição.",
            $"{TerceiroCodigo}: código sem descrição.",
            $"{OutroCodigo}: descrição sem código.",
            $"{OutroCodigo}: descrição repetida.");
    }

    [Fact]
    public void ConferirConteudo_QuandoForaDoPadrao_SugereAReformatacao()
    {
        DocumentoDeDescricoesDeErros documento = Documento(Entrada(), Entrada(OutroCodigo));
        string foraDeOrdem = documento.SerializarEmOrdem().Replace(OutroCodigo, TerceiroCodigo, StringComparison.Ordinal);

        string[] falhas = DescricoesDeErrosPublicadasTests.ConferirConteudo(foraDeOrdem, [Codigo, TerceiroCodigo], null);

        falhas.Should().ContainSingle().Which.Should().Contain(DescricoesDeErrosPublicadasTests.VariavelDeReformatacao);
    }

    [Fact]
    public void ConferirConteudo_AoReformatar_PreservaTextosECodigoSemDescricao()
    {
        string original = Documento(Entrada()).SerializarEmOrdem();
        string? gravado = null;

        string[] falhas = DescricoesDeErrosPublicadasTests.ConferirConteudo(original.Trim(), [Codigo, OutroCodigo], texto => gravado = texto);

        gravado.Should().Be(original);
        falhas.Should().Equal($"{OutroCodigo}: código sem descrição.");
    }

    [Theory]
    [InlineData("""{"versaoDoFormato":4,"descricoes":[]}""", "versão do formato desconhecida")]
    [InlineData("""{"versaoDoFormato":1}""", "falta a lista de descrições")]
    [InlineData("""{"versaoDoFormato":1,"descricoes":[],"campoExtra":"texto"}""", "campo fora do formato")]
    public void ConferirConteudo_QuandoFormatoNaoEValido_IndicaRegraSemTexto(string conteudo, string regra)
    {
        string[] falhas = DescricoesDeErrosPublicadasTests.ConferirConteudo(conteudo, [], null);

        falhas.Should().Contain(falha => falha.Contains(regra, StringComparison.Ordinal));
    }

    private static DescricaoDeErro Entrada(string codigo = Codigo) =>
        new(codigo, ["UNI-REQ-0001"], ["O campo não foi preenchido."], ["Preencha o campo."]);

    private static DocumentoDeDescricoesDeErros Documento(params DescricaoDeErro[] descricoes) =>
        new(DocumentoDeDescricoesDeErros.VersaoAtual, descricoes);
}
