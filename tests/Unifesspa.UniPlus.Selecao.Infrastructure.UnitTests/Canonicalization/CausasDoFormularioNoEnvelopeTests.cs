namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDoFormularioNoEnvelopeTests
{
    private const string ValorFicticio = "VALOR FICTÍCIO RECONHECÍVEL";

    [Fact]
    public void LerFormularios_ExibicaoDaSegundaPaginaRecusada_SituaCausaNaExibicao()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"] = new JsonArray(FormularioComTermosNoEnvelope.Pagina("PRIMEIRA", 0, "SECAO", null),
            FormularioComTermosNoEnvelope.Pagina("SEGUNDA", 1, "SECAO", null, ExibicaoIncoerente()));
        IReadOnlyList<FieldError> causas = LerCausas(FormularioComTermosNoEnvelope.Documento(formulario));

        causas.Should().Equal(FormaIncoerente("formularios[0].paginas[1].exibicao[0][0].valor"));
        ConferirAusenciaDoTexto(causas, ValorFicticio);
    }

    [Fact]
    public void LerFormularios_ExibicaoDoSegundoTermoRecusada_SituaCausaNaExibicao()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO", FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 0),
            FormularioComTermosNoEnvelope.Termo("SEGUNDO", 1, ExibicaoIncoerente()));
        IReadOnlyList<FieldError> causas = LerCausas(FormularioComTermosNoEnvelope.Documento(formulario));

        causas.Should().Equal(FormaIncoerente("formularios[0].termos[1].exibicao[0][0].valor"));
        ConferirAusenciaDoTexto(causas, ValorFicticio);
    }

    [Fact]
    public void LerFormularios_SegundoFormularioSemRevisao_SituaCausaNoFormulario()
    {
        JsonObject segundo = FormularioComTermosNoEnvelope.Formulario("HABILITACAO");
        segundo["paginas"] = new JsonArray();
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO"), segundo);

        LerCausas(documento).Should().Equal(BlocoExigido("formularios[1].paginas"));
    }

    [Fact]
    public void LerFormularios_SecaoComBlocoERevisaoForaDoFim_PropagaTodasAsCausas()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"] = new JsonArray(FormularioComTermosNoEnvelope.PaginaDeRevisao(),
            FormularioComTermosNoEnvelope.Pagina("SECAO", 1, "SECAO", "COMPROVACAO_DOCUMENTAL"));

        LerCausas(FormularioComTermosNoEnvelope.Documento(formulario)).Should().Equal(
            new FieldError("formularios[0].paginas[1].bloco", new DomainError("EstruturaFormulario.SecaoComBloco", "Uma seção não declara bloco de sistema.")),
            new FieldError("formularios[0].paginas", new DomainError("EstruturaFormulario.RevisaoEAceiteForaDoFim", "A revisão e aceite é sempre a última página do formulário.")));
    }

    [Fact(DisplayName = "Páginas com código repetido não expõem o valor em nenhuma causa")]
    public void LerFormularios_PaginasComCodigoDuplicado_NaoExpoeCodigoNasCausas()
    {
        const string codigoFicticio = "PAGINA_FICTICIA_X";
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"] = new JsonArray(
            FormularioComTermosNoEnvelope.Pagina(codigoFicticio, 0, "SECAO", "COMPROVACAO_DOCUMENTAL"),
            FormularioComTermosNoEnvelope.Pagina(codigoFicticio, 1, "SECAO", null),
            FormularioComTermosNoEnvelope.Pagina("REVISAO", 2, "BLOCO", "REVISAO_E_ACEITE"));
        IReadOnlyList<FieldError> causas = LerCausas(FormularioComTermosNoEnvelope.Documento(formulario));

        causas.Should().Contain(new FieldError("formularios[0].paginas[1].codigo",
            new DomainError("EstruturaFormulario.PaginaCodigoDuplicado", "Existe outra página com o mesmo código no formulário.")));
        causas.Should().Contain(causa => causa.Error.Code == "EstruturaFormulario.SecaoComBloco");
        ConferirAusenciaDoTexto(causas, codigoFicticio);
    }

    [Fact]
    public void LerFormularios_SegundaPaginaDeBlocoComExibicao_SituaCausaNaPagina()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"] = new JsonArray(FormularioComTermosNoEnvelope.Pagina("SECAO", 0, "SECAO", null),
            FormularioComTermosNoEnvelope.Pagina("REVISAO", 1, "BLOCO", "REVISAO_E_ACEITE", FormularioComTermosNoEnvelope.Exibicao()));

        LerCausas(FormularioComTermosNoEnvelope.Documento(formulario)).Should().Equal(ExibicaoEmBloco("formularios[0].paginas[1].exibicao"));
    }

    [Fact]
    public void LerFormularios_PaginaComOrdemNegativaEExibicao_PropagaTodasAsCausas()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"] = new JsonArray(FormularioComTermosNoEnvelope.Pagina("REVISAO", -1, "BLOCO",
            "REVISAO_E_ACEITE", FormularioComTermosNoEnvelope.Exibicao()));

        LerCausas(FormularioComTermosNoEnvelope.Documento(formulario)).Should().Equal(
            OrdemDaPagina("formularios[0].paginas[0].ordem"), ExibicaoEmBloco("formularios[0].paginas[0].exibicao"));
    }

    [Fact]
    public void LerFormularios_TerceiroTermoComOrdemNegativa_SituaCausaNoTermo()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO", FormularioComTermosNoEnvelope.Termo("UM", 0),
            FormularioComTermosNoEnvelope.Termo("DOIS", 1), FormularioComTermosNoEnvelope.Termo("TRES", -1));

        LerCausas(FormularioComTermosNoEnvelope.Documento(formulario)).Should().Equal(OrdemDoTermo("formularios[0].termos[2].ordem"));
    }

    [Fact]
    public void LerFormularios_DoisTermosComOrdemNegativa_ParaNoPrimeiro()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO", FormularioComTermosNoEnvelope.Termo("UM", -1),
            FormularioComTermosNoEnvelope.Termo("DOIS", -1, FormularioComTermosNoEnvelope.Exibicao()));

        LerCausas(FormularioComTermosNoEnvelope.Documento(formulario)).Should().Equal(OrdemDoTermo("formularios[0].termos[0].ordem"));
    }

    [Fact]
    public void LerFormularios_DoisFormulariosSemRevisao_ParaNoPrimeiro()
    {
        JsonObject primeiro = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        JsonObject segundo = FormularioComTermosNoEnvelope.Formulario("HABILITACAO");
        primeiro["paginas"] = new JsonArray();
        segundo["paginas"] = new JsonArray();

        LerCausas(FormularioComTermosNoEnvelope.Documento(primeiro, segundo)).Should().Equal(BlocoExigido("formularios[0].paginas"));
    }

    [Fact]
    public void LerFormularios_TextoDoTermoRecusado_NaoExpoeConteudoNaCausa()
    {
        const string textoFicticio = "CONTEÚDO FICTÍCIO RECONHECÍVEL";
        JsonObject termo = FormularioComTermosNoEnvelope.Termo("TERMO", -1);
        termo["nome"] = textoFicticio;
        termo["texto"] = textoFicticio;
        IReadOnlyList<FieldError> causas = LerCausas(FormularioComTermosNoEnvelope.Documento(
            FormularioComTermosNoEnvelope.Formulario("INSCRICAO", termo)));

        causas.Should().Equal(OrdemDoTermo("formularios[0].termos[0].ordem"));
        ConferirAusenciaDoTexto(causas, textoFicticio);
    }

    [Fact]
    public void LerFormularios_TituloDaPaginaRecusada_NaoExpoeConteudoNaCausa()
    {
        const string textoFicticio = "CONTEÚDO FICTÍCIO RECONHECÍVEL";
        JsonObject pagina = FormularioComTermosNoEnvelope.Pagina("PAGINA", -1, "BLOCO", "REVISAO_E_ACEITE");
        pagina["titulo"] = textoFicticio;
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"] = new JsonArray(pagina);
        IReadOnlyList<FieldError> causas = LerCausas(FormularioComTermosNoEnvelope.Documento(formulario));

        causas.Should().Equal(OrdemDaPagina("formularios[0].paginas[0].ordem"));
        ConferirAusenciaDoTexto(causas, textoFicticio);
    }

    private static IReadOnlyList<FieldError> LerCausas(JsonObject documento)
    {
        LeitorEnvelope leitor = new();
        LeituraDosFormularios.LerFormularios(leitor, documento);
        leitor.Falhou.Should().BeTrue();
        return leitor.Falha<object>().Errors;
    }

    private static void ConferirAusenciaDoTexto(IReadOnlyList<FieldError> causas, string texto)
    {
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains(texto, StringComparison.Ordinal)
            && (causa.Field == null || !causa.Field.Contains(texto, StringComparison.Ordinal)));
    }

    private static JsonArray ExibicaoIncoerente() => new(new JsonArray(new JsonObject
    {
        ["codigoDoDado"] = "FATO",
        ["operador"] = "IGUAL",
        ["valor"] = new JsonArray(ValorFicticio),
    }));

    private static FieldError FormaIncoerente(string campo) => new(campo,
        new DomainError("CondicaoDnf.FormaIncoerenteComOperador",
            "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco."));

    private static FieldError BlocoExigido(string campo) => new(campo,
        new DomainError("EstruturaFormulario.BlocoExigidoAusente", "O formulário precisa do bloco REVISAO_E_ACEITE."));

    private static FieldError ExibicaoEmBloco(string campo) => new(campo,
        new DomainError("EstruturaFormulario.ExibicaoForaDeSecao", "Só a seção tem exibição condicional; o bloco de sistema aparece sempre."));

    private static FieldError OrdemDaPagina(string campo) => new(campo,
        new DomainError("EstruturaFormulario.PaginaOrdemInvalida", "A ordem da página não pode ser negativa."));

    private static FieldError OrdemDoTermo(string campo) => new(campo,
        new DomainError("TermoFormulario.OrdemInvalida", "A ordem do termo não pode ser negativa."));
}
