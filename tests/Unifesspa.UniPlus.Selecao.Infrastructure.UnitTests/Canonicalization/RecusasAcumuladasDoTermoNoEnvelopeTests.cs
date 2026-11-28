namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class RecusasAcumuladasDoTermoNoEnvelopeTests
{
    private const string CodigoInvalido = "termo-invalido";
    private const string Termo = "formularios[0].termos[";
    private static readonly DomainError CondicaoIncoerente = new("CondicaoDnf.FormaIncoerenteComOperador", "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco.");
    private static readonly DomainError CodigoForaDoFormato = new("TermoFormulario.CodigoFormatoInvalido", "O código do termo exigido começa com letra maiúscula e tem só letras maiúsculas, algarismos e sublinhados isolados (ex.: CIENCIA_DO_EDITAL).");
    private static readonly DomainError OrdemNegativa = new("TermoFormulario.OrdemInvalida", "A ordem do termo não pode ser negativa.");

    [Fact]
    public void LerFormularios_ExibicaoEObrigatoriedadeInvalidas_PropagaAmbasCadaUmaNoSeuCaminho()
    {
        JsonObject termo = TermoComObrigatoriedadeInvalida("PRIMEIRO", 0, ExibicaoInvalida());
        Conferir([termo], [(Termo + "0].exibicao[0][0].valor", CondicaoIncoerente), (Termo + "0].obrigatoriedade.quando[0][0].valor", CondicaoIncoerente)]);
    }

    [Fact]
    public void LerFormularios_ExibicaoInvalidaEOrdemNegativa_PropagaAmbas()
    {
        JsonObject termo = TermoComExibicaoInvalida("PRIMEIRO", -1);
        Conferir([termo], [(Termo + "0].exibicao[0][0].valor", CondicaoIncoerente), (Termo + "0].ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerFormularios_ExibicaoInvalidaECodigoForaDoFormato_PropagaAmbas()
    {
        JsonObject termo = TermoComExibicaoInvalida(CodigoInvalido, 0);
        Conferir([termo], [(Termo + "0].exibicao[0][0].valor", CondicaoIncoerente), (Termo + "0].codigo", CodigoForaDoFormato)]);
    }

    [Fact]
    public void LerFormularios_ObrigatoriedadeInvalidaEOrdemRepetidaEntreTermos_PropagaAmbas()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 1);
        JsonObject repetido = TermoComObrigatoriedadeInvalida("SEGUNDO", 1, null);
        Conferir([anterior, repetido], [(Termo + "1].obrigatoriedade.quando[0][0].valor", CondicaoIncoerente), (Termo + "1]", OrdemForaDaSequencia(1))]);
    }

    [Fact]
    public void LerFormularios_SoExibicaoInvalida_NaoGeraRecusaExtra()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 0);
        JsonObject termo = TermoComExibicaoInvalida("SEGUNDO", 1);
        Conferir([anterior, termo], [(Termo + "1].exibicao[0][0].valor", CondicaoIncoerente)]);
    }

    [Fact]
    public void LerFormularios_SoObrigatoriedadeInvalida_NaoGeraRecusaExtra()
    {
        JsonObject termo = TermoComObrigatoriedadeInvalida("PRIMEIRO", 0, null);
        Conferir([termo], [(Termo + "0].obrigatoriedade.quando[0][0].valor", CondicaoIncoerente)]);
    }

    [Fact]
    public void LerFormularios_SoCodigoForaDoFormato_MantemCodigoETexto()
    {
        Conferir([FormularioComTermosNoEnvelope.Termo(CodigoInvalido, 0)], [(Termo + "0].codigo", CodigoForaDoFormato)]);
    }

    [Fact]
    public void LerFormularios_SoOrdemNegativa_MantemCodigoETexto()
    {
        Conferir([FormularioComTermosNoEnvelope.Termo("PRIMEIRO", -1)], [(Termo + "0].ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerFormularios_SoOrdemRepetidaEntreTermos_ApontaOTermo()
    {
        JsonObject anterior = FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 3);
        JsonObject repetido = FormularioComTermosNoEnvelope.Termo("SEGUNDO", 3);
        Conferir([anterior, repetido], [(Termo + "1]", OrdemForaDaSequencia(1))]);
    }

    [Fact]
    public void LerFormularios_TipoDaObrigatoriedadeForaDoFormatoComRecusasDeRegra_EncerraOItemComAsRecusasJaJuntadas()
    {
        JsonObject termo = TermoComExibicaoInvalida(CodigoInvalido, -1);
        termo["obrigatoriedade"] = new JsonObject { ["tipo"] = "TIPO_INEXISTENTE", ["quando"] = null };
        JsonObject documento = Documento([termo]);
        LeitorEnvelope leitor = new();
        LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;

        causas.Should().HaveCount(2);
        causas[0].Should().Be(new FieldError(Termo + "0].exibicao[0][0].valor", CondicaoIncoerente));
        causas[1].Error.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        causas[1].Error.Message.Should().Contain("termos[0].obrigatoriedade.tipo");
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains(CodigoInvalido) && !(causa.Field ?? string.Empty).Contains(CodigoInvalido));
    }

    [Fact]
    public void LerFormularios_PrimeiroTermoRecusadoEmDoisTermos_ParaNoPrimeiro()
    {
        JsonObject primeiro = TermoComExibicaoInvalida("PRIMEIRO", 0);
        JsonObject segundo = TermoComExibicaoInvalida("SEGUNDO", 1);
        Conferir([primeiro, segundo], [(Termo + "0].exibicao[0][0].valor", CondicaoIncoerente)]);
    }

    private static DomainError OrdemForaDaSequencia(int indice) => new(ErrosCodecEnvelope.EnvelopeMalformado,
        $"'{Termo}{indice}]' repete código ou quebra a ordem crescente dos termos.");

    private static JsonArray ExibicaoInvalida() => CondicoesNoEnvelope.Invalidas(false, true);

    private static JsonObject TermoComExibicaoInvalida(string codigo, int ordem) =>
        FormularioComTermosNoEnvelope.Termo(codigo, ordem, ExibicaoInvalida());

    private static JsonObject TermoComObrigatoriedadeInvalida(string codigo, int ordem, JsonNode? exibicao)
    {
        JsonObject termo = FormularioComTermosNoEnvelope.Termo(codigo, ordem, exibicao);
        termo["obrigatoriedade"] = new JsonObject { ["tipo"] = "QUANDO", ["quando"] = CondicoesNoEnvelope.Invalidas(false, true) };
        return termo;
    }

    private static JsonObject Documento(JsonObject[] termos) =>
        FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO", termos));

    private static void Conferir(JsonObject[] termos, (string Campo, DomainError Erro)[] esperadas)
    {
        JsonObject documento = Documento(termos);
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
