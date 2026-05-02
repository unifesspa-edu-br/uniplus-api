namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Logging;

using AwesomeAssertions;

using NSubstitute;

using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

using Unifesspa.UniPlus.Infrastructure.Core.Logging;

public sealed class PiiMaskingEnricherTests
{
    private readonly PiiMaskingEnricher _enricher = new();
    private readonly ILogEventPropertyFactory _propertyFactory = Substitute.For<ILogEventPropertyFactory>();

    // ─── CA-01 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Enrich_DadoCpfFormatadoEmScalarValue_QuandoEnricherProcessar_EntaoDeveMascararComHifen()
    {
        LogEvent evento = CriarEventoComPropriedade("CpfCandidato", "123.456.789-01");

        _enricher.Enrich(evento, _propertyFactory);

        ValorDaPropriedade(evento, "CpfCandidato").Should().Be("***.***.***-01");
    }

    // ─── CA-02 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Enrich_DadoCpfSomenteDigitos_QuandoEnricherProcessar_EntaoDeveAplicarFormatoSerpro()
    {
        LogEvent evento = CriarEventoComPropriedade("CpfCandidato", "12345678901");

        _enricher.Enrich(evento, _propertyFactory);

        ValorDaPropriedade(evento, "CpfCandidato").Should().Be("***.***.***-01");
    }

    // ─── CA-03 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Enrich_DadoPropriedadeSemCpf_QuandoEnricherProcessar_EntaoDevePreservarReferenciaOriginal()
    {
        const string textoOriginal = "processo seletivo iniciado com sucesso";
        LogEvent evento = CriarEventoComPropriedade("Mensagem", textoOriginal);
        LogEventPropertyValue valorAntes = evento.Properties["Mensagem"];

        _enricher.Enrich(evento, _propertyFactory);

        LogEventPropertyValue valorDepois = evento.Properties["Mensagem"];
        valorDepois.Should().BeSameAs(valorAntes, "sem CPF, o enricher não deve realocar o ScalarValue");
    }

    // ─── CA-04 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Enrich_DadoMultiplasOcorrenciasDeCpfNaMesmaString_QuandoEnricherProcessar_EntaoDeveMascararTodas()
    {
        const string mensagem = "candidatos 123.456.789-01 e 987.654.321-02 homologados";
        LogEvent evento = CriarEventoComPropriedade("Mensagem", mensagem);

        _enricher.Enrich(evento, _propertyFactory);

        ValorDaPropriedade(evento, "Mensagem")
            .Should().Be("candidatos ***.***.***-01 e ***.***.***-02 homologados");
    }

    // ─── CA-05 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Enrich_DadoCpfEmStructureValueAninhado_QuandoEnricherProcessar_EntaoDeveMascararRecursivamente()
    {
        LogEventProperty[] camposCandidato =
        [
            new("Nome", new ScalarValue("João da Silva")),
            new("Cpf", new ScalarValue("123.456.789-01")),
        ];
        StructureValue candidato = new(camposCandidato, typeTag: "Candidato");
        LogEvent evento = CriarEventoComPropriedade("Candidato", candidato);

        _enricher.Enrich(evento, _propertyFactory);

        StructureValue candidatoMascarado = (StructureValue)evento.Properties["Candidato"];
        ValorDoCampo(candidatoMascarado, "Cpf").Should().Be("***.***.***-01");
        ValorDoCampo(candidatoMascarado, "Nome").Should().Be("João da Silva");
        candidatoMascarado.TypeTag.Should().Be("Candidato", "TypeTag deve ser preservado ao reconstruir a estrutura");
    }

