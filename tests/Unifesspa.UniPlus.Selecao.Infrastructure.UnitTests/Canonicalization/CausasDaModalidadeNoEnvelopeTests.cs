namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaModalidadeNoEnvelopeTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerModalidade_RegraInvalida_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject modalidade = Modalidade();
        modalidade["regraDeAplicabilidade"] = CondicoesNoEnvelope.Invalidas(separadas, unica);
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        CondicoesNoEnvelope.Conferir(new JsonObject { ["modalidades"] = new JsonArray(modalidade) }, leitor, "modalidades[0].regraDeAplicabilidade", separadas, unica);
    }

    [Fact]
    public void LerModalidade_ComposicaoInvalida_SituaCausaNoItem()
    {
        JsonObject modalidade = Modalidade();
        modalidade["composicaoOrigemCodigo"] = "OUTRA";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Should().Be(new FieldError("modalidades[0]", new DomainError("ModalidadeSelecionada.ComposicaoOrigemIndevida",
            "A modalidade só declara código de origem quando a composição de vagas é RETIRADA_DE_OUTRA_MODALIDADE.")));
        CaminhoNoDocumento.Existe(new JsonObject { ["modalidades"] = new JsonArray(modalidade) }, causa.Field!).Should().BeTrue();
    }

    [Fact]
    public void LerModalidade_OrigemEParIndevidos_PropagaCausasNoItem()
    {
        JsonObject modalidade = Modalidade();
        modalidade["composicaoOrigemCodigo"] = "OUTRA";
        modalidade["remanejamentoPar"] = "PAR";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Select(c => (c.Field, c.Error.Code)).Should().Equal(
            ("modalidades[0]", "ModalidadeSelecionada.ComposicaoOrigemIndevida"),
            ("modalidades[0]", "ModalidadeSelecionada.RemanejamentoCruzadoIndevido"));
        JsonObject documento = new() { ["modalidades"] = new JsonArray(modalidade) };
        causas.Should().OnlyContain(c => CaminhoNoDocumento.Existe(documento, c.Field!));
    }

    [Fact]
    public void LerModalidade_VocabularioInvalido_SituaCausaNoItem()
    {
        JsonObject modalidade = Modalidade();
        modalidade["codigo"] = "invalido";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Field.Should().Be("modalidades[0].codigo");
        causa.Error.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        causa.Error.Message.Should().Be("Envelope malformado em 'modalidades': o código não tem o formato do cadastro (A-Z, 0-9 e underscore) — e códigos são chave de composição e de remanejamento dentro da oferta.");
        causa.Error.Message.Should().NotContain("invalido");
        causa.Field.Should().NotContain("invalido");
        CaminhoNoDocumento.Existe(new JsonObject { ["modalidades"] = new JsonArray(modalidade) }, causa.Field!).Should().BeTrue();
    }

    [Fact]
    public void LerModalidade_RemanejamentoIncoerente_SituaCausaNoItem()
    {
        JsonObject modalidade = Modalidade();
        modalidade["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        modalidade["descricao"] = "Descrição fictícia da candidata Ana";
        modalidade["baseLegal"] = "Norma fictícia da candidata Ana";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Should().Be(new FieldError("modalidades[0]", new DomainError(ErrosCodecEnvelope.EnvelopeMalformado,
            "Envelope malformado em 'modalidades': a regra de remanejamento não é a que a natureza da modalidade pede.")));
        causa.Field.Should().NotContain("candidata Ana");
        causa.Error.Message.Should().NotContain("candidata Ana");
    }

    [Fact]
    public void LerModalidade_OrigemIndevidaERemanejamentoIncoerente_PropagaAmbasAsCausas()
    {
        JsonObject modalidade = Modalidade();
        modalidade["composicaoOrigemCodigo"] = "OUTRA";
        modalidade["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        Result<object> resultado = leitor.Falha<object>();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.ComposicaoOrigemIndevida");
        resultado.Errors.Select(causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("modalidades[0]", "ModalidadeSelecionada.ComposicaoOrigemIndevida"),
            ("modalidades[0]", ErrosCodecEnvelope.EnvelopeMalformado));
    }

    [Fact]
    public void LerModalidade_ParIndevidoERemanejamentoIncoerente_PropagaAmbasAsCausas()
    {
        JsonObject modalidade = Modalidade();
        modalidade["remanejamentoPar"] = "PAR";
        modalidade["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        Result<object> resultado = leitor.Falha<object>();
        resultado.Error!.Code.Should().Be("ModalidadeSelecionada.RemanejamentoCruzadoIndevido");
        resultado.Errors.Select(causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("modalidades[0]", "ModalidadeSelecionada.RemanejamentoCruzadoIndevido"),
            ("modalidades[0]", ErrosCodecEnvelope.EnvelopeMalformado));
    }

    [Fact]
    public void LerModalidade_CotaReservadaComDestinoUnico_RecusaSomenteCascata()
    {
        JsonObject modalidade = Modalidade();
        modalidade["naturezaLegal"] = "COTA_RESERVADA";
        modalidade["regraRemanejamento"] = "DESTINO_UNICO";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Field.Should().Be("modalidades[0]");
        causa.Error.Code.Should().Be("ModalidadeSelecionada.CotaReservadaExigeCascata");
    }

    [Fact]
    public void LerModalidade_AcaoAoIndeferirInvalida_RecusaVocabulario()
    {
        JsonObject modalidade = Modalidade();
        modalidade["acaoQuandoIndeferido"] = "ACAO_EXEMPLO_INVALIDA";
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Field.Should().Be("modalidades[0].acaoQuandoIndeferido");
        causa.Error.Message.Should().NotContain("ACAO_EXEMPLO_INVALIDA");
        causa.Field.Should().NotContain("ACAO_EXEMPLO_INVALIDA");
        causa.Error.Should().Be(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado,
            "Envelope malformado em 'modalidades': a ação ao indeferir não pertence ao domínio fechado do cadastro " +
            "(RETIRA_A_MODALIDADE) — restaurá-la daria à homologação das inscrições uma instrução que ela não sabe executar."));
    }

    [Fact]
    public void LerModalidade_NomeComEspacosNasPontas_RestauraONomeAparado()
    {
        JsonObject modalidade = Modalidade();
        modalidade["nome"] = "  Ampla concorrência  ";
        LeitorEnvelope leitor = new();
        ModalidadeSelecionada? lida = LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        leitor.Falhou.Should().BeFalse();
        lida!.Nome.Should().Be("Ampla concorrência");
    }

    [Fact]
    public void LerModalidade_SemNome_RecusaNoCampo()
    {
        JsonObject modalidade = Modalidade();
        modalidade.Remove("nome");
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Contain("nome");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LerModalidade_NomeVazio_RecusaNoCampo(string nome)
    {
        JsonObject modalidade = Modalidade();
        modalidade["nome"] = nome;
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        leitor.Erro.Should().Be(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado,
            "Envelope malformado em 'modalidades[0].nome': esperado um texto não vazio."));
    }

    [Fact]
    public void LerModalidade_NomeAcimaDoLimite_RecusaSemRepetirOValor()
    {
        string excedente = new('N', 201);
        JsonObject modalidade = Modalidade();
        modalidade["nome"] = excedente;
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().StartWith("Envelope malformado em 'modalidades[0].nome':");
        leitor.Erro.Message.Should().NotContain(excedente);
    }

    [Fact]
    public void LerModalidade_NomeNoLimite_Aceita()
    {
        JsonObject modalidade = Modalidade();
        modalidade["nome"] = new string('N', 200);
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, "modalidades[0]");
        leitor.Falhou.Should().BeFalse();
    }

    internal static JsonObject Modalidade() => JsonNode.Parse("""
        {"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","modalidadeOrigemId":"22222222-2222-2222-2222-222222222222",
        "codigo":"AC","nome":"Ampla concorrência","descricao":null,"naturezaLegal":"AMPLA","composicaoVagas":"RESTANTE_DAS_VAGAS_OFERTADAS",
        "composicaoOrigemCodigo":null,"regraRemanejamento":null,"remanejamentoDestino":null,"remanejamentoPar":null,
        "remanejamentoDestinoFinal":null,"regraDeAplicabilidade":null,"acaoQuandoIndeferido":null,"baseLegal":"Norma","quantidadeDeclarada":40}
        """)!.AsObject();
}
