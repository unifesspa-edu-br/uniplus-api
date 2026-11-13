namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// As invariantes da regra de recurso e da convenção de contagem de prazo, que a reidratação confere por
/// conta própria: ela não é porta de fundo para o que a escrita recusa.
/// </summary>
public sealed class EnvelopeCodecRecusaRegraDeRecursoTests
{
    // ── Invariantes da regra de recurso: a reidratação não é porta de fundo ──

    /// <summary>
    /// O prazo de interposição em unidade não declarável entra pela reidratação, não pelo
    /// HTTP: quem chega pela API é barrado pelo validator do comando, mas o codec chama
    /// <c>RegraRecursoFase.Criar</c> direto. Sem a invariante na entidade, um envelope com
    /// <c>Nenhuma</c> — que é o valor zero do enum, e portanto o default de qualquer args
    /// construído sem preencher a unidade — reidrataria num agregado cujo prazo não conta
    /// em unidade alguma.
    /// </summary>
    [Fact(DisplayName = "Prazo de interposição sem unidade declarável no envelope: recusa nomeada da entidade, não agregado em estado proibido")]
    public void RegraRecursoComPrazoSemUnidadeDeclaravel_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDaRegraDeRecurso(envelope)["prazoUnidade"] = nameof(UnidadePrazo.Nenhuma));

        resultado.IsFailure.Should().BeTrue(
            "a unidade do prazo é o que decide quando a janela do candidato fecha; sem ela o prazo não conta");
        resultado.Error!.Code.Should().Be("RegraRecursoFase.PrazoSemUnidadeDeclaravel");
    }

    [Fact(DisplayName = "Prazo de interposição não positivo no envelope: recusa nomeada, e não um prazo que fecha ao abrir")]
    public void RegraRecursoComPrazoNaoPositivo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            // O decimal trafega como texto canônico de 4 casas, como o resto do envelope —
            // um número JSON aqui seria recusado pela gramática antes de a entidade opinar,
            // e o teste passaria pelo motivo errado.
            ArgsDaRegraDeRecurso(envelope)["prazoValor"] = "0.0000");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.PrazoNaoPositivo");
    }

    [Fact(DisplayName = "Par de suspensividade pela metade no envelope: recusa nomeada, e não uma janela sem relógio")]
    public void RegraRecursoComSuspensividadePelaMetade_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDaRegraDeRecurso(envelope)["suspensividadePrimeiraInstanciaUnidade"] = null);

        resultado.IsFailure.Should().BeTrue(
            "o valor sobreviveu e a unidade sumiu — o par deixou de dizer em que relógio a janela corre");
        resultado.Error!.Code.Should().Be("RegraRecursoFase.SuspensividadeIncompleta");
    }

    /// <summary>
    /// A janela recursal da ETAPA restaurada com prazo zero é recusada — uma janela que abre e
    /// nunca fecha não é configuração, é dado corrompido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A irmã da FASE já recusa: <c>RegraRecursoFase.Reidratar</c> delega a <c>Criar</c>, e o
    /// doc dela enuncia o porquê — o envelope é caminho de construção que não passa pela porta
    /// HTTP, e uma regra restaurada com prazo não positivo é tão inutilizável quanto uma escrita
    /// assim pela primeira vez.
    /// </para>
    /// <para>
    /// O decodificador confere forma e precisão dos args, não as invariantes deles. Sem a
    /// validação na reidratação, quem controla os bytes controla o prazo — e o descarte de uma
    /// retificação reporia um certame cujo prazo recursal é zero.
    /// </para>
    /// </remarks>
    [Fact(DisplayName = "Janela recursal da etapa com prazo zero é recusada na reidratação")]
    public void RecursoDaEtapaComPrazoZero_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoRecursoDaEtapa(envelope)["prazoValor"] = "0.0000");

        resultado.IsFailure.Should().BeTrue(
            "prazo não positivo é invariante do domínio, e vale igual em quem escreve e em quem repõe");
        resultado.Error!.Message.Should().NotContain("esperado um texto",
            "a recusa tem de ser do prazo, e não da FORMA do prazo — decimal viaja como texto no "
            + "envelope, e escrever um número aqui recusaria por tipo, provando outra coisa");
    }

    [Fact(DisplayName = "Janela de recurso sem a declaração de instância superior no envelope: recusa, e não 'não admite' por omissão")]
    public void RegraRecursoSemAdmiteInstanciaSuperior_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDaRegraDeRecurso(envelope).Remove("admiteInstanciaSuperior"));

        resultado.IsFailure.Should().BeTrue(
            "a ausência lida como falso congelaria o recurso como terminando na 1ª instância sem que o edital o tenha dito");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Âncora do recurso apontando para produto de OUTRA fase: recusa nomeada, não exceção da fábrica")]
    public void RegraRecursoAncoradaEmProdutoDeOutraFase_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(AncorarORecursoEmProdutoDeOutraFase);

        resultado.IsFailure.Should().BeTrue(
            "a âncora passou a apontar para uma publicação que a fase não faz — o prazo do candidato correria " +
            "de um ato que não é o que o prejudicou");
        resultado.Error!.Code.Should().Be("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase");
    }

    [Fact(DisplayName = "Âncora do recurso apontando para produto NÃO preliminar da própria fase: recusa nomeada")]
    public void RegraRecursoAncoradaEmProdutoNaoPreliminar_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject comRecurso = envelope["cronogramaFases"]!["fases"]!.AsArray()
                .Select(f => f!.AsObject())
                .Single(f => f["regraRecurso"] is JsonObject);

            comRecurso["produtos"]!.AsArray()[0]!["papel"] = null;
            comRecurso["regraRecurso"]!["produtoAncoraId"] =
                comRecurso["produtos"]!.AsArray()[0]!["id"]!.GetValue<string>();
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase");
    }

    /// <summary>
    /// Adultera a janela que corre da CIÊNCIA, e não a que corre do ato, porque é ela que escapa por inteiro:
    /// DefinirRecursos classifica tudo que não é ato publicado como "ciencia" ao procurar janelas duplicadas,
    /// então trocar a de ciência por 'Nenhuma' mantém o balde ocupado uma vez só e não colide com nada. A
    /// guarda de ciência — "exige que a etapa prometa parecer individual" — também deixa de valer, porque só
    /// olha CienciaIndividual. Nenhuma outra checagem sobra.
    /// </summary>
    [Fact(DisplayName = "etapas[].recursos[].ancora com o sentinela 'Nenhuma' é recusada")]
    public void Etapa_RecursoSemAncora_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(TrocarAAncoraDeCienciaPorNenhuma);

        resultado.IsFailure.Should().BeTrue(
            "'Nenhuma' é a ausência declarada que RecursoDaEtapa.Criar recusa e que Reidratar não reconfere; " +
            "sem a guarda do decodificador o valor atravessa DefinirRecursos, persiste como ancora = 0, e a " +
            "reemissão dá os mesmos bytes — o certame volta com uma janela recursal cujo prazo não corre de " +
            "instante nenhum, e o round-trip aprova porque os bytes conferem");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("ancora");
    }

    /// <summary>
    /// O código da convenção de contagem pertence ao rol que o decodificador conhece.
    /// </summary>
    /// <remarks>
    /// Toda outra referência de regra do envelope é conferida contra um rol fechado; esta era a
    /// única que não. Um código fora do rol atravessava a leitura, persistia — a coluna o
    /// comporta — e devolvia ao certame restaurado uma convenção de contagem que motor nenhum
    /// implementa, sem nada acusar.
    /// </remarks>
    [Fact(DisplayName = "Convenção de contagem com código fora do rol é recusada na reidratação")]
    public void AlgoritmoContagemPrazoForaDoRol_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["algoritmoContagemPrazo"]!.AsObject()["codigo"] = "CONTAGEM-PRAZO-INEXISTENTE");

        resultado.IsFailure.Should().BeTrue(
            "um código que o sistema não reconhece não descreve convenção nenhuma, e restaurá-lo daria ao certame " +
            "um prazo que ninguém sabe contar");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.RegraDesconhecida);
    }

    /// <summary>
    /// O erro oposto, e o mais caro: rol estreito demais torna irreidratável um certame
    /// legitimamente publicado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Um rol do decodificador fechado em literais deixaria irreidratável o envelope publicado
    /// sob uma regra que a lista esquecesse, como a do PSIQ ou a de quadro fixo com AC_PCD. A
    /// configuração congelada é a evidência jurídica do certame; recusá-la deixa o descarte da
    /// retificação sem como repor o estado anterior.
    /// </para>
    /// <para>
    /// A asserção é de <b>sucesso</b>, e não de "falhou por outro motivo": nada no decodificador
    /// nem no domínio consome o código desta convenção — a referência é opaca —, então trocá-lo
    /// por outro do rol reidrata inteiro. O código que o corpus já usa fica fora da teoria
    /// porque escrevê-lo de novo não mudaria byte nenhum, e o helper exige que a adulteração
    /// mude os bytes.
    /// </para>
    /// </remarks>
    [Theory(DisplayName = "Toda convenção de contagem do rol atravessa a reidratação")]
    [InlineData(AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora)]
    [InlineData(AlgoritmoContagemPrazoCodigo.AvancaDataUtil)]
    public void AlgoritmoContagemPrazoDoRol_Reidrata(string codigo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["algoritmoContagemPrazo"]!.AsObject()["codigo"] = codigo);

        resultado.IsSuccess.Should().BeTrue(
            $"'{codigo}' pertence a AlgoritmoContagemPrazoCodigo.Todos — recusá-lo tornaria irreidratável um " +
            $"certame publicado sob ele. Recusado com: {resultado.Error?.Code} / {resultado.Error?.Message}");
    }

    /// <summary>
    /// O código e a versão da convenção de contagem cabem nas colunas que vão recebê-los.
    /// </summary>
    /// <remarks>
    /// Toda referência de regra do envelope é lida pelo leitor compartilhado, que mede
    /// código e versão contra a largura da coluna. A convenção de contagem foi durante um
    /// tempo a única lida fora dele — a forma dela carrega <c>presente</c> ao lado da tripla,
    /// e o fechamento de chaves embutido no leitor exigia exatamente <c>codigo</c>,
    /// <c>versao</c> e <c>hash</c> —, e por isso era a única que podia atravessar a leitura
    /// com um código largo demais. Hoje ela passa pelo mesmo leitor, com o fechamento de
    /// chaves a cargo do chamador, e este teste guarda o teto contra uma volta atrás. O valor
    /// recanonicaliza nos mesmos bytes, então a prova de round-trip aprova; a recusa só
    /// chegaria no <c>INSERT</c>, como <c>22001</c> traduzido em 500 no meio do descarte.
    /// </remarks>
    [Theory(DisplayName = "Convenção de contagem com código ou versão maior que a coluna é recusada na leitura")]
    [InlineData("codigo")]
    [InlineData("versao")]
    public void AlgoritmoContagemPrazoComCampoAcimaDaColuna_Recusa(string campo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["algoritmoContagemPrazo"]!.AsObject()[campo] = new string('X', 200));

        resultado.IsFailure.Should().BeTrue(
            $"'{campo}' com 200 caracteres não cabe na coluna que o vai receber, e quem descobre isso tem de ser " +
            "a leitura do envelope — não o INSERT do descarte");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    private static JsonObject ArgsDoRecursoDaEtapa(JsonObject envelope) =>
        envelope["etapas"]!.AsArray()
            .Select(e => e!.AsObject())
            .SelectMany(e => e["recursos"]!.AsArray().Select(r => r!.AsObject()))
            .First(r => r["ancora"]!.GetValue<string>() == "AtoPublicado")["args"]!
            .AsObject();

    /// <summary>
    /// Os <c>args</c> da regra de recurso da única fase do corpus que a declara. Localizada
    /// pela presença do bloco, não por índice fixo: uma fase nova no corpus deslocaria o
    /// índice e faria os testes acima passarem a adulterar outra coisa.
    /// </summary>
    private static JsonObject ArgsDaRegraDeRecurso(JsonObject envelope)
    {
        JsonObject fase = envelope["cronogramaFases"]!["fases"]!.AsArray()
            .Select(f => f!.AsObject())
            .Single(f => f["regraRecurso"] is JsonObject);

        return fase["regraRecurso"]!["args"]!.AsObject();
    }

    private static void AncorarORecursoEmProdutoDeOutraFase(JsonObject envelope)
    {
        JsonArray fases = envelope["cronogramaFases"]!["fases"]!.AsArray();
        JsonObject comRecurso = fases.Select(f => f!.AsObject()).Single(f => f["regraRecurso"] is JsonObject);
        JsonObject outra = fases.Select(f => f!.AsObject())
            .First(f => f["regraRecurso"] is not JsonObject && f["produtos"]!.AsArray().Count > 0);

        comRecurso["regraRecurso"]!["produtoAncoraId"] =
            outra["produtos"]!.AsArray()[0]!["id"]!.GetValue<string>();
    }

    private static void TrocarAAncoraDeCienciaPorNenhuma(JsonObject envelope) =>
        envelope["etapas"]!.AsArray()
            .SelectMany(etapa => etapa!["recursos"]!.AsArray())
            .First(recurso => recurso!["ancora"]!.GetValue<string>() == nameof(AncoraDoRecurso.CienciaIndividual))!
            ["ancora"] = nameof(AncoraDoRecurso.Nenhuma);
}
