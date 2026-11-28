namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class RecusasAcumuladasDaPaginaNoEnvelopeTests
{
    private const string CodigoInvalido = "pagina-invalida";
    private const string Pagina = "formularios[0].paginas[";
    private static readonly DomainError ExibicaoIncoerente = new("CondicaoDnf.FormaIncoerenteComOperador", "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco.");
    private static readonly DomainError CodigoForaDoFormato = new("EstruturaFormulario.PaginaCodigoInvalido", "O código da página é obrigatório, começa com letra maiúscula e tem só letras maiúsculas, algarismos e sublinhados isolados, até 60 caracteres.");
    private static readonly DomainError ExibicaoEmBloco = new("EstruturaFormulario.ExibicaoForaDeSecao", "Só a seção tem exibição condicional; o bloco de sistema aparece sempre.");
    private static readonly DomainError OrdemNegativa = new("EstruturaFormulario.PaginaOrdemInvalida", "A ordem da página não pode ser negativa.");

    [Fact]
    public void LerFormularios_ExibicaoInvalidaECodigoForaDoFormato_PropagaAmbas()
    {
        JsonObject pagina = PaginaComExibicaoInvalida(CodigoInvalido, 0, "SECAO", null);
        Conferir([pagina], [(Pagina + "0].exibicao[0][0].valor", ExibicaoIncoerente), (Pagina + "0].codigo", CodigoForaDoFormato)]);
    }

    [Fact]
    public void LerFormularios_ExibicaoInvalidaEmBlocoDeSistema_PropagaAmbas()
    {
        JsonObject pagina = PaginaComExibicaoInvalida("REVISAO", 0, "BLOCO", "REVISAO_E_ACEITE");
        Conferir([pagina], [(Pagina + "0].exibicao[0][0].valor", ExibicaoIncoerente), (Pagina + "0].exibicao", ExibicaoEmBloco)]);
    }

    [Fact]
    public void LerFormularios_OrdemNegativaDepoisDeOutraPagina_RecusaSoAForma()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Pagina("PRIMEIRA", 5, "SECAO", null);
        JsonObject negativa = FormularioComTermosNoEnvelope.Pagina("SEGUNDA", -1, "SECAO", null);
        Conferir([anterior, negativa], [(Pagina + "1].ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerFormularios_ExibicaoInvalidaEOrdemForaDaSequencia_PropagaAmbas()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Pagina("PRIMEIRA", 1, "SECAO", null);
        JsonObject repetida = PaginaComExibicaoInvalida("SEGUNDA", 1, "SECAO", null);
        Conferir([anterior, repetida], [(Pagina + "1].exibicao[0][0].valor", ExibicaoIncoerente), (Pagina + "1]", OrdemForaDaSequencia(1))]);
    }

    [Fact]
    public void LerFormularios_CodigoForaDoFormatoEOrdemRepetida_PropagaAmbasNaOrdem()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Pagina("PRIMEIRA", 1, "SECAO", null);
        JsonObject repetida = FormularioComTermosNoEnvelope.Pagina(CodigoInvalido, 1, "SECAO", null);
        Conferir([anterior, repetida], [(Pagina + "1].codigo", CodigoForaDoFormato), (Pagina + "1]", OrdemForaDaSequencia(1))]);
    }

    [Fact]
    public void LerFormularios_CodigoForaDoFormatoEOrdemNegativa_NaoAcusaAOrdemForaDaSequencia()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Pagina("PRIMEIRA", 5, "SECAO", null);
        JsonObject negativa = FormularioComTermosNoEnvelope.Pagina(CodigoInvalido, -1, "SECAO", null);
        Conferir([anterior, negativa], [(Pagina + "1].codigo", CodigoForaDoFormato), (Pagina + "1].ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerFormularios_SoExibicaoInvalida_NaoGeraRecusaExtra()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Pagina("PRIMEIRA", 0, "SECAO", null);
        JsonObject pagina = PaginaComExibicaoInvalida("SEGUNDA", 1, "SECAO", null);
        Conferir([anterior, pagina], [(Pagina + "1].exibicao[0][0].valor", ExibicaoIncoerente)]);
    }

    [Fact]
    public void LerFormularios_SoOrdemForaDaSequencia_MantemCodigoETexto()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Pagina("PRIMEIRA", 3, "SECAO", null);
        JsonObject repetida = FormularioComTermosNoEnvelope.Pagina("SEGUNDA", 3, "SECAO", null);
        Conferir([anterior, repetida], [(Pagina + "1]", OrdemForaDaSequencia(1))]);
    }

    [Fact]
    public void LerFormularios_SoCodigoForaDoFormato_MantemCodigoETexto()
    {
        JsonObject pagina = FormularioComTermosNoEnvelope.Pagina(CodigoInvalido, 0, "SECAO", null);
        Conferir([pagina], [(Pagina + "0].codigo", CodigoForaDoFormato)]);
    }

    [Fact]
    public void LerFormularios_TipoForaDoFormatoComExibicaoInvalida_RecusaSoOFormato()
    {
        JsonObject pagina = PaginaComExibicaoInvalida("PRIMEIRA", 0, "TIPO_INEXISTENTE", null);
        LeitorEnvelope leitor = new();
        LeituraDosFormularios.LerFormularios(leitor, Documento([pagina]), FormulariosDoDocumentoDeTeste.SemFases);
        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Contain("paginas[0].tipo");
        leitor.Falha<object>().Errors.Should().ContainSingle().Which.Error.Should().Be(leitor.Erro);
    }

    private static DomainError OrdemForaDaSequencia(int indice) => new(ErrosCodecEnvelope.EnvelopeMalformado,
        $"'{Pagina}{indice}]' quebra a ordem crescente das páginas.");

    private static JsonObject PaginaComExibicaoInvalida(string codigo, int ordem, string tipo, string? bloco) =>
        FormularioComTermosNoEnvelope.Pagina(codigo, ordem, tipo, bloco, CondicoesNoEnvelope.Invalidas(false, true));

    private static JsonObject Documento(JsonObject[] paginas)
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"] = new JsonArray(paginas);
        return FormularioComTermosNoEnvelope.Documento(formulario);
    }

    private static void Conferir(JsonObject[] paginas, (string Campo, DomainError Erro)[] esperadas)
    {
        JsonObject documento = Documento(paginas);
        LeitorEnvelope leitor = new();
        LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Select(causa => (causa.Field!, causa.Error)).Should().Equal(esperadas);
        causas.Should().OnlyHaveUniqueItems();
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
        foreach (string valor in new[] { "VALOR_FICTICIO_X", CodigoInvalido })
            causas.Should().OnlyContain(causa => !causa.Error.Message.Contains(valor) && !causa.Field!.Contains(valor));
    }
}
