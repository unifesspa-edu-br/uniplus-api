namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <b>O que a reidratação recusa</b> — e por quê: versão desconhecida do envelope, versão
/// conhecida mas não reidratável (ex.: a 1.0, que podia congelar classificação como stub),
/// e blocos derivados que não fecham entre si (Story #859, critérios de aceite sobre versão
/// e integridade dos blocos derivados; ADR-0110 sobre codec nunca aposentado e sobre o
/// bloco 'nao_construido' banido).
/// </summary>
/// <remarks>
/// Cada teste aqui é uma contraprova: sem a guarda que ele exercita, o envelope
/// reidrataria <b>com sucesso</b> num agregado que nunca existiu. É por isso que a recusa
/// é <b>nomeada</b> — um descarte que falha precisa dizer ao operador se o problema é uma
/// versão que o sistema não conhece, uma que ele conhece e não reidrata, ou uma evidência
/// que não prova o que diz provar.
/// </remarks>
public sealed class EnvelopeCodecRecusaTests
{
    // ── Versão desconhecida e versão conhecida-não-reidratável ──

    [Fact(DisplayName = "Versão fora do registro: recusa nomeada, não tentativa de leitura")]
    public void VersaoDesconhecida_Recusa()
    {
        VersaoConfiguracao versao = VersaoComSchema("9.9");

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);
    }

    // ── Integridade forense: a evidência tem de provar o que diz provar ──

    [Fact(DisplayName = "Bytes adulterados não produzem o hash persistido — recusa antes de qualquer parse")]
    public void IntegridadeViolada_Recusa()
    {
        // Bytes adulterados decodificam e recodificam IDENTICAMENTE — o round-trip passaria
        // sem que eles correspondessem mais ao HashConfiguracao. É o hash, não o round-trip,
        // que prova que os bytes são os que o ato congelou.
        (VersaoConfiguracao versao, byte[] bytes) = VersaoRica();

        JsonObject adulterado = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!.AsObject();
        adulterado["periodo"]!["numero"] = "666/2026";
        byte[] bytesAdulterados = PerfilCanonicoV1.Instancia.Serializar(adulterado);

        // A versão continua declarando o hash ORIGINAL — é o cenário de quem trocou os
        // bytes na coluna sem poder recomputar o hash da linha forense.
        VersaoConfiguracao comprometida = ComBytes(versao, bytesAdulterados);

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(comprometida);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
    }

    [Fact(DisplayName = "Bytes não-canônicos (chaves fora de ordem) são recusados, mesmo com o hash batendo")]
    public void BytesNaoCanonicos_Recusa()
    {
        // Este é o caso de quem adultera E recomputa o hash. O hash bate — mas os bytes não
        // estão na forma canônica (ADR-0100), e um envelope canônico é o que o encoder
        // produz. Reserializar o que se leu tem de reproduzir os bytes; se não reproduz, o
        // que está na coluna não é um envelope 1.1, é outra coisa com um hash coerente.
        (VersaoConfiguracao _, byte[] bytes) = VersaoRica();

        // Reordena as chaves do topo (o encoder as emite em ordem ordinal).
        JsonObject original = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!.AsObject();
        JsonObject foraDeOrdem = [];
        foreach (KeyValuePair<string, JsonNode?> par in original.Reverse().ToList())
        {
            foraDeOrdem[par.Key] = par.Value?.DeepClone();
        }

        byte[] naoCanonicos = Encoding.UTF8.GetBytes(foraDeOrdem.ToJsonString());
        naoCanonicos.Should().NotEqual(bytes, "pré-condição: a reordenação tem de produzir bytes distintos");

        ProcessoSeletivo processo = ProcessoPublicado();
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, naoCanonicos);

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
    }

    [Fact(DisplayName = "Algoritmo de hash que o codec não emite é recusado")]
    public void AlgoritmoNaoSuportado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            CorpusEnvelope.Codec.SchemaVersion,
            algoritmoHash: "md5@v0",
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            atoCriadorHash: CorpusEnvelope.HashDocumento,
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.AlgoritmoNaoSuportado);
    }

    // ── Coerência envelope × linha ──

    [Fact(DisplayName = "O hash do documento no envelope tem de ser o do ato criador da versão")]
    public void HashDocumentoDivergente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            CorpusEnvelope.Codec.SchemaVersion,
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            // O envelope carrega o hash do corpus; a linha declara outro. Uma das duas
            // evidências está errada — e não há como saber qual.
            atoCriadorHash: new string('7', 64),
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

    [Fact(DisplayName = "A versão 1 não pode carregar o 18º bloco — ela abre a cadeia e não retifica ato algum")]
    public void Versao1ComBlocoDeRetificacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(
            CorpusEnvelope.ProcessoRico(),
            new RetificacaoInfo(CorpusEnvelope.AtoAbertura, "motivo qualquer"))).Bytes;

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, bytes);

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

    [Fact(DisplayName = "A versão N>1 sem o 18º bloco é recusada — a cadeia de retificação não se perde")]
    public void VersaoSucessoraSemBlocoDeRetificacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] semRetificacao = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        VersaoConfiguracao v1 = CorpusEnvelope.VersaoDeAbertura(processo, semRetificacao);
        VersaoConfiguracao v2 = CorpusEnvelope.VersaoDeRetificacao(v1, semRetificacao);

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(v2);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

    [Fact(DisplayName = "O ato retificado no envelope tem de ser o que a versão registra ter emendado")]
    public void AtoRetificadoDivergente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] abertura = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;
        byte[] retificada = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(
            CorpusEnvelope.ProcessoRico(),
            new RetificacaoInfo(new Guid("01900000-0000-7000-8000-00000000dead"), "motivo"))).Bytes;

        VersaoConfiguracao v1 = CorpusEnvelope.VersaoDeAbertura(processo, abertura);
        VersaoConfiguracao v2 = CorpusEnvelope.VersaoDeRetificacao(v1, retificada);

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(v2);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

    // ── A gramática é FECHADA em todos os níveis ──

    /// <summary>
    /// O caso crítico é o <c>bonusRegional</c>: um leitor que só olhasse <c>presente</c>
    /// leria <c>{"presente":false,"fator":"1.2000",…}</c> como “sem bônus” e
    /// <b>descartaria o bônus regional do certame</b> (RN05) sem deixar rastro. É o modo
    /// de falha exato que esta story existe para tornar impossível.
    /// </summary>
    [Theory(DisplayName = "Chave desconhecida em QUALQUER nível é recusada — um leitor tolerante perde configuração em silêncio")]
    [InlineData("$")]
    [InlineData("bonusRegional")]
    [InlineData("periodo")]
    [InlineData("hashesEdital")]
    [InlineData("atendimento")]
    [InlineData("classificacao")]
    [InlineData("etapas.0")]
    [InlineData("distribuicao.0")]
    [InlineData("distribuicao.0.regraDistribuicao")]
    [InlineData("distribuicao.0.referenciaDemografica")]
    [InlineData("modalidades.0")]
    [InlineData("criteriosDesempate.0")]
    [InlineData("criteriosDesempate.0.args")]
    [InlineData("criteriosDesempate.0.regra")]
    [InlineData("criteriosDesempate.5.args")]
    [InlineData("classificacao.regrasEliminacao.0")]
    [InlineData("classificacao.regrasEliminacao.0.args")]
    [InlineData("classificacao.regrasEliminacao.0.regra")]
    [InlineData("classificacao.regraCalculo")]
    [InlineData("classificacao.quadroPesoAreaEnem.0")]
    [InlineData("classificacao.quadroPesoAreaEnem.0.grupoAreaEnem")]
    [InlineData("classificacao.quadroPesoAreaEnem.0.areas.0")]
    [InlineData("atendimento.condicoes.0")]
    [InlineData("atendimento.recursos.0")]
    [InlineData("atendimento.tiposDeficiencia.0")]
    [InlineData("bonusRegional.regra")]
    [InlineData("vagas.0")]
    [InlineData("cascataRemanejamento")]
    [InlineData("cascataRemanejamento.regra")]
    [InlineData("cascataRemanejamento.ordens.0")]
    [InlineData("identidadesUnidade")]
    [InlineData("identidadesUnidade.administradora")]
    [InlineData("divulgacao")]
    // Os dois objetos de regra OPCIONAIS e o bloco da convenção de contagem. Os obrigatórios
    // acima já cobriam o leitor compartilhado; estes três não passavam por caso nenhum, e um
    // fechamento de chaves esquecido neles não acusaria em lugar algum da suíte.
    [InlineData("classificacao.regraArredondamento")]
    [InlineData("distribuicao.0.regraAjuste")]
    [InlineData("algoritmoContagemPrazo")]
    public void ChaveDesconhecida_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject alvo = caminho == "$"
                ? envelope
                : Navegar(envelope, caminho);
            alvo["chaveIntrusa"] = "x";
        });

        resultado.IsFailure.Should().BeTrue(
            $"uma chave desconhecida em '{caminho}' significa que o envelope tem uma forma que este codec não " +
            "conhece — ignorá-la é a definição de perder dado em silêncio");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "O caso crítico: {presente:false} com args de bônus é RECUSADO, não lido como 'sem bônus'")]
    public void BonusPresenteFalsoComArgs_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject bonus = envelope["bonusRegional"]!.AsObject();
            bonus["presente"] = false;
            bonus.Remove("regra");
            bonus.Remove("teto");
            bonus.Remove("baseLegalBonusRegionalId");
            bonus.Remove("tipoInstrumento");
            bonus.Remove("identificacao");
            bonus.Remove("descricao");
            bonus.Remove("municipios");
            // Sobra `fator` — a forma "ausente" só admite `presente`.
        });

        resultado.IsFailure.Should().BeTrue(
            "um bônus 'ausente' que ainda carrega args é envelope contraditório. Lê-lo como 'sem bônus' descartaria " +
            "o bônus regional (RN05) de um certame publicado, e ninguém veria.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Mesmo raciocínio de <see cref="BonusPresenteFalsoComArgs_Recusa"/>, agora para a
    /// cascata (Story #575): um leitor que só olhasse <c>presente</c> leria
    /// <c>{"presente":false,"regra":{...},...}</c> como "sem cascata" e descartaria a
    /// sequência legal de remanejamento (RN-CASCATA-1) de um certame publicado, sem
    /// deixar rastro — exatamente o modo de falha que <c>ExigirChaves</c> torna impossível.
    /// </summary>
    [Fact(DisplayName = "O caso crítico: {presente:false} com args de cascata é RECUSADO, não lido como 'sem cascata'")]
    public void CascataPresenteFalsoComArgs_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject cascata = envelope["cascataRemanejamento"]!.AsObject();
            cascata["presente"] = false;
            // Sobra `regra`, `fallbackCodigo` e `ordens` — a forma "ausente" só admite `presente`.
        });

        resultado.IsFailure.Should().BeTrue(
            "uma cascata 'ausente' que ainda carrega regra/fallback/ordens é envelope contraditório. Lê-la como " +
            "'sem cascata' descartaria a sequência legal de remanejamento (RN-CASCATA-1) de um certame publicado, " +
            "e ninguém veria.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Um envelope com <c>"presente"</c> duas vezes tem <b>duas leituras possíveis</b>, e o
    /// hash cobre as duas igualmente. Aceitar a última seria escolher em silêncio.
    /// </summary>
    /// <remarks>
    /// A recusa vem de <b>duas</b> guardas independentes, e é deliberado que venha: a forma
    /// canônica (um envelope com chave repetida não é o que <c>ComputeSnapshotBytes</c>
    /// produz) e o <c>AllowDuplicateProperties = false</c> no parse. A primeira é a que de
    /// fato dispara hoje — a segunda existe porque uma versão futura poderia ter forma
    /// canônica diferente, e a garantia não pode depender disso.
    /// </remarks>
    [Fact(DisplayName = "Chave JSON duplicada é recusada — não vale 'a última ganha'")]
    public void ChaveDuplicada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        string json = Encoding.UTF8.GetString(bytes);
        string duplicado = json.Replace("\"presente\":true", "\"presente\":true,\"presente\":false", StringComparison.Ordinal);
        duplicado.Should().NotBe(json, "pré-condição: a duplicação tem de ter sido aplicada");

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, Encoding.UTF8.GetBytes(duplicado));

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().BeOneOf(
            ErrosCodecEnvelope.EnvelopeMalformado,
            ErrosCodecEnvelope.IntegridadeViolada);
    }

    [Fact(DisplayName = "Chave desconhecida DENTRO do 18º bloco (retificacao) é recusada")]
    public void ChaveDesconhecidaNaRetificacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] abertura = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(
            CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(
                CorpusEnvelope.ProcessoRico(),
                new RetificacaoInfo(CorpusEnvelope.AtoAbertura, "motivo"))).Bytes))!.AsObject();

        envelope["retificacao"]!.AsObject()["chaveIntrusa"] = "x";

        VersaoConfiguracao v1 = CorpusEnvelope.VersaoDeAbertura(processo, abertura);
        VersaoConfiguracao v2 = CorpusEnvelope.VersaoDeRetificacao(
            v1, PerfilCanonicoV1.Instancia.Serializar(envelope));

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(v2);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Bloco real com chaves de um formato de stub extinto é forma intrusa — recusado, não tolerado")]
    [InlineData("vagas")]
    [InlineData("documentosExigidos")]
    [InlineData("cascataRemanejamento")]
    public void BlocoRealComFormaDeStubExtinto_Recusa(string bloco)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope[bloco] = new JsonObject { ["status"] = "construido", ["itens"] = new JsonArray() });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Bloco obrigatório ausente é recusado")]
    public void BlocoAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope.Remove("atendimento"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── issue #563 — CA-12: o bloco divulgacao promovido de stub a real precisa de um decoder
    // tão estrito quanto o encoder. Round-trip verde prova que o que foi emitido sobrevive; não
    // prova que o decoder recusa o que não deveria aceitar. Cada linha adultera UM aspecto do
    // bloco default (CorpusEnvelope.ProcessoRico não configura divulgação) e assere o código E o
    // caminho ofensivos (via trecho da mensagem) — nunca apenas IsFailure, que passaria por
    // qualquer outro motivo, nem apenas o código, que um mesmo EnvelopeMalformado genérico
    // compartilha entre guards diferentes. ──

    public static IEnumerable<object[]> MutacoesDeDivulgacao()
    {
        static JsonObject Bloco(JsonObject envelope) => envelope["divulgacao"]!.AsObject();

        yield return
        [
            "token estranho em camposPublicos (vocabulário fechado)",
            (Action<JsonObject>)(envelope => Bloco(envelope)["camposPublicos"] =
                new JsonArray(JsonValue.Create("cpf"), JsonValue.Create("numero_inscricao"))),
            "ConfiguracaoDivulgacao.CampoNaoPermitido",
            // ADR-0023: errors[].message nunca ecoa o valor rejeitado — a mensagem é
            // genérica desde a migração para acumulação (ADR-0125), não mais "'cpf'".
            "vocabulário de divulgação pública",
        ];
        yield return
        [
            "token repetido (sem repetição)",
            (Action<JsonObject>)(envelope => Bloco(envelope)["camposPublicos"] =
                new JsonArray(JsonValue.Create("numero_inscricao"), JsonValue.Create("numero_inscricao"))),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.camposPublicos",
        ];
        yield return
        [
            "array fora da ordem canônica (a ordenação faz parte dos bytes)",
            (Action<JsonObject>)(envelope =>
            {
                JsonObject bloco = Bloco(envelope);
                bloco["camposPublicos"] = new JsonArray(JsonValue.Create("numero_inscricao"), JsonValue.Create("nome_abreviado"));
                bloco["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente;
            }),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.camposPublicos",
        ];
        yield return
        [
            "sem numero_inscricao (o piso)",
            (Action<JsonObject>)(envelope =>
            {
                JsonObject bloco = Bloco(envelope);
                bloco["camposPublicos"] = new JsonArray(JsonValue.Create("nome_abreviado"));
                bloco["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente;
            }),
            "ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio",
            "numero_inscricao",
        ];
        yield return
        [
            "nome e nome_abreviado juntos (formas excludentes)",
            (Action<JsonObject>)(envelope =>
            {
                JsonObject bloco = Bloco(envelope);
                bloco["camposPublicos"] = new JsonArray(
                    JsonValue.Create("nome"), JsonValue.Create("nome_abreviado"), JsonValue.Create("numero_inscricao"));
                bloco["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente;
                bloco["justificativa"] = "Justificativa qualquer.";
            }),
            "ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes",
            "ao mesmo tempo",
        ];
        yield return
        [
            "regraNomeAbreviado preenchido sem nome_abreviado (regra sem abreviação)",
            (Action<JsonObject>)(envelope => Bloco(envelope)["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.regraNomeAbreviado",
        ];
        yield return
        [
            "nome_abreviado sem regraNomeAbreviado (abreviação sem regra)",
            (Action<JsonObject>)(envelope => Bloco(envelope)["camposPublicos"] =
                new JsonArray(JsonValue.Create("nome_abreviado"), JsonValue.Create("numero_inscricao"))),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.regraNomeAbreviado",
        ];
        yield return
        [
            "regra com identificador desconhecido (despacho por identificador conhecido)",
            (Action<JsonObject>)(envelope =>
            {
                JsonObject bloco = Bloco(envelope);
                bloco["camposPublicos"] = new JsonArray(JsonValue.Create("nome_abreviado"), JsonValue.Create("numero_inscricao"));
                bloco["regraNomeAbreviado"] = "regra_do_futuro";
            }),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "regra_do_futuro",
        ];
        yield return
        [
            "chave do bloco ausente (forma fechada)",
            (Action<JsonObject>)(envelope => Bloco(envelope).Remove("justificativa")),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "'justificativa' está ausente",
        ];
        yield return
        [
            "chave intrusa no bloco (forma fechada)",
            (Action<JsonObject>)(envelope => Bloco(envelope)["chaveIntrusa"] = "x"),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "chaveIntrusa",
        ];
        yield return
        [
            "tipo errado — camposPublicos como objeto, não array",
            (Action<JsonObject>)(envelope => Bloco(envelope)["camposPublicos"] = new JsonObject()),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.camposPublicos",
        ];
        yield return
        [
            "forma provisória antiga {\"status\":\"nao_construido\"} — o stub deixou de existir",
            (Action<JsonObject>)(envelope => envelope["divulgacao"] = new JsonObject { ["status"] = "nao_construido" }),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "'camposPublicos' está ausente",
        ];
        // As linhas abaixo substituem um único caso anterior que se dizia "decombinada e com
        // espaço nas bordas", mas usava "ç"/"ã" PRÉ-COMPOSTOS (NFC) — os espaços nas bordas já
        // bastavam para a recusa, e a guarda de normalização nunca era exercitada de fato. Cada
        // aspecto agora é um caso independente. O caso de NFD real (combinantes de verdade) NÃO
        // está aqui: PerfilCanonicoV1.Serializar, que ReidratarComEnvelopeAdulterado usa para
        // produzir os bytes finais, aplica NFC a toda string de negócio — "curaria" a própria
        // adulteração antes de o decoder vê-la, e o teste nunca observaria a recusa. É
        // Divulgacao_JustificativaEmNfdReal_Recusa, logo abaixo desta teoria, com um
        // serializador que não recanonicaliza — e por isso mesmo é pego pelo gate universal de
        // forma canônica (IntegridadeViolada), não pelo guard de LerDivulgacao (EnvelopeMalformado):
        // decomposição genuína nunca sobrevive para chegar ao decoder específico da versão.
        yield return
        [
            "justificativa já em NFC com espaço nas bordas — só o trim falha",
            (Action<JsonObject>)(envelope => Bloco(envelope)["justificativa"] = "  Divulgação com espaço nas bordas.  "),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.justificativa",
        ];
        yield return
        [
            "justificativa vazia — o codificador nunca emite essa forma, só null",
            (Action<JsonObject>)(envelope => Bloco(envelope)["justificativa"] = ""),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.justificativa",
        ];
        yield return
        [
            "justificativa acima do limite de 1000 caracteres da coluna",
            (Action<JsonObject>)(envelope => Bloco(envelope)["justificativa"] = new string('a', LimitesDoEnvelope.Justificativa + 1)),
            ErrosCodecEnvelope.EnvelopeMalformado,
            "divulgacao.justificativa",
        ];
        yield return
        [
            "justificativa contém o caractere nulo (U+0000) — sobrevive a trim, NFC e ao limite de comprimento",
            (Action<JsonObject>)(envelope => Bloco(envelope)["justificativa"] = $"Divulgação com {(char)0} no meio."),
            "ConfiguracaoDivulgacao.JustificativaComCaractereNulo",
            "caractere nulo",
        ];
    }

    [Theory(DisplayName = "issue #563 (CA-12): cada mutação do bloco divulgacao ataca UMA invariante e é recusada com o código e o caminho esperados")]
    [MemberData(nameof(MutacoesDeDivulgacao))]
    public void Divulgacao_CadaMutacaoAtacaUmaInvariante_Recusa(
        string descricao, Action<JsonObject> mutar, string codigoEsperado, string trechoEsperado)
    {
        ArgumentNullException.ThrowIfNull(mutar);

        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(mutar);

        resultado.IsFailure.Should().BeTrue($"a mutação '{descricao}' tem de ser recusada");
        resultado.Error!.Code.Should().Be(codigoEsperado, $"a mutação '{descricao}' tem de recusar com o código específico da invariante que ela ataca, não com 'IsFailure' por qualquer outro motivo");
        resultado.Error!.Message.Should().Contain(trechoEsperado, $"a mutação '{descricao}' tem de apontar para o campo ofensivo, não só recusar pelo código genérico");
    }

    /// <summary>
    /// O par do primeiro caso da teoria de recusa acima ("justificativa já em NFC com espaço
    /// nas bordas"): aqui a adulteração é NFD de verdade — "c" + combinante cedilha (U+0327) e
    /// "a" + combinante til (U+0303), sem espaço nas bordas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>O código esperado é <see cref="ErrosCodecEnvelope.IntegridadeViolada"/>, não
    /// <see cref="ErrosCodecEnvelope.EnvelopeMalformado"/></b> — e essa é a prova em si, não um
    /// acidente de teste. Decomposição Unicode genuína em QUALQUER string do envelope já é
    /// recusada por <c>RegistroCodecsEnvelope.VerificarFormaCanonica</c>, o gate universal que
    /// roda ANTES do decoder específico da versão: reserializar o payload inteiro pelo perfil
    /// canônico aplica NFC a toda string de negócio, produz bytes diferentes dos que chegaram, e
    /// o gate recusa por integridade — o decoder nem chega a rodar. O guard de forma canônica
    /// dentro de <c>LerDivulgacao</c> só é alcançável pelo caso de espaço nas bordas (acima): NFC
    /// não mexe em espaço, então esse caso passa incólume pelo gate universal e só é pego pelo
    /// guard específico do bloco <c>divulgacao</c>.
    /// </para>
    /// <para>
    /// Não usa <see cref="ReidratarComEnvelopeAdulterado"/>: aquele helper produz os bytes finais
    /// com <see cref="PerfilCanonicoV1.Serializar"/>, que "curaria" a decomposição antes mesmo de
    /// os bytes existirem — o teste nunca conseguiria observar o gate rejeitando nada. Aqui os
    /// bytes são escritos por um serializador comum, preservando os combinantes tal como foram
    /// escritos.
    /// </para>
    /// </remarks>
    [Fact(DisplayName = "issue #563 (CA-12): justificativa em NFD real é recusada pelo gate universal de forma canônica, antes de o decoder da versão rodar")]
    public void Divulgacao_JustificativaEmNfdReal_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] originais = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(originais))!.AsObject();
        envelope["divulgacao"]!.AsObject()["justificativa"] = $"Divulgac{(char)0x0327}a{(char)0x0303}o real";

        byte[] adulterados = Encoding.UTF8.GetBytes(envelope.ToJsonString());
        adulterados.Should().NotEqual(originais, "pré-condição: a adulteração tem de mudar os bytes");

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, adulterados);
        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue("a justificativa decomposta (NFD) não está na forma canônica que o gate universal exige");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
        resultado.Error!.Message.Should().Contain("forma canônica");
    }

    // ── Vocabulário fechado: as factories do domínio NÃO o fecham ──

    /// <summary>
    /// As factories têm ramos <c>default</c> que engolem código desconhecido: um
    /// <c>DISTRIB-VAGAS-XPTO</c> passaria como <b>institucional</b>
    /// (<c>ConfiguracaoDistribuicaoVagas:116</c> só testa <c>== Lei12711</c>), e um
    /// <c>FORMULA-XPTO</c> como <b>cálculo local</b> (<c>ConfiguracaoClassificacao:632</c>
    /// só testa <c>== ClassificacaoImportada</c>). Os dois fariam round-trip perfeito
    /// reconstruindo uma configuração <b>diferente da congelada</b>.
    /// </summary>
    [Theory(DisplayName = "Código de regra fora do rol é recusado — o ramo default das factories o aceitaria")]
    [InlineData("distribuicao.0.regraDistribuicao", "DISTRIB-VAGAS-XPTO")]
    [InlineData("classificacao.regraCalculo", "FORMULA-XPTO")]
    [InlineData("classificacao.regraArredondamento", "PRECISAO-XPTO")]
    [InlineData("classificacao.regraOrdemAlocacao", "ALOCACAO-XPTO")]
    [InlineData("bonusRegional.regra", "BONUS-XPTO")]
    [InlineData("criteriosDesempate.0.regra", "DESEMPATE-XPTO")]
    [InlineData("classificacao.regrasEliminacao.0.regra", "ELIM-XPTO")]
    [InlineData("cascataRemanejamento.regra", "REMANEJ-XPTO")]
    public void CodigoDeRegraForaDoRol_Recusa(string caminho, string codigo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            Navegar(envelope, caminho)["codigo"] = codigo);

        resultado.IsFailure.Should().BeTrue(
            $"'{codigo}' não pertence ao rol. As factories do domínio não fecham o vocabulário — um código " +
            "desconhecido cairia no ramo default delas e reconstruiria configuração diferente da congelada.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.RegraDesconhecida);
    }

    // ── Coerência natureza × remanejamento: a regra é do CADASTRO, e o snapshot a traz por valor ──

    /// <summary>
    /// A tabela de coerência vive no <b>cadastro</b> de modalidades
    /// (<c>Modalidade.ValidarCoerenciaNaturezaRemanejamento</c>, módulo Configuração), e por
    /// isso o comando <b>nunca</b> a viola: o handler não lê estes campos do payload —
    /// <b>copia-os da view do cadastro</b>. Mas o snapshot-copy (ADR-0061) os congela
    /// <b>por valor</b>, e quem reconstrói a modalidade a partir dos bytes não passa pelo
    /// cadastro.
    /// </summary>
    [Theory(DisplayName = "Natureza legal incoerente com a regra de remanejamento é RECUSADA na reidratação")]
    // Uma ampla concorrência que remaneja as próprias vagas ociosas — para onde?
    [InlineData("Ampla", "DestinoUnico")]
    [InlineData("Ampla", "SegueCascata")]
    [InlineData("Ampla", "Cruzado")]
    // Uma cota reservada da Lei 12.711 que foge da cascata legal (INV-12).
    [InlineData("CotaReservada", "Nenhuma")]
    [InlineData("CotaReservada", "DestinoUnico")]
    // Uma suplementar que não remaneja para lugar nenhum.
    [InlineData("Suplementar", "Nenhuma")]
    [InlineData("OutraModalidade", "SegueCascata")]
    public void NaturezaIncoerenteComRemanejamento_Recusa(string natureza, string remanejamento)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject modalidade = envelope["modalidades"]!.AsArray()[0]!.AsObject();
            modalidade["naturezaLegal"] = natureza;
            modalidade["regraRemanejamento"] = remanejamento;
        });

        resultado.IsFailure.Should().BeTrue(
            $"'{natureza}' com remanejamento '{remanejamento}' é combinação que o cadastro proíbe e que o caminho de " +
            "escrita jamais produz — mas o encoder reemite os dois enums verbatim, então ela faz round-trip PERFEITO. " +
            "Restaurá-la entregaria ao motor de vagas do certame publicado uma configuração que nunca existiu.");
    }

    /// <summary>
    /// O <c>acaoQuandoIndeferido</c> é <b>domínio fechado</b> no cadastro
    /// (<c>RECLASSIFICAR_AC</c> · <c>RECLASSIFICAR_REGRA_EDITAL</c>), e o comando nunca
    /// produz outro token — ele <b>copia</b> o da view. Um token inventado no envelope faria
    /// round-trip perfeito e restauraria uma <b>instrução que o motor de homologação não sabe
    /// executar</b>.
    /// </summary>
    [Theory(DisplayName = "Ação ao indeferir fora do domínio fechado do cadastro é recusada")]
    [InlineData("INDEFERE")]
    [InlineData("RECLASSIFICA_AC")]
    [InlineData("reclassificar_ac")]
    [InlineData("QUALQUER_COISA")]
    public void AcaoQuandoIndeferidoForaDoCadastro_Recusa(string token)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["modalidades"]!.AsArray()[1]!["acaoQuandoIndeferido"] = token);

        resultado.IsFailure.Should().BeTrue(
            $"'{token}' não pertence ao domínio fechado do cadastro — restaurá-lo daria ao motor de homologação " +
            "uma instrução que ele não sabe executar");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// O código da modalidade é <b>chave</b>: a composição (<c>RETIRA_DE</c>) e o
    /// remanejamento (<c>DESTINO_UNICO</c>, <c>CRUZADO</c>) apontam para códigos de outras
    /// modalidades da mesma oferta. O cadastro impõe <c>^[A-Z0-9_]+$</c>.
    /// </summary>
    [Theory(DisplayName = "Código de modalidade fora do formato do cadastro é recusado — códigos são CHAVE")]
    [InlineData("modalidades.0.codigo", "ac")]
    [InlineData("modalidades.0.codigo", "AC-2")]
    [InlineData("modalidades.0.codigo", "Ampla Concorrência")]
    public void CodigoDeModalidadeForaDoFormato_Recusa(string caminho, string codigo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            Navegar(envelope, caminho[..caminho.LastIndexOf('.')])["codigo"] = codigo);

        resultado.IsFailure.Should().BeTrue(
            $"'{codigo}' não tem o formato do cadastro (A-Z, 0-9, underscore) — e um código inválido vira chave do " +
            "grafo de remanejamento que o motor de vagas do certame vai percorrer");
        resultado.Error!.Code.Should().BeOneOf(
            ErrosCodecEnvelope.EnvelopeMalformado,
            ErrosCodecEnvelope.BlocosDerivadosIncoerentes);
    }

    /// <summary>
    /// O código da condição é <b>chave natural</b>: é por ele que a invariante ADR-0067
    /// reconhece a condição PcD (<c>OfertaAtendimentoEspecializado.CodigoCondicaoPcd</c> —
    /// <c>"PCD"</c>). O cadastro impõe <c>^[A-Z][A-Z0-9_]{1,49}$</c>; <c>OfertaCondicao.Criar</c>
    /// apenas faz <c>Trim</c>.
    /// </summary>
    [Theory(DisplayName = "Código de condição fora do formato do cadastro é recusado — é CHAVE da invariante PcD")]
    [InlineData("pcd")]
    [InlineData("P CD")]
    [InlineData("1PCD")]
    [InlineData("P")]
    public void CodigoDeCondicaoForaDoFormato_Recusa(string codigo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["atendimento"]!["condicoes"]!.AsArray()[0]!["condicaoCodigo"] = codigo);

        resultado.IsFailure.Should().BeTrue(
            $"'{codigo}' não tem o formato do cadastro. Um 'pcd' minúsculo restaurado deixaria de ser reconhecido " +
            "como a condição PcD (ADR-0067) — e os tipos de deficiência do certame ficariam ofertados sob uma " +
            "condição que ninguém identifica");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// O cadastro normaliza os critérios (<c>Modalidade.NormalizarCriterios</c>) e o motor de
    /// homologação os avalia por <b>comparação exata</b>. Um item em branco é um requisito
    /// que não diz nada; um item com espaços nas pontas é pior — é um requisito que
    /// <b>ninguém reconhece</b>. O encoder reemite o array verbatim, então os dois fazem
    /// round-trip perfeito.
    /// </summary>
    [Theory(DisplayName = "Critério cumulativo em branco ou não-normalizado é recusado")]
    [InlineData("   ")]
    [InlineData(" renda_per_capita_ate_1sm ")]
    [InlineData("renda_per_capita_ate_1sm ")]
    [InlineData("\trenda_per_capita_ate_1sm")]
    public void CriterioCumulativoMalFormado_Recusa(string criterio)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["modalidades"]!.AsArray()[1]!["criteriosCumulativos"]!.AsArray()[0] = criterio);

        resultado.IsFailure.Should().BeTrue(
            $"'{criterio}' não é a forma que o cadastro produz. A guarda RECUSA em vez de normalizar: aparar o " +
            "espaço aqui mudaria o valor em relação aos bytes congelados, e a prova de round-trip passaria a " +
            "recusar o descarte de um certame legítimo — trocaríamos um dado sujo por um certame sem descarte.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── Os três blocos derivados ('distribuicao', 'modalidades', 'ofertas') têm de fechar ──

    [Fact(DisplayName = "Modalidade cuja oferta não existe em 'distribuicao' é recusada")]
    public void ModalidadeSemDistribuicao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["modalidades"]!.AsArray()[0]!["ofertaCursoOrigemId"] = "44440000-0000-4000-8000-000000000009");

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "Distribuição sem nenhuma modalidade é recusada")]
    public void DistribuicaoSemModalidade_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            string oferta = envelope["distribuicao"]!.AsArray()[0]!["ofertaCursoOrigemId"]!.GetValue<string>();
            JsonArray sobreviventes = [];
            IEnumerable<JsonNode?> deOutrasOfertas = envelope["modalidades"]!.AsArray()
                .Where(m => m!["ofertaCursoOrigemId"]!.GetValue<string>() != oferta);

            foreach (JsonNode? modalidade in deOutrasOfertas)
            {
                sobreviventes.Add(modalidade!.DeepClone());
            }

            envelope["modalidades"] = sobreviventes;
        });

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "Oferta ausente do bloco 'ofertas' é recusada")]
    public void OfertaAusenteDoBloco_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray ofertas = envelope["ofertas"]!.AsArray();
            ofertas.RemoveAt(0);
        });

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "Oferta EXTRA no bloco 'ofertas' é recusada (a igualdade é de conjuntos, não inclusão)")]
    public void OfertaExtraNoBloco_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["ofertas"]!.AsArray().Add("44440000-0000-4000-8000-000000000009"));

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "Oferta DUPLICADA em 'ofertas' é recusada")]
    public void OfertaDuplicadaNoBloco_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray ofertas = envelope["ofertas"]!.AsArray();
            ofertas.Add(ofertas[0]!.GetValue<string>());
        });

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "Oferta DUPLICADA em 'distribuicao' é recusada")]
    public void OfertaDuplicadaEmDistribuicao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray distribuicao = envelope["distribuicao"]!.AsArray();
            distribuicao.Add(distribuicao[0]!.DeepClone());
        });

        AssertIncoerencia(resultado);
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

    private static JsonObject ArgsDoRecursoDaEtapa(JsonObject envelope) =>
        envelope["etapas"]!.AsArray()
            .Select(e => e!.AsObject())
            .SelectMany(e => e["recursos"]!.AsArray().Select(r => r!.AsObject()))
            .First(r => r["ancora"]!.GetValue<string>() == "AtoPublicado")["args"]!
            .AsObject();

    private static void AssertIncoerencia(Result<EnvelopeReidratado> resultado)
    {
        resultado.IsFailure.Should().BeTrue(
            "'distribuicao', 'modalidades' e 'ofertas' derivam da MESMA coleção (ADR-0110 D8). Recombiná-los em " +
            "silêncio quando não fecham reconstruiria um agregado que nunca existiu.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.BlocosDerivadosIncoerentes);
    }

    // ── Story #923 — exigenciaId duplicado em documentosExigidos ──

    /// <summary>
    /// Um encoder real nunca produz <c>exigenciaId</c> duplicado — mas um envelope
    /// adulterado poderia. Sem <see cref="EnvelopeCodec.IndexarExigenciasPorId"/>, o
    /// <c>ToDictionary</c> ingênuo lançaria <see cref="ArgumentException"/> (500 não tratado
    /// no meio de uma restauração) em vez de recusar com um <see cref="DomainError"/> nomeado.
    /// </summary>
    [Fact(DisplayName = "Story #923: exigenciaId duplicado em documentosExigidos.exigencias é recusado, não lança")]
    public void ExigenciaIdDuplicado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Exigência Duplicada", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(), voBase: 40, pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
            regraAjuste: null, referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
                    null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021",
                    quantidadeDeclarada: 40).Value!,
            ]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!,
            regraArredondamento: null, casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('c', 64)).Value!,
            nOpcoesAlocacao: 1, regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: true, permiteComplementacao: true, coletaInscricao: true, coletaSolicitacaoIsencao: false, inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FormatosPermitidos qualquer = FormatosPermitidos.Criar(true, null).Value!;
        DocumentoExigido rg = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null, qualquer, null).Value!;
        DocumentoExigido cpf = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "CPF", "CPF", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null, qualquer, null).Value!;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            "088/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, hashDocumento, FusoInstitucional.ZoneId));

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(congelado.Bytes))!.AsObject();
        JsonArray exigencias = envelope["documentosExigidos"]!["exigencias"]!.AsArray();
        exigencias.Should().HaveCount(2, "pré-condição: duas exigências para duplicar o id de uma na outra");
        exigencias[1]!["exigenciaId"] = exigencias[0]!["exigenciaId"]!.DeepClone();

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(envelope);
        adulterados.Should().NotEqual(congelado.Bytes, "pré-condição: a adulteração tem de mudar os bytes");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, adulterados, congelado.SchemaVersion, congelado.AlgoritmoHash, hashDocumento, "testes", TimeProvider.System, CorpusEnvelope.ContextoRico());
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);

        Result<EnvelopeReidratado> resultado = new RegistroCodecsEnvelope().Reidratar(publicacao.Value!);

        resultado.IsFailure.Should().BeTrue(
            "exigenciaId duplicado só é alcançável por adulteração — um encoder real nunca o produz — e a " +
            "restauração precisa recusar nomeadamente, não lançar ArgumentException do ToDictionary ingênuo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── Issue #1059 (UNI-REQ-0072) — recusas do decoder nos dois blocos de valores selecionáveis,
    // e a bicondicional de fatosColetados[].valoresSelecionaveis com tipoRenderizacao nos dois sentidos ──

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis ausente é recusado — omitir a chave não é o mesmo que declará-la null")]
    public void ValoresSelecionaveis_ChaveAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA").Remove("valoresSelecionaveis"));

        resultado.IsFailure.Should().BeTrue(
            "a gramática de cada item de fatosColetados fecha em 'valoresSelecionaveis' — o encoder sempre a " +
            "emite, presente e explícita (array ou null), nunca omitida");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis de tipo errado (nem array nem null) é recusado")]
    public void ValoresSelecionaveis_TipoErrado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"] = "BRANCA");

        resultado.IsFailure.Should().BeTrue(
            "um escalar no lugar do array (ou de null) é forma que o encoder nunca produz para este campo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis[].valorCodigo duplicado é recusado")]
    public void ValoresSelecionaveis_ValorCodigoDuplicado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray valores = FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"]!.AsArray();
            valores[1] = valores[0]!.DeepClone();
        });

        resultado.IsFailure.Should().BeTrue(
            "o encoder nunca emite duas entradas para o mesmo valorCodigo dentro do mesmo fato — a " +
            "repetição só é alcançável por adulteração dos bytes");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis[].ordem ausente é recusado")]
    public void ValoresSelecionaveis_OrdemAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"]!.AsArray()[0]!.AsObject().Remove("ordem"));

        resultado.IsFailure.Should().BeTrue(
            "'ordem' fecha a gramática de cada item de valoresSelecionaveis — o encoder sempre a emite, " +
            "e é dela que o formulário público deriva a posição de exibição da opção");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis[].ordem negativa é recusada")]
    public void ValoresSelecionaveis_OrdemNegativa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"]!.AsArray()[0]!["ordem"] = -1);

        resultado.IsFailure.Should().BeTrue(
            "uma ordem negativa não corresponde a posição alguma de exibição — o encoder só emite índices " +
            "canônicos, sempre a partir de zero");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Bicondicional D1-bis: um fato de <b>seleção</b> (SELECAO_UNICA/SELECAO_MULTIPLA) sem
    /// array de valores é um seletor <b>mudo</b> — o candidato veria o campo, mas nenhuma opção
    /// para escolher. É o mesmo modo de falha que <see cref="ObterFormularioRenderizavelQueryHandlerTests"/>
    /// exercita no handler de leitura pública; aqui a guarda é a do próprio DECODER.
    /// </summary>
    [Fact(DisplayName = "Bicondicional: valoresSelecionaveis null num fato de SELEÇÃO é recusado — seletor mudo")]
    public void ValoresSelecionaveis_NuloEmFatoDeSelecao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"] = null);

        resultado.IsFailure.Should().BeTrue(
            "COR_RACA é SELECAO_UNICA — a bicondicional exige array. Restaurar null aqui publicaria um " +
            "campo de seleção sem as opções que o candidato precisa escolher");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>issue #1077 — §4, CA-13: um envelope adulterado com array VAZIO para um fato de seleção é malformado, não um caso legítimo a reidratar.</summary>
    [Fact(DisplayName = "Bicondicional: valoresSelecionaveis com array VAZIO num fato de SELEÇÃO é recusado — cardinalidade mínima 1")]
    public void ValoresSelecionaveis_ArrayVazioEmFatoDeSelecao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue(
            "COR_RACA é SELECAO_UNICA — array vazio é um seletor sem opção nenhuma, tão mudo quanto null. " +
            "A publicação original nunca teria produzido isso: o gate de pré-canonicalização " +
            "(ProcessoSeletivo.PendenciaDeFatoColetadoSemValoresOfertados) recusa antes de canonicalizar, e " +
            "SerializarFatosColetados também lança — só um envelope adulterado chega aqui com [] persistido");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Bicondicional: valoresSelecionaveis com array num fato que NÃO é de seleção é recusado — vocabulário pendurado")]
    public void ValoresSelecionaveis_ArrayEmFatoNaoDeSelecao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["tipoRenderizacao"] = "BOOLEANO");

        resultado.IsFailure.Should().BeTrue(
            "trocar o tipo de renderização para BOOLEANO sem esvaziar 'valoresSelecionaveis' deixa um " +
            "vocabulário de seleção pendurado num campo que a bicondicional proíbe de tê-lo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados ausente é recusado")]
    public void ValoresDominioDeclarados_ChaveAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA").Remove("valoresDominioDeclarados"));

        resultado.IsFailure.Should().BeTrue(
            "a gramática de cada item de metadadosFatos fecha em 'valoresDominioDeclarados' — o encoder " +
            "sempre a emite, presente e explícita, nunca omitida");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados de tipo errado (nem array nem null) é recusado")]
    public void ValoresDominioDeclarados_TipoErrado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"] = "BRANCA");

        resultado.IsFailure.Should().BeTrue(
            "um escalar no lugar do array (ou de null) é forma que o encoder nunca produz para este campo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados[].valorCodigo duplicado é recusado")]
    public void ValoresDominioDeclarados_ValorCodigoDuplicado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
        {
            JsonArray valores = MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"]!.AsArray();
            valores[1] = valores[0]!.DeepClone();
        });

        resultado.IsFailure.Should().BeTrue(
            "o encoder nunca emite duas entradas para o mesmo valorCodigo dentro do metadado do mesmo fato");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados[].ordem ausente é recusado")]
    public void ValoresDominioDeclarados_OrdemAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"]!.AsArray()[0]!.AsObject().Remove("ordem"));

        resultado.IsFailure.Should().BeTrue(
            "'ordem' fecha a gramática de cada item de valoresDominioDeclarados — o encoder sempre a emite");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados[].ordem negativa é recusada")]
    public void ValoresDominioDeclarados_OrdemNegativa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"]!.AsArray()[0]!["ordem"] = -1);

        resultado.IsFailure.Should().BeTrue(
            "uma ordem negativa não corresponde a posição alguma de exibição — o encoder só emite índices " +
            "canônicos, sempre a partir de zero");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── issue #1071 — etapas[].tipoEtapa: snapshot congelado do tipo de etapa ──

    [Fact(DisplayName = "etapas[].tipoEtapa ausente é recusado")]
    public void TipoEtapa_Ausente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!.AsObject().Remove("tipoEtapa"));

        resultado.IsFailure.Should().BeTrue(
            "sem tipoEtapa não há como reidratar a identidade estável que AvaliarEtapaObrigatoria compara");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "etapas[].tipoEtapa com chave obrigatória ausente é recusado")]
    [InlineData("origemId")]
    [InlineData("codigo")]
    [InlineData("nome")]
    [InlineData("admitePontuacao")]
    [InlineData("admiteEliminacao")]
    [InlineData("notaDeOrigemNoEnem")]
    public void TipoEtapa_ChaveAusente_Recusa(string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!.AsObject().Remove(chave));

        resultado.IsFailure.Should().BeTrue(
            $"'{chave}' fecha a gramática de tipoEtapa — o encoder sempre a emite");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa com chave intrusa é recusado")]
    public void TipoEtapa_ChaveIntrusa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["descricao"] = "campo que o encoder nunca emitiu");

        resultado.IsFailure.Should().BeTrue("a gramática de tipoEtapa é fechada — igual a qualquer outro bloco do envelope");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "etapas[].tipoEtapa com sinalizador que não é booleano é recusado")]
    [InlineData("admitePontuacao")]
    [InlineData("admiteEliminacao")]
    [InlineData("notaDeOrigemNoEnem")]
    public void TipoEtapa_SinalizadorNaoBooleano_Recusa(string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]![chave] = "true");

        resultado.IsFailure.Should().BeTrue(
            $"'{chave}' é booleano no encoder; o texto \"true\" não é o mesmo documento");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa que não pontua nem elimina é recusado")]
    public void TipoEtapa_SemCaraterAdmitido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonNode tipoEtapa = envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!;
            tipoEtapa["admitePontuacao"] = false;
            tipoEtapa["admiteEliminacao"] = false;
        });

        resultado.IsFailure.Should().BeTrue(
            "um tipo sem caráter admitido não existe no cadastro — a factory do snapshot o recusa");
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.SemCaraterAdmitido");
    }

    [Fact(DisplayName = "etapas[].tipoEtapa com nota de origem no ENEM que não pontua é recusado")]
    public void TipoEtapa_NotaDeOrigemNoEnemSemPontuacao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonNode tipoEtapa = envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!;
            tipoEtapa["notaDeOrigemNoEnem"] = true;
            tipoEtapa["admitePontuacao"] = false;
            tipoEtapa["admiteEliminacao"] = true;
        });

        resultado.IsFailure.Should().BeTrue(
            "o cadastro não admite tipo com nota do ENEM que não pontue — a factory do snapshot o recusa");
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.NotaDeOrigemNoEnemSemPontuacao");
    }

    [Fact(DisplayName = "etapas[].tipoEtapa.origemId vazio (Guid.Empty) é recusado")]
    public void TipoEtapa_OrigemIdVazio_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["origemId"] = Guid.Empty.ToString());

        resultado.IsFailure.Should().BeTrue(
            "um snapshot sem origem não prova de qual tipo do cadastro a etapa foi copiada");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "etapas[].tipoEtapa.codigo ou nome vazio é recusado")]
    [InlineData("codigo")]
    [InlineData("nome")]
    public void TipoEtapa_CodigoOuNomeVazio_Recusa(string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]![chave] = "");

        resultado.IsFailure.Should().BeTrue(
            $"'{chave}' vazio produziria um snapshot que a própria factory do domínio (TipoEtapaSnapshot.Criar) recusa");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa.codigo acima do limite da coluna (64) é recusado")]
    public void TipoEtapa_CodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["codigo"] = new string('A', 65));

        resultado.IsFailure.Should().BeTrue(
            "um código de 65 caracteres não cabe na coluna tipo_etapa_codigo (varchar 64) — recusar aqui evita " +
            "DbUpdateException (500) no meio do descarte");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa.nome acima do limite da coluna (200) é recusado")]
    public void TipoEtapa_NomeAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["nome"] = new string('A', 201));

        resultado.IsFailure.Should().BeTrue(
            "um nome de 201 caracteres não cabe na coluna tipo_etapa_nome (varchar 200)");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].faseCodigo acima do limite da coluna (60) é recusado")]
    public void Etapa_FaseCodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["faseCodigo"] = new string('A', 61));

        resultado.IsFailure.Should().BeTrue(
            "um código de 61 caracteres não cabe na coluna fase_codigo (varchar 60) — o teto do decodificador " +
            "media 300, então o valor passava aqui para estourar no INSERT, com 500 no meio do descarte");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);

        // EnvelopeMalformado é o código de dezenas de guardas. Sem prender a mensagem ao campo
        // e ao teto, uma guarda futura que recusasse este envelope por outro motivo — o código
        // de fase que não resolve no cronograma, por exemplo — deixaria o teste verde com o
        // limite de volta em 300, que é exatamente a regressão que ele existe para prender.
        resultado.Error.Message.Should().Contain("etapas[0].faseCodigo").And.Contain("60");
    }

    [Theory(DisplayName = "distribuicao[].grupoAreaEnem com código ou rótulo acima do limite da coluna é recusado")]
    [InlineData("codigo", 31, "30")]
    [InlineData("rotulo", 61, "60")]
    public void Distribuicao_GrupoAreaEnemAcimaDoLimite_Recusa(string campo, int tamanho, string limite)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = new() { ["codigo"] = "TECNOLOGICA", ["rotulo"] = "Tecnológica" };
            grupo[campo] = new string('A', tamanho);
            envelope["distribuicao"]!.AsArray()[0]!["grupoAreaEnem"] = grupo;
        });

        resultado.IsFailure.Should().BeTrue(
            $"um {campo} de {tamanho} caracteres não cabe na coluna (varchar {limite}) — recusar aqui evita " +
            "DbUpdateException (500) no meio do descarte");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain($"distribuicao[0].grupoAreaEnem.{campo}").And.Contain(limite);
    }

    [Fact(DisplayName = "distribuicao[].grupoAreaEnem sem o rótulo é recusado")]
    public void Distribuicao_GrupoAreaEnemSemRotulo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["grupoAreaEnem"] = new JsonObject { ["codigo"] = "TECNOLOGICA" });

        resultado.IsFailure.Should().BeTrue("o grupo congelado carrega código e rótulo, os dois");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("distribuicao[0].grupoAreaEnem");
    }

    [Fact(DisplayName = "distribuicao[].grupoAreaEnem como texto solto, no formato anterior, é recusado")]
    public void Distribuicao_GrupoAreaEnemComoTexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["grupoAreaEnem"] = "Tecnológica");

        resultado.IsFailure.Should().BeTrue("o grupo congelado é um objeto com código e rótulo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("distribuicao[0].grupoAreaEnem");
    }

    [Fact(DisplayName = "classificacao com resolução de Pesos por Área e quadro vazio é recusada")]
    public void Classificacao_ResolucaoComQuadroVazio_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue("a resolução declarada sem grupo congelado não teria pesos para a nota");
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio");
    }

    [Fact(DisplayName = "classificacao baseada em ENEM com cálculo local sem a resolução de Pesos por Área é recusada")]
    public void Classificacao_EnemLocalSemResolucao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["resolucaoPesoAreaEnem"] = null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria");
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com grupo repetido é recusado")]
    public void Classificacao_QuadroComGrupoRepetido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray quadro = envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray();
            quadro.Add(quadro[0]!.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido");
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com área repetida no mesmo grupo é recusado")]
    public void Classificacao_QuadroComAreaRepetida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray areas = envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray();
            areas.Add(areas[0]!.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("GrupoPesoAreaEnemCongelado.AreaRepetida");
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com corte acima da nota máxima é recusado")]
    public void Classificacao_QuadroComCorteAcimaDaNotaMaxima_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray()[0]!["corte"] = "1000.0001");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("GrupoPesoAreaEnemCongelado.CorteForaDaFaixa");
    }

    [Theory(DisplayName = "classificacao.quadroPesoAreaEnem com texto acima da coluna é recusado")]
    [InlineData("codigo", 31, "30")]
    [InlineData("rotulo", 101, "100")]
    public void Classificacao_QuadroComTextoDeAreaAcimaDaColuna_Recusa(string campo, int tamanho, string limite)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray()[0]![campo] = new string('A', tamanho));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain($"classificacao.quadroPesoAreaEnem[0].areas[0].{campo}").And.Contain(limite);
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com peso de mais dígitos que a coluna é recusado")]
    public void Classificacao_QuadroComPesoAcimaDaPrecisao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray()[0]!["peso"] = "100.0000");

        resultado.IsFailure.Should().BeTrue("numeric(6,4) comporta até 99,9999 — o excesso só estouraria no SaveChanges");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("numeric(6,4)");
    }

    [Theory(DisplayName = "classificacao com não-caractere na resolução, no grupo, na base legal ou na área é recusada como malformada, sem exceção")]
    [InlineData("resolucao")]
    [InlineData("grupo")]
    [InlineData("baseLegal")]
    [InlineData("area")]
    public void Classificacao_NaoCaractere_RecusaComoMalformada(string onde)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComNaoCaractere(onde switch
        {
            "resolucao" => static (envelope, texto) => envelope["classificacao"]!["resolucaoPesoAreaEnem"] = texto,
            "grupo" => static (envelope, texto) => Quadro(envelope)[0]!["grupoAreaEnem"]!["rotulo"] = texto,
            "baseLegal" => static (envelope, texto) => Quadro(envelope)[0]!["baseLegal"] = texto,
            _ => static (envelope, texto) => Quadro(envelope)[0]!["areas"]!.AsArray()[0]!["rotulo"] = texto,
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);

        static JsonArray Quadro(JsonObject envelope) => envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray();
    }

    /// <summary>
    /// Como <see cref="ReidratarComEnvelopeAdulterado"/>, mas com um não-caractere (U+FFFE) no
    /// texto que <paramref name="adulterar"/> grava. O não-caractere entra direto nos bytes:
    /// o perfil canônico não o serializaria, porque ele não tem forma NFC.
    /// </summary>
    private static Result<EnvelopeReidratado> ReidratarComNaoCaractere(Action<JsonObject, string> adulterar)
    {
        const string Marcador = "MARCADOR-DO-NAO-CARACTERE";
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] originais = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(originais))!.AsObject();
        adulterar(envelope, "Texto" + Marcador);

        string comMarcador = Encoding.UTF8.GetString(PerfilCanonicoV1.Instancia.Serializar(envelope));
        comMarcador.Should().Contain(Marcador, "pré-condição: a adulteração tem de chegar aos bytes");
        byte[] adulterados = Encoding.UTF8.GetBytes(comMarcador.Replace(Marcador, ((char)0xFFFE).ToString(), StringComparison.Ordinal));

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, adulterados);
        return CorpusEnvelope.Registro.Reidratar(versao);
    }

    /// <summary>
    /// O critério de desempate por área do ENEM é o sexto da ordem no processo rico: é o
    /// índice 5 do array canônico, ordenado pela ordem do critério.
    /// </summary>
    private static JsonObject ArgsDoDesempatePorArea(JsonObject envelope)
    {
        JsonObject criterio = envelope["criteriosDesempate"]!.AsArray()[5]!.AsObject();
        criterio["regra"]!["codigo"]!.GetValue<string>().Should().Be(
            CriterioDesempateCodigo.MaiorNotaAreaEnem, "pré-condição: o índice 5 é o desempate por área do ENEM");
        return criterio["args"]!.AsObject();
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas que não é lista é recusado")]
    public void DesempatePorArea_AreasForaDeLista_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"] = "REDACAO");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("criteriosDesempate[5].args");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas com item que não é texto é recusado")]
    public void DesempatePorArea_AreaQueNaoETexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"]!.AsArray().Add(7));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("criteriosDesempate[5].args.areas[3]");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas vazia é recusada pela mesma regra da gravação")]
    public void DesempatePorArea_SemAreas_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreasObrigatorias");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas com área repetida é recusada pela mesma regra da gravação")]
    public void DesempatePorArea_AreaRepetida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray areas = ArgsDoDesempatePorArea(envelope)["areas"]!.AsArray();
            areas.Add(areas[0]!.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreaRepetida");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas com código fora da forma do cadastro é recusado")]
    public void DesempatePorArea_CodigoForaDaForma_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"]!.AsArray()[0] = "redacao");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreaInvalida");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[].args da falta em dia de prova do ENEM com chave intrusa é recusado")]
    public void EliminacaoPorFaltaEmDiaDeProvaEnem_ChaveIntrusaNosArgs_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject falta = Navegar(envelope, "classificacao.regrasEliminacao.3");
            falta["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "pré-condição: o índice 3 é a eliminação por falta em dia de prova");
            falta["args"]!.AsObject()["chaveIntrusa"] = "x";
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("classificacao.regrasEliminacao[3].args");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[] com a falta em dia de prova do ENEM repetida é recusada pela mesma regra da gravação")]
    public void EliminacaoPorFaltaEmDiaDeProvaEnem_Repetida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray regras = Navegar(envelope, "classificacao")["regrasEliminacao"]!.AsArray();
            JsonNode falta = regras[3]!;
            falta["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "pré-condição: o índice 3 é a eliminação por falta em dia de prova");
            regras.Insert(3, falta.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[] com corte em área fora do quadro é recusado pela mesma regra da gravação")]
    public void CorteEmArea_ForaDoQuadro_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject corte = Navegar(envelope, "classificacao.regrasEliminacao.0");
            corte["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimCorteEmArea, "pré-condição: o índice 0 é o corte em área");
            corte["args"]!["areaCodigo"] = "FISICA";
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[] com dois cortes na mesma área é recusado pela mesma regra da gravação")]
    public void CorteEmArea_Repetido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray regras = Navegar(envelope, "classificacao")["regrasEliminacao"]!.AsArray();
            JsonNode corte = regras[0]!;
            corte["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimCorteEmArea, "pré-condição: o índice 0 é o corte em área");
            JsonNode outro = corte.DeepClone();
            outro["args"]!["minimo"] = "500.0000";
            regras.Insert(1, outro);
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.CorteEmAreaRepetido");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[].args do corte em área sem a área é recusado")]
    public void CorteEmArea_SemArea_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject corte = Navegar(envelope, "classificacao.regrasEliminacao.0");
            corte["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimCorteEmArea, "pré-condição: o índice 0 é o corte em área");
            corte["args"]!.AsObject().Remove("areaCodigo");
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("classificacao.regrasEliminacao[0].args");
    }

    [Fact(DisplayName = "etapas[].produtos[].atoCodigo acima do limite da coluna (60) é recusado")]
    public void Etapa_ProdutoAtoCodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["produtos"]!.AsArray()[0]!["atoCodigo"] = new string('A', 61));

        resultado.IsFailure.Should().BeTrue(
            "um código de 61 caracteres não cabe na coluna ato_codigo de produtos_da_etapa (varchar 60)");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("etapas[0].produtos[0].atoCodigo").And.Contain("60");
    }

    [Fact(DisplayName = "etapas[].bancas[].codigo acima do limite da coluna (60) é recusado")]
    public void Etapa_BancaCodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["bancas"]!.AsArray()[0]!["codigo"] = new string('A', 61));

        resultado.IsFailure.Should().BeTrue(
            "um código de 61 caracteres não cabe na coluna codigo de bancas_da_etapa (varchar 60)");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("etapas[0].bancas[0].codigo").And.Contain("60");
    }

    [Fact(DisplayName = "etapas[].recursos[].ancora com o sentinela 'Nenhuma' é recusada")]
    public void Etapa_RecursoSemAncora_Recusa()
    {
        // Adultera a janela que corre da CIÊNCIA, e não a que corre do ato, porque é ela que
        // escapa por inteiro: DefinirRecursos classifica tudo que não é ato publicado como
        // "ciencia" ao procurar janelas duplicadas, então trocar a de ciência por 'Nenhuma'
        // mantém o balde ocupado uma vez só e não colide com nada. A guarda de ciência —
        // "exige que a etapa prometa parecer individual" — também deixa de valer, porque só
        // olha CienciaIndividual. Nenhuma outra checagem sobra.
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()
                .SelectMany(etapa => etapa!["recursos"]!.AsArray())
                .First(recurso => recurso!["ancora"]!.GetValue<string>() == nameof(AncoraDoRecurso.CienciaIndividual))!
                ["ancora"] = nameof(AncoraDoRecurso.Nenhuma));

        resultado.IsFailure.Should().BeTrue(
            "'Nenhuma' é a ausência declarada que RecursoDaEtapa.Criar recusa e que Reidratar não reconfere; " +
            "sem a guarda do decodificador o valor atravessa DefinirRecursos, persiste como ancora = 0, e a " +
            "reemissão dá os mesmos bytes — o certame volta com uma janela recursal cujo prazo não corre de " +
            "instante nenhum, e o round-trip aprova porque os bytes conferem");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("ancora");
    }

    // ── Infraestrutura dos testes ──

    // ── Coleta de fatos / derivação / grafo conjunto (Story #928, §7.4) ──

    [Fact(DisplayName = "Versão do interpretador de derivação desconhecida é recusada")]
    public void VersaoInterpretadorDesconhecida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => envelope["versaoInterpretador"] = "999");

        resultado.IsFailure.Should().BeTrue(
            "um snapshot resolvido por uma semântica de motor que este sistema não conhece não pode ser " +
            "reidratado como se fosse íntegro");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Grafo conjunto congelado que não reproduz o recomputado é recusado (testemunho)")]
    public void GrafoTestemunhoDivergente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            // Tira uma aresta do grafo congelado: o grafo recomputado das partes reidratadas ainda a
            // terá, então o testemunho não fecha — o congelado deixou de ser cópia verificável.
            JsonArray arestas = envelope["grafoDependencia"]!["arestas"]!.AsArray();
            arestas.RemoveAt(arestas.Count - 1);
        });

        resultado.IsFailure.Should().BeTrue(
            "o grafo congelado é testemunho redundante — se ele diverge do recomputado, a evidência não prova o que diz");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Grafo conjunto congelado como null é recusado, não estoura em 500")]
    public void GrafoNulo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => envelope["grafoDependencia"] = null);

        resultado.IsFailure.Should().BeTrue(
            "uma coluna adulterada com `grafoDependencia: null` e hash recomputado tem de recusar como malformada, " +
            "nunca produzir NullReferenceException");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "DNF vazio numa pré-condição ([] ou cláusula vazia [[]]) é recusado — o encoder emite null, nunca vazio")]
    [InlineData(false)]
    [InlineData(true)]
    public void DnfVazio_Recusa(bool clausulaVazia)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            // RENDA é o fato com pré-condição; troca-a por um DNF vazio, que o decoder colapsaria para
            // "sem pré-condição" — uma forma que o encoder jamais emite (a ausência de condição é null).
            JsonObject renda = envelope["fatosColetados"]!.AsArray()
                .Single(fato => fato!["fatoCodigo"]!.GetValue<string>() == "RENDA")!.AsObject();
            renda["precondicao"] = clausulaVazia ? new JsonArray(new JsonArray()) : new JsonArray();
        });

        resultado.IsFailure.Should().BeTrue(
            "um DNF vazio colapsaria para 'sem pré-condição' e a restauração o recanonicalizaria como null, " +
            "aceitando uma forma que o certame nunca congelou");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Código contribuído por MODALIDADE fora do domínio de modalidades ofertadas é recusado")]
    public void ContribuiForaDoDominioDeModalidades_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            // A regra âncora de MODALIDADE contribui um código que o processo não oferta — o conjunto
            // de modalidades ofertadas congelado prova o domínio, mas não que cada contribui caiba nele.
            JsonObject modalidade = envelope["regrasDerivacao"]!.AsArray()
                .Single(no => no!["codigoFato"]!.GetValue<string>() == "MODALIDADE")!.AsObject();
            modalidade["regras"]!.AsArray()[0]!["contribui"] = "CODIGO_NAO_OFERTADO";
        });

        resultado.IsFailure.Should().BeTrue(
            "uma regra que contribui um código fora do domínio de modalidades ofertadas resolveria um valor " +
            "impossível — a reconstrução do VO da regra contra o domínio congelado recusa");
    }

    // ── Taxa de inscrição e isenção (issue #1112) — mesmo raciocínio de forma fechada e
    // vocabulário fechado de LerDivulgacao, aplicado ao bloco taxaInscricao ──

    [Fact(DisplayName = "issue #1112: taxaInscricao.presente=false é recusado — CA-01 garante que todo envelope publicado declarou taxa")]
    public void TaxaInscricao_PresenteFalso_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject bloco = envelope["taxaInscricao"]!.AsObject();
            bloco.Clear();
            bloco["presente"] = false;
        });

        resultado.IsFailure.Should().BeTrue(
            "um envelope legitimamente publicado nunca congela 'presente:false' — CA-01 recusa Publicar() " +
            "sem taxa declarada, então só bytes adulterados chegam ao decoder nesse estado");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("taxaInscricao.presente");
    }

    [Fact(DisplayName = "issue #1112: fundamento de isenção com token fora do vocabulário conhecido é recusado")]
    public void TaxaInscricao_FundamentoDesconhecido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray(JsonValue.Create("FUNDAMENTO_INEXISTENTE")));

        resultado.IsFailure.Should().BeTrue(
            "'FUNDAMENTO_INEXISTENTE' não pertence ao vocabulário de FundamentoIsencaoCodigo — a factory recusa, " +
            "nunca ignora em silêncio");
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoDesconhecido");
    }

    [Fact(DisplayName = "issue #1112: fundamentos repetidos são recusados — o encoder nunca emite duplicata")]
    public void TaxaInscricao_FundamentosDuplicados_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray(
                JsonValue.Create(FundamentoIsencaoCodigo.CadastroUnico), JsonValue.Create(FundamentoIsencaoCodigo.CadastroUnico)));

        resultado.IsFailure.Should().BeTrue(
            "ConfiguracaoTaxaInscricao.Criar deduplica antes de guardar — o encoder nunca emite token repetido; " +
            "achar um é sinal de bytes que não vieram desse caminho");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("taxaInscricao.fundamentos");
    }

    [Fact(DisplayName = "issue #1112: fundamentos fora da ordem canônica são recusados — o decoder não reordena")]
    public void TaxaInscricao_FundamentosForaDeOrdem_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray(
                JsonValue.Create(FundamentoIsencaoCodigo.DoacaoMedulaOssea), JsonValue.Create(FundamentoIsencaoCodigo.CadastroUnico)));

        resultado.IsFailure.Should().BeTrue(
            "'fundamentos' sai do encoder na ordem canônica (código ordinal) porque a entidade já os guarda " +
            "ordenados — fora de ordem é malformado, não uma forma alternativa a reordenar em silêncio");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("taxaInscricao.fundamentos");
    }

    [Fact(DisplayName = "issue #1319: envelope que ainda traz confirmacaoFundamentos é recusado — o bloco é de forma fechada")]
    public void TaxaInscricao_ChaveConfirmacaoFundamentosRemovida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["confirmacaoFundamentos"] = false);

        resultado.IsFailure.Should().BeTrue(
            "a confirmação saiu do agregado e da emissão — chave a mais no bloco é malformado, " +
            "não campo tolerado por compatibilidade");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("confirmacaoFundamentos",
            "a recusa nomeia a chave intrusa — sem isso o teste passaria por qualquer defeito " +
            "de forma no bloco, inclusive um alheio à remoção");
    }

    [Fact(DisplayName = "issue #1310: envelope que cobra taxa sem nenhum fundamento é recusado — o decoder é tão estrito quanto a escrita")]
    public void TaxaInscricao_CobraSemFundamento_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue(
            "ConfiguracaoTaxaInscricao.Criar exige fundamento de quem cobra — o encoder nunca " +
            "emite 'cobra:true' com 'fundamentos:[]', e o decoder não pode aceitar o que a escrita recusa");
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra");
    }

    // ── Cidade da Unidade administradora (issue #1114) — bicondicional all-or-nothing ──

    [Fact(DisplayName = "issue #1114: cidade parcial (só código IBGE) em identidadesUnidade.administradora é recusada")]
    public void IdentidadesUnidade_CidadeParcialSoCodigo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["identidadesUnidade"]!["administradora"]!["cidadeNome"] = null);

        resultado.IsFailure.Should().BeTrue(
            "UnidadeAdministradoraSnapshot.Criar guarda o trio de cidade completo ou nenhum — o encoder nunca " +
            "emite código IBGE sem nome/UF");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("identidadesUnidade.administradora");
    }

    [Fact(DisplayName = "issue #1114: cidade parcial (só UF) em identidadesUnidade.administradora é recusada")]
    public void IdentidadesUnidade_CidadeParcialSoUf_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            envelope["identidadesUnidade"]!["administradora"]!["cidadeCodigoIbge"] = null;
            envelope["identidadesUnidade"]!["administradora"]!["cidadeNome"] = null;
        });

        resultado.IsFailure.Should().BeTrue(
            "trio parcial (só UF) é o mesmo tipo de bytes que não vêm do encoder — recusa como malformado");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "issue #1114: código IBGE em formato inválido em identidadesUnidade.administradora é recusado")]
    public void IdentidadesUnidade_CidadeCodigoIbgeFormatoInvalido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["identidadesUnidade"]!["administradora"]!["cidadeCodigoIbge"] = "ABCDEFG");

        resultado.IsFailure.Should().BeTrue(
            "o código IBGE tem de ter 7 dígitos numéricos — ReferenciaCidadeGeo.Validar recusa formato inválido");
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "issue #1114: UF incoerente com o prefixo do código IBGE em identidadesUnidade.administradora é recusada")]
    public void IdentidadesUnidade_CidadeUfIncoerente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["identidadesUnidade"]!["administradora"]!["cidadeUf"] = "SP");

        resultado.IsFailure.Should().BeTrue(
            "o prefixo '15' do código IBGE de Marabá corresponde a PA, incoerente com SP");
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    private static ProcessoSeletivo ProcessoPublicado()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        CorpusEnvelope.Publicar(processo);
        return processo;
    }

    private static (VersaoConfiguracao Versao, byte[] Bytes) VersaoRica()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;
        return (CorpusEnvelope.VersaoDeAbertura(processo, bytes), bytes);
    }

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

    [Fact(DisplayName = "Âncora do recurso apontando para produto de OUTRA fase: recusa nomeada, não exceção da fábrica")]
    public void RegraRecursoAncoradaEmProdutoDeOutraFase_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray fases = envelope["cronogramaFases"]!["fases"]!.AsArray();
            JsonObject comRecurso = fases.Select(f => f!.AsObject()).Single(f => f["regraRecurso"] is JsonObject);
            JsonObject outra = fases.Select(f => f!.AsObject())
                .First(f => f["regraRecurso"] is not JsonObject && f["produtos"]!.AsArray().Count > 0);

            comRecurso["regraRecurso"]!["produtoAncoraId"] =
                outra["produtos"]!.AsArray()[0]!["id"]!.GetValue<string>();
        });

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

    // ── O vínculo da exigência documental com a fase e a etapa que a coletam ──

    /// <summary>
    /// Onde o documento é coletado é dito por dois campos que só significam alguma coisa
    /// contra o cronograma e as etapas repostas ao lado deles.
    /// </summary>
    /// <remarks>
    /// A escrita confere os três vínculos antes de aceitar a exigência; a reidratação aceitava
    /// o que o documento dissesse. Uma exigência apontando para fase ou etapa que não existe
    /// no processo reposto atravessa a leitura e morre na chave estrangeira, como 500 no meio
    /// do descarte — e a que aponta para etapa de <b>outra</b> fase nem isso: persiste, e o
    /// certame restaurado passa a coletar o documento num dia que o cronograma não prevê.
    /// </remarks>
    [Fact(DisplayName = "Exigência apontando para fase fora do cronograma é recusada na reidratação")]
    public void ExigenciaComFaseForaDoCronograma_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            PrimeiraExigencia(envelope)["exigidoNaFaseId"] = Guid.CreateVersion7().ToString());

        resultado.IsFailure.Should().BeTrue(
            "a fase que coleta o documento tem de ser uma fase deste cronograma");
        resultado.Error!.Code.Should().Be("DocumentoExigido.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "Exigência apontando para etapa que o processo não tem é recusada na reidratação")]
    public void ExigenciaComEtapaForaDoProcesso_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            PrimeiraExigencia(envelope)["exigidoNaEtapaId"] = Guid.CreateVersion7().ToString());

        resultado.IsFailure.Should().BeTrue(
            "a etapa que coleta o documento tem de existir no processo reposto — senão a exigência restaurada " +
            "aponta para o nada, e quem descobre é a chave estrangeira");
        resultado.Error!.Code.Should().Be("DocumentoExigido.EtapaNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "Exigência apontando para etapa que não acontece na fase declarada é recusada na reidratação")]
    public void ExigenciaComEtapaDeOutraFase_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
        {
            // Uma etapa real do processo, mas que não declara a fase em que a exigência diz
            // ser coletada: é o caso que passa pela chave estrangeira e sobrevive calado.
            string etapaId = envelope["etapas"]!.AsArray()[0]!["id"]!.GetValue<string>();
            PrimeiraExigencia(envelope)["exigidoNaEtapaId"] = etapaId;
        });

        resultado.IsFailure.Should().BeTrue(
            "apontar etapa de outra fase diria que a habilitação coleta no dia da prova");
        resultado.Error!.Code.Should().Be("DocumentoExigido.EtapaNaoPertenceAFase");
    }

    private static JsonObject PrimeiraExigencia(JsonObject envelope) =>
        envelope["documentosExigidos"]!["exigencias"]!.AsArray()[0]!.AsObject();

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
    /// Já aconteceu neste codec — um envelope publicado sob PSIQ ou sob a regra que hoje é
    /// COM-PCD-PURO era irreidratável porque o rol do decodificador estava fechado em dois
    /// literais. A configuração congelada é a evidência jurídica do certame; recusá-la deixa o
    /// descarte da retificação sem como repor o estado anterior.
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

    /// <summary>
    /// Adultera o envelope e <b>recomputa o hash</b> — é o cenário mais exigente: quem
    /// adultera controla a linha inteira. O que sobra para recusar são a canonicidade e a
    /// gramática, e é isso que estes testes exercitam.
    /// </summary>
    private static Result<EnvelopeReidratado> ReidratarComEnvelopeAdulterado(Action<JsonObject> adulterar)
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] originais = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(originais))!.AsObject();
        adulterar(envelope);

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(envelope);
        adulterados.Should().NotEqual(originais, "pré-condição: a adulteração tem de mudar os bytes");

        // A versão é reconstruída SOBRE os bytes adulterados — o hash bate, e o gate de
        // integridade não é o que os recusa. É a gramática.
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, adulterados);
        return CorpusEnvelope.Registro.Reidratar(versao);
    }

    /// <summary>
    /// Mesmo raciocínio de <see cref="ReidratarComEnvelopeAdulterado"/>, mas sobre o processo de
    /// referência da golden fixture (<see cref="EnvelopeCanonicoGoldenTests.ProcessoDeReferencia"/>) —
    /// é ele, não <see cref="CorpusEnvelope.ProcessoRico"/>, quem tem <c>documentosExigidos</c>
    /// populado, e por isso <c>metadadosFatos[].valoresDominioDeclarados</c> só existe aqui.
    /// </summary>
    private static Result<EnvelopeReidratado> ReidratarComEnvelopeDeReferenciaAdulterado(Action<JsonObject> adulterar)
    {
        ProcessoSeletivo processo = EnvelopeCanonicoGoldenTests.ProcessoDeReferencia();
        DadosEdital dados = EnvelopeCanonicoGoldenTests.DadosDeReferencia();
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, EnvelopeCanonicoGoldenTests.HashFixo, FusoInstitucional.ZoneId,
            MetadadosFatosCongelados: EnvelopeCanonicoGoldenTests.MetadadosFatosDeReferencia(),
            ValoresSelecionaveisCongelados: EnvelopeCanonicoGoldenTests.ValoresSelecionaveisDeReferencia()));

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(congelado.Bytes))!.AsObject();
        adulterar(envelope);

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(envelope);
        adulterados.Should().NotEqual(congelado.Bytes, "pré-condição: a adulteração tem de mudar os bytes");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, adulterados, congelado.SchemaVersion, congelado.AlgoritmoHash,
            EnvelopeCanonicoGoldenTests.HashFixo, CorpusEnvelope.Ator, TimeProvider.System, CorpusEnvelope.ContextoRico());
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.ClearDomainEvents();

        return CorpusEnvelope.Registro.Reidratar(publicacao.Value!);
    }

    private static JsonObject FatoColetadoPorCodigo(JsonObject envelope, string codigo) =>
        envelope["fatosColetados"]!.AsArray()
            .Single(fato => fato!["fatoCodigo"]!.GetValue<string>() == codigo)!.AsObject();

    private static JsonObject MetadadoFatoPorCodigo(JsonObject envelope, string codigo) =>
        envelope["documentosExigidos"]!["metadadosFatos"]!.AsArray()
            .Single(metadado => metadado!["fatoCodigo"]!.GetValue<string>() == codigo)!.AsObject();

    private static JsonObject Navegar(JsonObject raiz, string caminho)
    {
        JsonNode atual = raiz;
        foreach (string parte in caminho.Split('.'))
        {
            atual = int.TryParse(parte, System.Globalization.CultureInfo.InvariantCulture, out int indice)
                ? atual.AsArray()[indice]!
                : atual.AsObject()[parte]!;
        }

        return atual.AsObject();
    }

    private static VersaoConfiguracao VersaoComSchema(string schemaVersion)
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

        return VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            schemaVersion,
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            atoCriadorHash: CorpusEnvelope.HashDocumento,
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);
    }

    /// <summary>
    /// Troca os bytes preservando <b>o hash original</b> — é o único jeito de simular a
    /// coluna adulterada sem que a linha forense acompanhe.
    /// </summary>
    private static VersaoConfiguracao ComBytes(VersaoConfiguracao original, byte[] bytes)
    {
        VersaoConfiguracao comprometida = (VersaoConfiguracao)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(VersaoConfiguracao));

        foreach (System.Reflection.PropertyInfo propriedade in typeof(VersaoConfiguracao).GetProperties())
        {
            propriedade.SetValue(comprometida, propriedade.GetValue(original));
        }

        typeof(VersaoConfiguracao)
            .GetProperty(nameof(VersaoConfiguracao.ConfiguracaoCongeladaCanonica))!
            .SetValue(comprometida, bytes);

        return comprometida;
    }
}
