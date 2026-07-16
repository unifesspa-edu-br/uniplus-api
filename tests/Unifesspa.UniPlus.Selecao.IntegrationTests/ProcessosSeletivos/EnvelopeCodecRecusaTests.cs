namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// <b>O que a reidratação recusa</b> — e por quê (Story #859: CA-04, CA-05, CA-06;
/// ADR-0110 D1/D8).
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
    // ── CA-05 / CA-04 — versão desconhecida e versão conhecida-não-reidratável ──

    [Fact(DisplayName = "CA-05 — versão fora do registro: recusa nomeada, não tentativa de leitura")]
    public void VersaoDesconhecida_Recusa()
    {
        VersaoConfiguracao versao = VersaoComSchema("9.9");

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);
    }

    [Fact(DisplayName = "CA-04 — a versão 1.0 é conhecida e RECUSADA: ela podia congelar classificação como stub")]
    public void Versao10_RecusaComMotivo()
    {
        VersaoConfiguracao versao = VersaoComSchema("1.0");

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoNaoReidratavel);
        resultado.Error.Message.Should().Contain("nao_construido",
            "a recusa da 1.0 é NOMEADA — ela podia trazer 'atendimento'/'classificacao' como stub (o fallback que a " +
            "D8 da ADR-0109 matou), e reidratar parcialmente reconstruiria um certame sem a dimensão que determina " +
            "o resultado dele");
    }

    /// <summary>
    /// A <c>1.0</c> e a <c>9.9</c> são ambas recusadas — mas por motivos <b>diferentes</b>,
    /// e o operador diante de um descarte que falhou precisa saber qual dos dois é.
    /// </summary>
    [Fact(DisplayName = "CA-04/CA-05 — 'conhecida e recusada' e 'desconhecida' são erros DISTINTOS")]
    public void RecusasSaoDistinguiveis()
    {
        CorpusEnvelope.Registro.Reidratar(VersaoComSchema("1.0")).Error!.Code
            .Should().NotBe(CorpusEnvelope.Registro.Reidratar(VersaoComSchema("9.9")).Error!.Code);
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
        byte[] bytesAdulterados = HashCanonicalComputer.ComputeSnapshotBytes(adulterado);

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
    [InlineData("classificacao.regrasEliminacao.0")]
    [InlineData("classificacao.regrasEliminacao.0.args")]
    [InlineData("classificacao.regrasEliminacao.0.regra")]
    [InlineData("classificacao.regraCalculo")]
    [InlineData("atendimento.condicoes.0")]
    [InlineData("atendimento.recursos.0")]
    [InlineData("atendimento.tiposDeficiencia.0")]
    [InlineData("bonusRegional.regra")]
    [InlineData("vagas.0")]
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
            bonus.Remove("municipioConvenio");
            bonus.Remove("baseLegal");
            // Sobra `fator` — a forma "ausente" só admite `presente`.
        });

        resultado.IsFailure.Should().BeTrue(
            "um bônus 'ausente' que ainda carrega args é envelope contraditório. Lê-lo como 'sem bônus' descartaria " +
            "o bônus regional (RN05) de um certame publicado, e ninguém veria.");
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
            v1, HashCanonicalComputer.ComputeSnapshotBytes(envelope));

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(v2);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Stub que virou objeto rico é forma NOVA — e forma nova é bump de versão, não leitura tolerante")]
    [InlineData("vagas")]
    [InlineData("documentosExigidos")]
    [InlineData("cascataRemanejamento")]
    public void StubViraObjeto_Recusa(string stub)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope[stub] = new JsonObject { ["status"] = "construido", ["itens"] = new JsonArray() });

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

    // ── CA-06 / D8 — os três blocos derivados têm de fechar ──

    [Fact(DisplayName = "CA-06 — modalidade cuja oferta não existe em 'distribuicao' é recusada")]
    public void ModalidadeSemDistribuicao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["modalidades"]!.AsArray()[0]!["ofertaCursoOrigemId"] = "44440000-0000-4000-8000-000000000009");

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "CA-06 — distribuição sem nenhuma modalidade é recusada")]
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

    [Fact(DisplayName = "CA-06 — oferta ausente do bloco 'ofertas' é recusada")]
    public void OfertaAusenteDoBloco_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray ofertas = envelope["ofertas"]!.AsArray();
            ofertas.RemoveAt(0);
        });

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "CA-06 — oferta EXTRA no bloco 'ofertas' é recusada (a igualdade é de conjuntos, não inclusão)")]
    public void OfertaExtraNoBloco_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["ofertas"]!.AsArray().Add("44440000-0000-4000-8000-000000000009"));

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "CA-06 — oferta DUPLICADA em 'ofertas' é recusada")]
    public void OfertaDuplicadaNoBloco_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray ofertas = envelope["ofertas"]!.AsArray();
            ofertas.Add(ofertas[0]!.GetValue<string>());
        });

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "CA-06 — oferta DUPLICADA em 'distribuicao' é recusada")]
    public void OfertaDuplicadaEmDistribuicao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray distribuicao = envelope["distribuicao"]!.AsArray();
            distribuicao.Add(distribuicao[0]!.DeepClone());
        });

        AssertIncoerencia(resultado);
    }

    private static void AssertIncoerencia(Result<EnvelopeReidratado> resultado)
    {
        resultado.IsFailure.Should().BeTrue(
            "'distribuicao', 'modalidades' e 'ofertas' derivam da MESMA coleção (ADR-0110 D8). Recombiná-los em " +
            "silêncio quando não fecham reconstruiria um agregado que nunca existiu.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.BlocosDerivadosIncoerentes);
    }

    // ── Infraestrutura dos testes ──

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

        byte[] adulterados = HashCanonicalComputer.ComputeSnapshotBytes(envelope);
        adulterados.Should().NotEqual(originais, "pré-condição: a adulteração tem de mudar os bytes");

        // A versão é reconstruída SOBRE os bytes adulterados — o hash bate, e o gate de
        // integridade não é o que os recusa. É a gramática.
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, adulterados);
        return CorpusEnvelope.Registro.Reidratar(versao);
    }

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