    [Fact]
    public void Enrich_DadoCpfEmSequenceValue_QuandoEnricherProcessar_EntaoDeveMascararTodosOsElementos()
    {
        SequenceValue sequencia = new(
        [
            new ScalarValue("123.456.789-01"),
            new ScalarValue("987.654.321-02"),
        ]);
        LogEvent evento = CriarEventoComPropriedade("CpfsHomologados", sequencia);

        _enricher.Enrich(evento, _propertyFactory);

        SequenceValue sequenciaMascarada = (SequenceValue)evento.Properties["CpfsHomologados"];
        ValorDoElemento(sequenciaMascarada, 0).Should().Be("***.***.***-01");
        ValorDoElemento(sequenciaMascarada, 1).Should().Be("***.***.***-02");
    }

    [Fact]
    public void Enrich_DadoStructureDentroDeSequence_QuandoEnricherProcessar_EntaoDeveMascararRecursivamenteEmProfundidade()
    {
        StructureValue primeiro = new(
        [
            new LogEventProperty("Cpf", new ScalarValue("123.456.789-01")),
        ]);
        StructureValue segundo = new(
        [
            new LogEventProperty("Cpf", new ScalarValue("987.654.321-02")),
        ]);
        SequenceValue lote = new([primeiro, segundo]);
        LogEvent evento = CriarEventoComPropriedade("Lote", lote);

        _enricher.Enrich(evento, _propertyFactory);

        SequenceValue loteMascarado = (SequenceValue)evento.Properties["Lote"];
        ValorDoCampo((StructureValue)loteMascarado.Elements[0], "Cpf").Should().Be("***.***.***-01");
        ValorDoCampo((StructureValue)loteMascarado.Elements[1], "Cpf").Should().Be("***.***.***-02");
    }

