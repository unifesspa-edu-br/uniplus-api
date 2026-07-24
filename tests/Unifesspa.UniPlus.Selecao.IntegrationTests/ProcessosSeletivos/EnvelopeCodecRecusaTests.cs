namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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
            v1, PerfilCanonicoV1.Instancia.Serializar(envelope));

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
    /// adulterado poderia. Sem <c>EnvelopeCodecV14.IndexarExigenciasPorId</c>, o
    /// <c>ToDictionary</c> ingênuo lançaria <see cref="ArgumentException"/> (500 não tratado
    /// no meio de uma restauração) em vez de recusar com um <see cref="DomainError"/> nomeado.
    /// </summary>
    [Fact(DisplayName = "Story #923: exigenciaId duplicado em documentosExigidos.exigencias é recusado, não lança")]
    public void ExigenciaIdDuplicado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Exigência Duplicada", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
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
            nOpcoesAlocacao: 1, regrasEliminacao: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: true, permiteComplementacao: true, produzResultado: true, resultadoDefinitivo: true,
            coletaInscricao: true, inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), atoProduzidoCodigo: "INSCRICAO",
            atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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
            "088/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, hashDocumento));

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(congelado.Bytes))!.AsObject();
        JsonArray exigencias = envelope["documentosExigidos"]!["exigencias"]!.AsArray();
        exigencias.Should().HaveCount(2, "pré-condição: duas exigências para duplicar o id de uma na outra");
        exigencias[1]!["exigenciaId"] = exigencias[0]!["exigenciaId"]!.DeepClone();

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(envelope);
        adulterados.Should().NotEqual(congelado.Bytes, "pré-condição: a adulteração tem de mudar os bytes");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, adulterados, congelado.SchemaVersion, congelado.AlgoritmoHash, hashDocumento, "testes", TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);

        Result<EnvelopeReidratado> resultado = new RegistroCodecsEnvelope().Reidratar(publicacao.Value!);

        resultado.IsFailure.Should().BeTrue(
            "exigenciaId duplicado só é alcançável por adulteração — um encoder real nunca o produz — e a " +
            "restauração precisa recusar nomeadamente, não lançar ArgumentException do ToDictionary ingênuo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
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

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(envelope);
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
