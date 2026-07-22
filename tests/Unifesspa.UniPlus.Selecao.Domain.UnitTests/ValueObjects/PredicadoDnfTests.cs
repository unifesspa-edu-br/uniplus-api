namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Cobre CA-01, CA-03 e CA-11 da Story #847 (ADR-0111) e a avaliação ternária da Story #916.</summary>
public sealed class PredicadoDnfTests
{
    private static CondicaoDnf Booleana(string fato, bool valor) =>
        CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;

    [Fact(DisplayName = "PredicadoDnf_SemClausulas_Avalia_Falso")]
    public void PredicadoDnf_SemClausulas_Avalia_Falso()
    {
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas([]);

        resultado.IsSuccess.Should().BeTrue("zero cláusulas é um estado estruturalmente válido");
        resultado.Value!.Clausulas.Should().BeEmpty();
        resultado.Value.Avaliar(new Dictionary<string, FatoResolvido>()).Should().Be(
            Ternario.Falso, "zero cláusulas é estado estrutural — nunca casa com ninguém, não se confunde com indeterminação");
    }

    [Fact(DisplayName = "PredicadoDnf_CriarDeCondicoesAgrupadas_Ignora_Ordinais_Ausentes")]
    public void PredicadoDnf_CriarDeCondicoesAgrupadas_Ignora_Ordinais_Ausentes()
    {
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (1, Booleana("PCD", true)),
            (3, Booleana("QUILOMBOLA", true)),
            (5, Booleana("EGRESSO_ESCOLA_PUBLICA", true)),
        ]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Clausulas.Should().HaveCount(3, "os ordinais 2 e 4 ausentes não geram cláusulas vazias");
    }

    [Fact(DisplayName = "PredicadoDnf_Avaliar_Combina_Clausulas_Ou_E_Condicoes_E")]
    public void PredicadoDnf_Avaliar_Combina_Clausulas_Ou_E_Condicoes_E()
    {
        // (PCD=true E QUILOMBOLA=true) OU (EGRESSO_ESCOLA_PUBLICA=true)
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (1, Booleana("PCD", true)),
            (1, Booleana("QUILOMBOLA", true)),
            (2, Booleana("EGRESSO_ESCOLA_PUBLICA", true)),
        ]);
        PredicadoDnf predicado = resultado.Value!;

        Dictionary<string, FatoResolvido> soPcd = new()
        {
            ["PCD"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)),
            ["QUILOMBOLA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)),
            ["EGRESSO_ESCOLA_PUBLICA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)),
        };
        predicado.Avaliar(soPcd).Should().Be(Ternario.Falso, "a primeira cláusula exige AMBAS as condições");

        Dictionary<string, FatoResolvido> soEgresso = new()
        {
            ["PCD"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)),
            ["QUILOMBOLA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)),
            ["EGRESSO_ESCOLA_PUBLICA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)),
        };
        predicado.Avaliar(soEgresso).Should().Be(Ternario.Verdadeiro, "a segunda cláusula sozinha já satisfaz o OU");
    }

    [Fact(DisplayName = "Story #916: fato não resolvível é Indeterminado (fail-closed) — nunca Falso, nunca lança")]
    public void PredicadoDnf_Avaliar_Fato_Nao_Resolvivel_E_Indeterminado()
    {
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas([(1, Booleana("SEXO_DESCONHECIDO", true))]);
        PredicadoDnf predicado = resultado.Value!;

        Action avaliar = () => predicado.Avaliar(new Dictionary<string, FatoResolvido>());

        avaliar.Should().NotThrow();
        predicado.Avaliar(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Indeterminado);
    }

    // ── Story #554 (PR #896, issue #892) — extensão dinâmica/multivalorada ──

    private static CondicaoDnf Categorica(string fato, Operador operador, JsonElement valor) =>
        CondicaoDnf.Criar(fato, operador, valor).Value!;

    private static JsonElement ArrayJson(params string[] valores) =>
        JsonSerializer.SerializeToElement(valores);

    private static JsonElement StringJson(string valor) =>
        JsonSerializer.SerializeToElement(valor);

    [Fact(DisplayName = "IGUAL em fato multivalorado é pertinência no conjunto do candidato")]
    public void Avaliar_IgualEmFatoMultivalorado_Pertinencia()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, Categorica("MODALIDADE", Operador.Igual, StringJson("LB_PPI")))]).Value!;

        Dictionary<string, FatoResolvido> fatos = new()
        {
            ["MODALIDADE"] = FatoResolvido.Resolvido(ArrayJson("LB_PPI", "AC")),
        };

        predicado.Avaliar(fatos).Should().Be(Ternario.Verdadeiro, "LB_PPI pertence ao conjunto [LB_PPI, AC] do candidato");
    }

    [Fact(DisplayName = "IGUAL em fato multivalorado sem o valor no conjunto é falso (contraprova)")]
    public void Avaliar_IgualEmFatoMultivalorado_ForaDoConjunto_Falso()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, Categorica("MODALIDADE", Operador.Igual, StringJson("LB_Q")))]).Value!;

        Dictionary<string, FatoResolvido> fatos = new()
        {
            ["MODALIDADE"] = FatoResolvido.Resolvido(ArrayJson("LB_PPI", "AC")),
        };

        predicado.Avaliar(fatos).Should().Be(Ternario.Falso);
    }

    [Fact(DisplayName = "EM em fato multivalorado é interseção — vazia resolve falso")]
    public void Avaliar_EmFatoMultivalorado_IntersecaoVazia_Falso()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, Categorica("MODALIDADE", Operador.Em, ArrayJson("LB_PPI", "LB_Q")))]).Value!;

        Dictionary<string, FatoResolvido> fatos = new()
        {
            ["MODALIDADE"] = FatoResolvido.Resolvido(ArrayJson("AC")),
        };

        predicado.Avaliar(fatos).Should().Be(Ternario.Falso, "[AC] intersecta [LB_PPI, LB_Q] em vazio");
    }

    [Fact(DisplayName = "EM em fato multivalorado é interseção — não vazia resolve verdadeiro")]
    public void Avaliar_EmFatoMultivalorado_IntersecaoNaoVazia_Verdadeiro()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, Categorica("MODALIDADE", Operador.Em, ArrayJson("LB_PPI", "LB_Q")))]).Value!;

        Dictionary<string, FatoResolvido> fatos = new()
        {
            ["MODALIDADE"] = FatoResolvido.Resolvido(ArrayJson("AC", "LB_PPI")),
        };

        predicado.Avaliar(fatos).Should().Be(Ternario.Verdadeiro, "[AC, LB_PPI] intersecta [LB_PPI, LB_Q] em [LB_PPI], não vazio");
    }

    [Fact(DisplayName = "Avaliação escalar não regride com a extensão multivalorada")]
    public void Avaliar_FatoEscalar_ComportamentoPreservado()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, Categorica("SEXO", Operador.Igual, StringJson("MASCULINO")))]).Value!;

        Dictionary<string, FatoResolvido> fatos = new() { ["SEXO"] = FatoResolvido.Resolvido(StringJson("MASCULINO")) };

        predicado.Avaliar(fatos).Should().Be(Ternario.Verdadeiro, "fato escalar (não-array) segue o ramo original, sem mudança de comportamento");
    }

    // ── Story #916 — operadores de exclusão (DIFERENTE/NAO_EM) e tabela-verdade ternária ──

    private static PredicadoDnf Predicado(params (int Clausula, CondicaoDnf Condicao)[] linhas) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(linhas).Value!;

    private static Dictionary<string, FatoResolvido> Fatos(string chave, JsonElement valor) =>
        new(StringComparer.Ordinal) { [chave] = FatoResolvido.Resolvido(valor) };

    [Theory(DisplayName = "DIFERENTE escalar: nega IGUAL quando o fato está resolvido")]
    [InlineData("FEMININO", Ternario.Verdadeiro)]
    [InlineData("MASCULINO", Ternario.Falso)]
    public void Diferente_Escalar_NegaIgualQuandoResolvido(string valorCandidato, Ternario esperado)
    {
        PredicadoDnf predicado = Predicado((1, Categorica("SEXO", Operador.Diferente, StringJson("MASCULINO"))));

        predicado.Avaliar(Fatos("SEXO", StringJson(valorCandidato))).Should().Be(esperado);
    }

    [Fact(DisplayName = "DIFERENTE nunca inverte ausência para Verdadeiro — fato ausente é Indeterminado")]
    public void Diferente_FatoAusente_Indeterminado()
    {
        PredicadoDnf predicado = Predicado((1, Categorica("SEXO", Operador.Diferente, StringJson("MASCULINO"))));

        predicado.Avaliar(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Indeterminado);
    }

    [Theory(DisplayName = "NAO_EM categórico: nega EM quando o fato está resolvido")]
    [InlineData("PARDA", Ternario.Verdadeiro)]
    [InlineData("PRETA", Ternario.Falso)]
    public void NaoEm_Categorico_NegaEmQuandoResolvido(string valorCandidato, Ternario esperado)
    {
        PredicadoDnf predicado = Predicado(
            (1, Categorica("COR_RACA", Operador.NaoEm, ArrayJson("PRETA"))));

        predicado.Avaliar(Fatos("COR_RACA", StringJson(valorCandidato))).Should().Be(esperado);
    }

    [Fact(DisplayName = "NAO_EM nunca inverte ausência para Verdadeiro — fato ausente é Indeterminado")]
    public void NaoEm_FatoAusente_Indeterminado()
    {
        PredicadoDnf predicado = Predicado((1, Categorica("COR_RACA", Operador.NaoEm, ArrayJson("PRETA"))));

        predicado.Avaliar(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "EM [] com fato resolvido é Falso (interseção vazia)")]
    public void Em_ListaVazia_ComFatoResolvido_Falso()
    {
        PredicadoDnf predicado = Predicado((1, Categorica("COR_RACA", Operador.Em, ArrayJson())));

        predicado.Avaliar(Fatos("COR_RACA", StringJson("PARDA"))).Should().Be(Ternario.Falso);
    }

    [Fact(DisplayName = "NAO_EM [] com fato resolvido é Verdadeiro (não pertence ao vazio)")]
    public void NaoEm_ListaVazia_ComFatoResolvido_Verdadeiro()
    {
        PredicadoDnf predicado = Predicado((1, Categorica("COR_RACA", Operador.NaoEm, ArrayJson())));

        predicado.Avaliar(Fatos("COR_RACA", StringJson("PARDA"))).Should().Be(Ternario.Verdadeiro);
    }

    [Fact(DisplayName = "EM []/NAO_EM [] com fato ausente continuam Indeterminado — a ausência tem precedência sobre a semântica de lista vazia")]
    public void EmOuNaoEmListaVazia_ComFatoAusente_Indeterminado()
    {
        PredicadoDnf emVazio = Predicado((1, Categorica("COR_RACA", Operador.Em, ArrayJson())));
        PredicadoDnf naoEmVazio = Predicado((1, Categorica("COR_RACA", Operador.NaoEm, ArrayJson())));

        emVazio.Avaliar(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Indeterminado);
        naoEmVazio.Avaliar(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "Fato presente com estado Indeterminado é Indeterminado, não Falso")]
    public void Avaliar_FatoIndeterminado_Indeterminado()
    {
        PredicadoDnf predicado = Predicado((1, Booleana("PCD", true)));

        Dictionary<string, FatoResolvido> fatos = new(StringComparer.Ordinal) { ["PCD"] = FatoResolvido.Indeterminado() };

        predicado.Avaliar(fatos).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "Um fato sem valor não pode ser construído como resolvido — o estado é declarado, nunca inferido do nulo")]
    public void FatoResolvido_ResolvidoComNulo_Recusado()
    {
        using JsonDocument nulo = JsonDocument.Parse("null");

        Action comNulo = () => FatoResolvido.Resolvido(nulo.RootElement.Clone());
        Action comIndefinido = () => FatoResolvido.Resolvido(default);

        comNulo.Should().Throw<ArgumentException>(
            "aceitar null como resolvido reintroduziria a ambiguidade entre não-aplicável e indeterminado");
        comIndefinido.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Comparação numérica sobre valor de tipo incoerente é Indeterminado, nunca lança")]
    public void Avaliar_ComparacaoNumericaComValorIncoerente_IndeterminadoSemLancar()
    {
        PredicadoDnf predicado = Predicado(
            (1, Categorica("FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18))));

        Action avaliar = () => predicado.Avaliar(Fatos("FAIXA_ETARIA", StringJson("NAO_NUMERICO")));

        avaliar.Should().NotThrow();
        predicado.Avaliar(Fatos("FAIXA_ETARIA", StringJson("NAO_NUMERICO"))).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "IGUAL escalar com tipo de fato incoerente com o valor configurado é Indeterminado, não Falso")]
    public void Avaliar_IgualComTipoIncoerente_Indeterminado()
    {
        PredicadoDnf predicado = Predicado((1, Booleana("PCD", true)));

        predicado.Avaliar(Fatos("PCD", StringJson("SIM"))).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "Propagação E: Falso vence sobre Indeterminado na mesma cláusula")]
    public void ClausulaAvaliar_FalsoVenceSobreIndeterminado()
    {
        PredicadoDnf predicado = Predicado(
            (1, Booleana("PCD", true)),
            (1, Booleana("FATO_AUSENTE", true)));

        // PCD=false (Falso) E FATO_AUSENTE (Indeterminado) -> Falso vence.
        predicado.Avaliar(Fatos("PCD", JsonSerializer.SerializeToElement(false))).Should().Be(Ternario.Falso);
    }

    [Fact(DisplayName = "Propagação E: Indeterminado vence quando nenhuma condição é Falso")]
    public void ClausulaAvaliar_IndeterminadoVenceSemFalso()
    {
        PredicadoDnf predicado = Predicado(
            (1, Booleana("PCD", true)),
            (1, Booleana("FATO_AUSENTE", true)));

        Dictionary<string, FatoResolvido> fatos = new() { ["PCD"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)) };

        // PCD=true (Verdadeiro) E FATO_AUSENTE (Indeterminado) -> Indeterminado.
        predicado.Avaliar(fatos).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "Propagação OU: Verdadeiro vence sobre qualquer outra cláusula")]
    public void PredicadoAvaliar_VerdadeiroVenceSobreIndeterminadoEFalso()
    {
        PredicadoDnf predicado = Predicado(
            (1, Booleana("EGRESSO_ESCOLA_PUBLICA", true)),
            (2, Booleana("FATO_AUSENTE", true)),
            (3, Booleana("QUILOMBOLA", true)));

        Dictionary<string, FatoResolvido> fatos = new()
        {
            ["EGRESSO_ESCOLA_PUBLICA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)),
            ["QUILOMBOLA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)),
        };

        predicado.Avaliar(fatos).Should().Be(Ternario.Verdadeiro);
    }

    [Fact(DisplayName = "Propagação OU: Indeterminado vence sobre Falso quando nenhuma cláusula é Verdadeira")]
    public void PredicadoAvaliar_IndeterminadoVenceSobreFalsoSemVerdadeiro()
    {
        PredicadoDnf predicado = Predicado(
            (1, Booleana("QUILOMBOLA", true)),
            (2, Booleana("FATO_AUSENTE", true)));

        Dictionary<string, FatoResolvido> fatos = new() { ["QUILOMBOLA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)) };

        predicado.Avaliar(fatos).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "Propagação OU: Falso só quando TODAS as cláusulas são Falso (nenhuma indeterminada, nenhuma verdadeira)")]
    public void PredicadoAvaliar_FalsoQuandoTodasAsClausulasSaoFalso()
    {
        PredicadoDnf predicado = Predicado(
            (1, Booleana("QUILOMBOLA", true)),
            (2, Booleana("PCD", true)));

        Dictionary<string, FatoResolvido> fatos = new()
        {
            ["QUILOMBOLA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)),
            ["PCD"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false)),
        };

        predicado.Avaliar(fatos).Should().Be(Ternario.Falso);
    }

    // ── Story #926 — NÃO_APLICÁVEL é estado próprio do átomo, distinto de INDETERMINADO ──

    private static Dictionary<string, FatoResolvido> Estados(params (string Fato, FatoResolvido Estado)[] entradas) =>
        entradas.ToDictionary(static e => e.Fato, static e => e.Estado, StringComparer.Ordinal);

    [Theory(DisplayName = "Átomo sobre fato NÃO_APLICÁVEL é não-aplicável para todo operador — a negação não inverte a inaplicabilidade")]
    [InlineData(Operador.Igual)]
    [InlineData(Operador.Diferente)]
    [InlineData(Operador.Em)]
    [InlineData(Operador.NaoEm)]
    [InlineData(Operador.MaiorIgual)]
    [InlineData(Operador.MenorIgual)]
    public void Atomo_SobreFatoNaoAplicavel_NuncaEhVerdadeiro(Operador operador)
    {
        JsonElement valor = operador switch
        {
            Operador.Em or Operador.NaoEm => ArrayJson("SIM"),
            Operador.MaiorIgual or Operador.MenorIgual => JsonSerializer.SerializeToElement(18),
            _ => StringJson("SIM"),
        };
        PredicadoDnf predicado = Predicado((1, Categorica("CONCORRER_PCD", operador, valor)));

        Ternario resultado = predicado.Avaliar(Estados(("CONCORRER_PCD", FatoResolvido.NaoAplicavel())));

        resultado.Should().Be(
            Ternario.Falso,
            "o candidato não é PcD, então o opt-in não se aplica — nem satisfaz o gatilho, nem fica pendente esperando resposta");
    }

    [Fact(DisplayName = "Um fato resolvido sobrevive ao descarte do documento que o originou")]
    public void FatoResolvido_SobreviveAoDescarteDoDocumentoDeOrigem()
    {
        FatoResolvido fato;
        using (JsonDocument documento = JsonDocument.Parse("\"MASCULINO\""))
        {
            fato = FatoResolvido.Resolvido(documento.RootElement);
        }

        PredicadoDnf predicado = Predicado((1, Categorica("SEXO", Operador.Igual, StringJson("MASCULINO"))));

        Action avaliar = () => predicado.Avaliar(Estados(("SEXO", fato)));

        avaliar.Should().NotThrow<ObjectDisposedException>(
            "o valor é clonado na construção — sem isso, o fato ficaria preso ao tempo de vida do documento que o produziu");
        predicado.Avaliar(Estados(("SEXO", fato))).Should().Be(Ternario.Verdadeiro);
    }

    [Fact(DisplayName = "Cláusula E com VERDADEIRO e NÃO_APLICÁVEL resolve Falso — o não-aplicável colapsa, não é ignorado")]
    public void Clausula_VerdadeiroComNaoAplicavel_Falso()
    {
        PredicadoDnf predicado = Predicado(
            (1, Booleana("EGRESSO_ESCOLA_PUBLICA", true)),
            (1, Categorica("CONCORRER_Q", Operador.Igual, StringJson("SIM"))));

        Ternario resultado = predicado.Avaliar(Estados(
            ("EGRESSO_ESCOLA_PUBLICA", FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true))),
            ("CONCORRER_Q", FatoResolvido.NaoAplicavel())));

        resultado.Should().Be(
            Ternario.Falso,
            "sem o colapso o átomo não-aplicável seria ignorado e a cláusula resolveria verdadeiro por um opt-in que nem foi perguntado");
    }

    [Fact(DisplayName = "Cláusula E com NÃO_APLICÁVEL e INDETERMINADO resolve Falso — o não-aplicável é definitivo e vence a pendência")]
    public void Clausula_NaoAplicavelComIndeterminado_Falso()
    {
        PredicadoDnf predicado = Predicado(
            (1, Categorica("CONCORRER_Q", Operador.Igual, StringJson("SIM"))),
            (1, Booleana("PCD", true)));

        Ternario resultado = predicado.Avaliar(Estados(
            ("CONCORRER_Q", FatoResolvido.NaoAplicavel()),
            ("PCD", FatoResolvido.Indeterminado())));

        resultado.Should().Be(
            Ternario.Falso,
            "a cláusula já é impossível pelo átomo não-aplicável — esperar a resposta pendente do outro fato não mudaria o resultado");
    }

    [Fact(DisplayName = "DNF misto: cláusula não-aplicável e cláusula pendente resolvem Indeterminado — os dois estados não colapsam num só")]
    public void Dnf_ClausulaNaoAplicavelComClausulaIndeterminada_Indeterminado()
    {
        PredicadoDnf predicado = Predicado(
            (1, Categorica("CONCORRER_Q", Operador.Igual, StringJson("SIM"))),
            (2, Booleana("PCD", true)));

        Ternario resultado = predicado.Avaliar(Estados(
            ("CONCORRER_Q", FatoResolvido.NaoAplicavel()),
            ("PCD", FatoResolvido.Indeterminado())));

        resultado.Should().Be(
            Ternario.Indeterminado,
            "a primeira cláusula está resolvida em falso, mas a segunda ainda pode virar verdadeira quando o candidato responder");
    }

    [Fact(DisplayName = "O estado vem do discriminador, não da ausência de valor: dois fatos sem valor levam a resultados opostos")]
    public void Estado_VemDoDiscriminador_NaoDaAusenciaDeValor()
    {
        PredicadoDnf predicado = Predicado((1, Categorica("CONCORRER_PCD", Operador.Igual, StringJson("SIM"))));

        Ternario naoAplicavel = predicado.Avaliar(Estados(("CONCORRER_PCD", FatoResolvido.NaoAplicavel())));
        Ternario indeterminado = predicado.Avaliar(Estados(("CONCORRER_PCD", FatoResolvido.Indeterminado())));

        naoAplicavel.Should().Be(Ternario.Falso, "não se aplica — a exigência que depende disso fica dispensada em definitivo");
        indeterminado.Should().Be(Ternario.Indeterminado, "aplica-se e ainda não foi respondido — a exigência segue pendente");
        naoAplicavel.Should().NotBe(indeterminado, "os dois carregam valor nulo e mesmo assim decidem coisas opostas");
    }
}
