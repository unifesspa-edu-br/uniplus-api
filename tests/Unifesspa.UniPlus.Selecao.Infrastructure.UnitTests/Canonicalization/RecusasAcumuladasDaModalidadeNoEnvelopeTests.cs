namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class RecusasAcumuladasDaModalidadeNoEnvelopeTests
{
    private const string Envelope = ErrosCodecEnvelope.EnvelopeMalformado;
    private static readonly DomainError Codigo = new(Envelope, "Envelope malformado em 'modalidades': o código não tem o formato do cadastro (A-Z, 0-9 e underscore) — e códigos são chave de composição e de remanejamento dentro da oferta.");
    private static readonly DomainError Acao = new(Envelope, "Envelope malformado em 'modalidades': a ação ao indeferir não pertence ao domínio fechado do cadastro (RETIRA_A_MODALIDADE) — restaurá-la daria à homologação das inscrições uma instrução que ela não sabe executar.");
    private static readonly DomainError Remanejamento = new(Envelope, "Envelope malformado em 'modalidades': a regra de remanejamento não é a que a natureza da modalidade pede.");
    private static readonly DomainError Regra = new("CondicaoDnf.FormaIncoerenteComOperador", "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco.");
    private static readonly DomainError Origem = new("ModalidadeSelecionada.ComposicaoOrigemIndevida", "A modalidade só declara código de origem quando a composição de vagas é RETIRADA_DE_OUTRA_MODALIDADE.");
    private static readonly DomainError DestinoIndevido = new("ModalidadeSelecionada.RemanejamentoDestinoIndevido", "A modalidade só declara destino de remanejamento quando a regra é DESTINO_UNICO.");
    private static readonly DomainError CruzadoIndevido = new("ModalidadeSelecionada.RemanejamentoCruzadoIndevido", "A modalidade só declara par ou destino final de remanejamento quando a regra é CRUZADO.");
    private const string Item = "modalidades[0]";
    private const string CaminhoDaRegra = "modalidades[0].regraDeAplicabilidade[0][0].valor";

    [Fact]
    public void LerModalidade_AcaoInvalidaECoerencia_PropagaAmbas()
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["acaoQuandoIndeferido"] = "acao-invalida";
        modalidade["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        Conferir(modalidade, [(Item + ".acaoQuandoIndeferido", Acao), (Item, Remanejamento)]);
    }

    [Fact]
    public void LerModalidade_RegraInvalidaECoerencia_PropagaAmbas()
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["regraDeAplicabilidade"] = CondicoesNoEnvelope.Invalidas(false, true);
        modalidade["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        Conferir(modalidade, [(CaminhoDaRegra, Regra), (Item, Remanejamento)]);
    }

    [Fact]
    public void LerModalidade_CodigoEAcaoInvalidos_PropagaAmbos()
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["codigo"] = "codigo-invalido";
        modalidade["acaoQuandoIndeferido"] = "acao-invalida";
        Conferir(modalidade, [(Item + ".codigo", Codigo), (Item + ".acaoQuandoIndeferido", Acao)]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LerModalidade_DestinoInvalidoComOuSemCodigoInvalido_PropagaFormato(bool codigoInvalido)
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["naturezaLegal"] = "ACAO_AFIRMATIVA";
        modalidade["regraRemanejamento"] = "DESTINO_UNICO";
        modalidade["remanejamentoDestino"] = "destino-invalido";
        if (codigoInvalido) modalidade["codigo"] = "codigo-invalido";
        (string, DomainError)[] esperadas = codigoInvalido
            ? [(Item + ".codigo", Codigo), (Item + ".remanejamentoDestino", Codigo)]
            : [(Item + ".remanejamentoDestino", Codigo)];
        Conferir(modalidade, esperadas);
    }

    [Fact]
    public void LerModalidade_TodosOsCodigosForaDoFormato_PropagaCadaChaveNaOrdem()
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["codigo"] = "codigo-invalido";
        modalidade["composicaoOrigemCodigo"] = "codigo-invalido";
        modalidade["remanejamentoDestino"] = "codigo-invalido";
        modalidade["remanejamentoPar"] = "codigo-invalido";
        modalidade["remanejamentoDestinoFinal"] = "codigo-invalido";
        Conferir(modalidade, [(Item + ".codigo", Codigo), (Item + ".composicaoOrigemCodigo", Codigo),
            (Item + ".remanejamentoDestino", Codigo), (Item + ".remanejamentoPar", Codigo),
            (Item + ".remanejamentoDestinoFinal", Codigo), (Item, Origem),
            (Item, DestinoIndevido),
            (Item, CruzadoIndevido)]);
    }

    [Fact]
    public void LerModalidade_DestinoInvalidoECoerencia_PropagaAmbas()
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["regraRemanejamento"] = "DESTINO_UNICO";
        modalidade["remanejamentoDestino"] = "destino-invalido";
        Conferir(modalidade, [(Item + ".remanejamentoDestino", Codigo), (Item, Remanejamento)]);
    }

    [Fact]
    public void LerModalidade_RegraInvalidaEOrigemIndevida_PropagaAmbas()
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["regraDeAplicabilidade"] = CondicoesNoEnvelope.Invalidas(false, true);
        modalidade["composicaoOrigemCodigo"] = "OUTRA";
        Conferir(modalidade, [(CaminhoDaRegra, Regra), (Item, Origem)]);
    }

    [Fact]
    public void LerModalidade_CincoFamiliasDeRecusas_PropagaNaOrdem()
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade["codigo"] = "codigo-invalido";
        modalidade["acaoQuandoIndeferido"] = "acao-invalida";
        modalidade["regraDeAplicabilidade"] = CondicoesNoEnvelope.Invalidas(false, true);
        modalidade["composicaoOrigemCodigo"] = "OUTRA";
        modalidade["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        Conferir(modalidade, [(Item + ".codigo", Codigo), (Item + ".acaoQuandoIndeferido", Acao),
            (CaminhoDaRegra, Regra), (Item, Origem), (Item, Remanejamento)]);
    }

    [Theory]
    [InlineData("composicaoOrigemCodigo", "")]
    [InlineData("composicaoOrigemCodigo", " ")]
    [InlineData("remanejamentoDestino", "")]
    [InlineData("remanejamentoDestino", " ")]
    [InlineData("remanejamentoPar", "")]
    [InlineData("remanejamentoPar", " ")]
    [InlineData("remanejamentoDestinoFinal", "")]
    [InlineData("remanejamentoDestinoFinal", " ")]
    public void LerModalidade_CodigoOpcionalEmBranco_RecusaEstrutura(string chave, string valor)
    {
        JsonObject modalidade = CausasDaModalidadeNoEnvelopeTests.Modalidade();
        modalidade[chave] = valor;
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, Item);
        leitor.Erro.Code.Should().Be(Envelope);
        leitor.Erro.Message.Should().Be($"Envelope malformado em '{Item}.{chave}': esperado um texto não vazio.");
        leitor.Erro.Message.Should().NotContain(modalidade[chave]!.ToJsonString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TextoOpcionalNaoVazio_AusenteOuNulo_Aceita(bool ausente)
    {
        JsonObject objeto = new();
        if (!ausente) objeto["codigo"] = null;
        LeitorEnvelope leitor = new();
        leitor.TextoOpcionalNaoVazio(objeto, "codigo", Item).Should().BeNull();
        leitor.Falhou.Should().BeFalse();
    }

    private static void Conferir(JsonObject modalidade, (string Campo, DomainError Erro)[] esperadas)
    {
        LeitorEnvelope leitor = new();
        LeituraDasModalidades.LerModalidade(leitor, modalidade, Item);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Select(causa => (causa.Field!, causa.Error)).Should().Equal(esperadas);
        causas.Should().OnlyHaveUniqueItems();
        JsonObject documento = new() { ["modalidades"] = new JsonArray(modalidade) };
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
        foreach (string valor in new[] { "codigo-invalido", "acao-invalida", "destino-invalido", "VALOR_FICTICIO_X" })
            causas.Should().OnlyContain(causa => !causa.Error.Message.Contains(valor) && !causa.Field!.Contains(valor));
    }
}