    [Fact]
    public void Enrich_DadoCpfComoValorEmDictionaryValue_QuandoEnricherProcessar_EntaoDeveMascararOValor()
    {
        DictionaryValue dicionario = new(
        [
            new(new ScalarValue("candidato-a"), new ScalarValue("123.456.789-01")),
            new(new ScalarValue("candidato-b"), new ScalarValue("987.654.321-02")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("CpfsPorChave", dicionario);

        _enricher.Enrich(evento, _propertyFactory);

        DictionaryValue dicionarioMascarado = (DictionaryValue)evento.Properties["CpfsPorChave"];
        dicionarioMascarado.Elements
            .Select(e => ((ScalarValue)e.Value).Value)
            .Should().BeEquivalentTo(["***.***.***-01", "***.***.***-02"]);
    }

    // ─── CA-03 complementar ────────────────────────────────────────────────

    [Fact]
    public void Enrich_DadoStructureValueSemCpf_QuandoEnricherProcessar_EntaoDevePreservarReferenciaDoStructure()
    {
        StructureValue estrutura = new(
        [
            new LogEventProperty("Nome", new ScalarValue("Maria Souza")),
            new LogEventProperty("Curso", new ScalarValue("Engenharia")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Candidato", estrutura);
        LogEventPropertyValue valorAntes = evento.Properties["Candidato"];

        _enricher.Enrich(evento, _propertyFactory);

        evento.Properties["Candidato"].Should().BeSameAs(valorAntes);
    }

    // ─── Cobertura dos ramos mistos (preservação parcial) ──────────────────

    [Fact]
    public void Enrich_DadoStructureComCpfSeguidoDeCampoSemCpf_QuandoEnricherProcessar_EntaoDevePreservarCampoInalteradoNaCopia()
    {
        StructureValue estrutura = new(
        [
            new LogEventProperty("Cpf", new ScalarValue("123.456.789-01")),
            new LogEventProperty("Curso", new ScalarValue("Engenharia")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Candidato", estrutura);

        _enricher.Enrich(evento, _propertyFactory);

        StructureValue mascarada = (StructureValue)evento.Properties["Candidato"];
        ValorDoCampo(mascarada, "Cpf").Should().Be("***.***.***-01");
        ValorDoCampo(mascarada, "Curso").Should().Be("Engenharia");
    }

    [Fact]
    public void Enrich_DadoSequenceComElementoSemCpfAntesDeCpf_QuandoEnricherProcessar_EntaoDeveCopiarElementosPreservadosNoBackfill()
    {
        SequenceValue sequencia = new(
        [
            new ScalarValue("sem cpf aqui"),
            new ScalarValue("123.456.789-01"),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Itens", sequencia);

        _enricher.Enrich(evento, _propertyFactory);

        SequenceValue mascarada = (SequenceValue)evento.Properties["Itens"];
        ValorDoElemento(mascarada, 0).Should().Be("sem cpf aqui");
        ValorDoElemento(mascarada, 1).Should().Be("***.***.***-01");
    }

    [Fact]
    public void Enrich_DadoSequenceComCpfSeguidoDeElementoSemCpf_QuandoEnricherProcessar_EntaoDevePreservarElementoInalterado()
    {
        SequenceValue sequencia = new(
        [
            new ScalarValue("123.456.789-01"),
            new ScalarValue("sem cpf aqui"),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Itens", sequencia);

        _enricher.Enrich(evento, _propertyFactory);

        SequenceValue mascarada = (SequenceValue)evento.Properties["Itens"];
        ValorDoElemento(mascarada, 0).Should().Be("***.***.***-01");
        ValorDoElemento(mascarada, 1).Should().Be("sem cpf aqui");
    }

    [Fact]
    public void Enrich_DadoDictionaryComValorSemCpfAntesDeCpf_QuandoEnricherProcessar_EntaoDeveCopiarEntradasPreservadasNoBackfill()
    {
        DictionaryValue dicionario = new(
        [
            new(new ScalarValue("curso"), new ScalarValue("Engenharia")),
            new(new ScalarValue("cpf"), new ScalarValue("123.456.789-01")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Dados", dicionario);

        _enricher.Enrich(evento, _propertyFactory);

        DictionaryValue mascarada = (DictionaryValue)evento.Properties["Dados"];
        List<KeyValuePair<ScalarValue, LogEventPropertyValue>> entradas = [.. mascarada.Elements];
        ((ScalarValue)entradas[0].Value).Value.Should().Be("Engenharia");
        ((ScalarValue)entradas[1].Value).Value.Should().Be("***.***.***-01");
    }

    [Fact]
    public void Enrich_DadoDictionaryComCpfSeguidoDeValorSemCpf_QuandoEnricherProcessar_EntaoDevePreservarEntradaInalterada()
    {
        DictionaryValue dicionario = new(
        [
            new(new ScalarValue("cpf"), new ScalarValue("123.456.789-01")),
            new(new ScalarValue("curso"), new ScalarValue("Engenharia")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Dados", dicionario);

        _enricher.Enrich(evento, _propertyFactory);

        DictionaryValue mascarada = (DictionaryValue)evento.Properties["Dados"];
        List<KeyValuePair<ScalarValue, LogEventPropertyValue>> entradas = [.. mascarada.Elements];
        ((ScalarValue)entradas[0].Value).Value.Should().Be("***.***.***-01");
        ((ScalarValue)entradas[1].Value).Value.Should().Be("Engenharia");
    }

    // ─── Word boundary — anti-falso-positivo e anti-falso-negativo ─────────

    [Fact]
    public void Enrich_DadoTimestampComQuatorzeDigitos_QuandoEnricherProcessar_EntaoNaoDeveCorromperLog()
    {
        LogEvent evento = CriarEventoComPropriedade("Timestamp", "20240523120456");

        _enricher.Enrich(evento, _propertyFactory);

        ValorDaPropriedade(evento, "Timestamp").Should().Be("20240523120456");
    }

    [Fact]
    public void Enrich_DadoIdNumericoDeDozeDigitos_QuandoEnricherProcessar_EntaoNaoDeveMascarar()
    {
        LogEvent evento = CriarEventoComPropriedade("IdExterno", "112345678901");

        _enricher.Enrich(evento, _propertyFactory);

        ValorDaPropriedade(evento, "IdExterno").Should().Be("112345678901");
    }

    [Fact]
    public void Enrich_DadoCpfAdjacenteALetras_QuandoEnricherProcessar_EntaoDeveMascarar()
    {
        LogEvent evento = CriarEventoComPropriedade("Mensagem", "candidato cpf12345678901 homologado");

        _enricher.Enrich(evento, _propertyFactory);

        ValorDaPropriedade(evento, "Mensagem").Should().Be("candidato cpf***.***.***-01 homologado");
    }

    [Fact]
    public void Enrich_DadoTimestampConcatenadoComCpf_QuandoEnricherProcessar_EntaoNaoDeveMascararParaEvitarCorrupcaoDeLog()
    {
        LogEvent evento = CriarEventoComPropriedade("Concatenado", "202405231212345678901");

        _enricher.Enrich(evento, _propertyFactory);

        ValorDaPropriedade(evento, "Concatenado").Should().Be("202405231212345678901",
            "21 dígitos consecutivos são ambíguos; preferimos preservar o log a mascarar dígitos errados");
    }

    // ─── DictionaryValue com CPF em chave ─────────────────────────────────

    [Fact]
    public void Enrich_DadoDictionaryComCpfNaChave_QuandoEnricherProcessar_EntaoDeveMascararAChave()
    {
        DictionaryValue dicionario = new(
        [
            new(new ScalarValue("123.456.789-01"), new ScalarValue("homologado")),
            new(new ScalarValue("987.654.321-02"), new ScalarValue("pendente")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("StatusLote", dicionario);

        _enricher.Enrich(evento, _propertyFactory);

        DictionaryValue mascarada = (DictionaryValue)evento.Properties["StatusLote"];
        mascarada.Elements.Select(e => ((ScalarValue)e.Key).Value)
            .Should().BeEquivalentTo(new object[] { "***.***.***-01", "***.***.***-02" });
        mascarada.Elements.Select(e => ((ScalarValue)e.Value).Value)
            .Should().BeEquivalentTo(new object[] { "homologado", "pendente" });
    }

    [Fact]
    public void Enrich_DadoDictionaryComCpfEmChaveEValor_QuandoEnricherProcessar_EntaoDeveMascararAmbos()
    {
        DictionaryValue dicionario = new(
        [
            new(new ScalarValue("123.456.789-01"), new ScalarValue("parceiro 987.654.321-02")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Relacionamento", dicionario);

        _enricher.Enrich(evento, _propertyFactory);

        DictionaryValue mascarada = (DictionaryValue)evento.Properties["Relacionamento"];
        KeyValuePair<ScalarValue, LogEventPropertyValue> unica = mascarada.Elements.Single();
        unica.Key.Value.Should().Be("***.***.***-01");
        ((ScalarValue)unica.Value).Value.Should().Be("parceiro ***.***.***-02");
    }

    [Fact]
    public void Enrich_DadoDictionaryComChaveNaoString_QuandoEnricherProcessar_EntaoDevePreservarChaveEReferenciaDoValorQuandoIntacto()
    {
        DictionaryValue dicionario = new(
        [
            new(new ScalarValue(1), new ScalarValue("primeiro")),
            new(new ScalarValue(2), new ScalarValue("segundo")),
        ]);
        LogEvent evento = CriarEventoComPropriedade("Ordenados", dicionario);
        LogEventPropertyValue antes = evento.Properties["Ordenados"];

        _enricher.Enrich(evento, _propertyFactory);

        evento.Properties["Ordenados"].Should().BeSameAs(antes);
    }

    [Fact]
    public void Enrich_DadoDictionaryComSomenteChaveCpfAlterada_QuandoEnricherProcessar_EntaoDeveReconstituirDicionarioComValorOriginal()
    {
        ScalarValue valorOriginal = new("homologado");
        DictionaryValue dicionario = new(
        [
            new(new ScalarValue("123.456.789-01"), valorOriginal),
        ]);
        LogEvent evento = CriarEventoComPropriedade("StatusPorCpf", dicionario);

        _enricher.Enrich(evento, _propertyFactory);

        DictionaryValue mascarada = (DictionaryValue)evento.Properties["StatusPorCpf"];
        KeyValuePair<ScalarValue, LogEventPropertyValue> unica = mascarada.Elements.Single();
        unica.Key.Value.Should().Be("***.***.***-01");
        unica.Value.Should().BeSameAs(valorOriginal, "valor sem CPF deve preservar referência original");
    }

    // ─── Cenários de borda ─────────────────────────────────────────────────

    [Fact]
    public void Enrich_DadoScalarValueNaoString_QuandoEnricherProcessar_EntaoDevePreservarValor()
    {
        LogEvent evento = CriarEventoComPropriedade("Idade", new ScalarValue(42));

        _enricher.Enrich(evento, _propertyFactory);

        ((ScalarValue)evento.Properties["Idade"]).Value.Should().Be(42);
    }

    [Fact]
    public void Enrich_DadoScalarValueNulo_QuandoEnricherProcessar_EntaoDevePreservarSemErro()
    {
        LogEvent evento = CriarEventoComPropriedade("CpfCandidato", new ScalarValue(null));

        Action acao = () => _enricher.Enrich(evento, _propertyFactory);

        acao.Should().NotThrow();
        ((ScalarValue)evento.Properties["CpfCandidato"]).Value.Should().BeNull();
    }

    [Theory]
    [InlineData("123.456.789-01", "***.***.***-01")]
    [InlineData("12345678901", "***.***.***-01")]
    [InlineData("123.45678901", "***.***.***-01")]
    [InlineData("123456789-01", "***.***.***-01")]
    [InlineData("000.000.000-00", "***.***.***-00")]
    [InlineData("999.999.999-99", "***.***.***-99")]
    public void MascararCpf_DadoVariacoesDeFormatacao_DeveRetornarMascaraNoPadraoSerpro(string entrada, string esperado)
    {
        PiiMaskingEnricher.MascararCpf(entrada).Should().Be(esperado);
    }

    [Fact]
    public void MascararCpf_DadoStringVazia_DeveRetornarMesmaReferencia()
    {
        string vazio = string.Empty;

        PiiMaskingEnricher.MascararCpf(vazio).Should().BeSameAs(vazio);
    }

    [Fact]
    public void MascararCpf_DadoTextoSemCpf_DeveRetornarMesmaReferencia()
    {
        const string texto = "processo de homologação iniciado";

        PiiMaskingEnricher.MascararCpf(texto).Should().BeSameAs(texto);
    }

    [Fact]
    public void Enrich_DadoLogEventNulo_EntaoDeveLancarArgumentNullException()
    {
        Action acao = () => _enricher.Enrich(null!, _propertyFactory);

        acao.Should().Throw<ArgumentNullException>();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static LogEvent CriarEventoComPropriedade(string nome, string valor)
        => CriarEventoComPropriedade(nome, new ScalarValue(valor));

    private static LogEvent CriarEventoComPropriedade(string nome, LogEventPropertyValue valor)
    {
        MessageTemplate template = new MessageTemplateParser().Parse("template");
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            template,
            [new LogEventProperty(nome, valor)]);
    }

    private static object? ValorDaPropriedade(LogEvent evento, string nome)
        => ((ScalarValue)evento.Properties[nome]).Value;

    private static object? ValorDoCampo(StructureValue estrutura, string nomeCampo)
        => ((ScalarValue)estrutura.Properties.First(p => p.Name == nomeCampo).Value).Value;

    private static object? ValorDoElemento(SequenceValue sequencia, int indice)
        => ((ScalarValue)sequencia.Elements[indice]).Value;
}
