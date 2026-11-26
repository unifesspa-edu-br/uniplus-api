namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class TermosDoFormularioNoEnvelopeTests
{
    [Fact]
    public void LerFormularios_TresTermosEmOrdem_PreservaAOrdemDeclarada()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 1), FormularioComTermosNoEnvelope.Termo("SEGUNDO", 2),
            FormularioComTermosNoEnvelope.Termo("TERCEIRO", 3)));
        LeitorEnvelope leitor = new();
        (IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos) =
            LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeFalse();
        formularios.Should().ContainSingle();
        termos.Select(termo => termo.Codigo).Should().Equal("PRIMEIRO", "SEGUNDO", "TERCEIRO");
        termos.Select(termo => termo.Ordem).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void LerFormularios_CodigoRepetidoNoTerceiroTermo_RecusaOTerceiro()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 1), FormularioComTermosNoEnvelope.Termo("SEGUNDO", 2),
            FormularioComTermosNoEnvelope.Termo("SEGUNDO", 3)));

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "'formularios[0].termos[2]' repete código ou quebra a ordem crescente dos termos.");
    }

    [Fact]
    public void LerFormularios_TerceiroTermoRepeteCodigoDoPrimeiro_RecusaOTerceiro()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 1), FormularioComTermosNoEnvelope.Termo("SEGUNDO", 2),
            FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 3)));

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "'formularios[0].termos[2]' repete código ou quebra a ordem crescente dos termos.");
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(2, 1)]
    public void LerFormularios_OrdemIgualOuMenorQueAAnterior_Recusa(int primeiraOrdem, int segundaOrdem)
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo("PRIMEIRO", primeiraOrdem), FormularioComTermosNoEnvelope.Termo("SEGUNDO", segundaOrdem)));

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "'formularios[0].termos[1]' repete código ou quebra a ordem crescente dos termos.");
    }

    [Fact]
    public void LerFormularios_PrimeiroTermoComOrdemZero_Aceita()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 0)));
        LeitorEnvelope leitor = new();
        (IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos) =
            LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeFalse();
        formularios.Should().ContainSingle();
        termos.Should().ContainSingle().Which.Ordem.Should().Be(0);
    }

    [Fact]
    public void LerFormularios_SemTermos_DevolveFormularioSemTermos()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO"));
        LeitorEnvelope leitor = new();
        (IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos) =
            LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeFalse();
        formularios.Should().ContainSingle();
        termos.Should().BeEmpty();
    }

    [Fact]
    public void LerFormularios_MesmoTermoEmFormulariosDistintos_Aceita()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(
            FormularioComTermosNoEnvelope.Formulario("HABILITACAO", FormularioComTermosNoEnvelope.Termo("TERMO", 1)),
            FormularioComTermosNoEnvelope.Formulario("INSCRICAO", FormularioComTermosNoEnvelope.Termo("TERMO", 1)));
        LeitorEnvelope leitor = new();
        (IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos) =
            LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeFalse();
        formularios.Select(formulario => formulario.Codigo.Valor).Should().Equal("HABILITACAO", "INSCRICAO");
        termos.Select(termo => termo.FormularioId).Should().Equal(formularios.Select(formulario => (Guid?)formulario.Id));
        termos.Select(termo => termo.Codigo).Should().Equal("TERMO", "TERMO");
        termos.Select(termo => termo.Ordem).Should().Equal(1, 1);
    }

    [Fact]
    public void LerFormularios_SegundoTermoNaoEObjeto_RecusaComCaminho()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO", FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 1));
        formulario["termos"]!.AsArray().Add("inválido");
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(formulario);

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "Envelope malformado em 'formularios[0].termos[1]': esperado um objeto.");
    }

    [Fact]
    public void LerFormularios_PaginaNaoEObjeto_RecusaComCaminho()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"]!.AsArray()[0] = JsonValue.Create("inválido");
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(formulario);

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "Envelope malformado em 'formularios[0].paginas[0]': esperado um objeto.");
    }

    [Fact]
    public void LerFormularios_FormularioNaoEObjeto_RecusaComCaminho()
    {
        JsonObject documento = new() { ["formularios"] = new JsonArray(JsonValue.Create("inválido")) };

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "Envelope malformado em 'formularios[0]': esperado um objeto.");
    }

    [Fact]
    public void LerFormularios_ChaveAusenteNoSegundoTermo_RecusaComCaminho()
    {
        JsonObject termo = FormularioComTermosNoEnvelope.Termo("SEGUNDO", 2);
        termo.Remove("nome");
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo("PRIMEIRO", 1), termo));

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "Envelope malformado em 'formularios[0].termos[1]': a chave 'nome' está ausente.");
    }

    [Fact]
    public void LerFormularios_CodigoRepetidoEOrdemNegativa_RecusaAFormaPrimeiro()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo("TERMO", 1), FormularioComTermosNoEnvelope.Termo("TERMO", -1)));

        ConferirRecusa(documento, "TermoFormulario.OrdemInvalida", "A ordem do termo não pode ser negativa.");
    }

    [Fact]
    public void LerFormularios_CodigoForaDeOrdemETermoRepetido_RecusaOCodigoPrimeiro()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO"),
            FormularioComTermosNoEnvelope.Formulario("HABILITACAO", FormularioComTermosNoEnvelope.Termo("TERMO", 1),
                FormularioComTermosNoEnvelope.Termo("TERMO", 2)));

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "'formularios[1].codigo' fora do formato, repetido ou fora da ordem ordinal.");
    }

    [Fact]
    public void LerFormularios_CodigoRepetido_Recusa()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO"),
            FormularioComTermosNoEnvelope.Formulario("INSCRICAO"));

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "'formularios[1].codigo' fora do formato, repetido ou fora da ordem ordinal.");
    }

    [Theory]
    [InlineData("inscricao")]
    [InlineData("INSCRICAO__DADOS")]
    [InlineData("1INSCRICAO")]
    public void LerFormularios_CodigoForaDoFormato_Recusa(string codigo)
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario(codigo));

        ConferirRecusa(documento, "EnvelopeCodec.EnvelopeMalformado", "'formularios[0].codigo' fora do formato, repetido ou fora da ordem ordinal.");
    }

    [Fact]
    public void LerFormularios_CodigoComMaisDeSessentaCaracteres_Recusa()
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario(new string('A', 61)));

        LeitorEnvelope leitor = new();
        LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("formularios[0].codigo");
    }

    [Fact]
    public void LerFormularios_ObrigatorioEFase_ChegamAoFormularioLido()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("HABILITACAO");
        formulario["obrigatorio"] = false;
        formulario["faseId"] = "44444444-4444-4444-8444-444444444444";
        LeitorEnvelope leitor = new();

        (IReadOnlyList<FormularioProcesso> formularios, _) =
            LeituraDosFormularios.LerFormularios(leitor, FormularioComTermosNoEnvelope.Documento(formulario), FormulariosDoDocumentoDeTeste.SemFases);

        FormularioProcesso lido = formularios.Should().ContainSingle().Subject;
        lido.Obrigatorio.Should().BeFalse();
        lido.FaseId.Should().Be(new Guid("44444444-4444-4444-8444-444444444444"));
    }

    [Fact]
    public void LerFormularios_ObrigatorioAusente_Recusa()
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario.Remove("obrigatorio");

        ConferirRecusa(FormularioComTermosNoEnvelope.Documento(formulario), "EnvelopeCodec.EnvelopeMalformado",
            "Envelope malformado em 'formularios[0]': a chave 'obrigatorio' está ausente.");
    }

    [Theory]
    [InlineData("TERMO", " TERMO ")]
    [InlineData("AÇÕES", "AC\u0327O\u0303ES")]
    public void LerFormularios_CodigosForaDoFormato_Recusa(string primeiroCodigo, string segundoCodigo)
    {
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(FormularioComTermosNoEnvelope.Formulario("INSCRICAO",
            FormularioComTermosNoEnvelope.Termo(primeiroCodigo, 1), FormularioComTermosNoEnvelope.Termo(segundoCodigo, 2)));

        ConferirRecusa(documento, "TermoFormulario.CodigoFormatoInvalido", "O código do termo exigido começa com letra maiúscula e tem só letras maiúsculas, algarismos e sublinhados isolados (ex.: CIENCIA_DO_EDITAL).");
    }

    private static void ConferirRecusa(JsonObject documento, string codigo, string mensagem)
    {
        LeitorEnvelope leitor = new();
        (IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos) =
            LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(codigo);
        leitor.Erro.Message.Should().Be(mensagem);
        formularios.Should().BeEmpty();
        termos.Should().BeEmpty();
    }
}
