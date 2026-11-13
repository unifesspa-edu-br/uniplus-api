namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// O vocabulário fechado das regras, dos códigos e dos domínios do cadastro que as factories do domínio não
/// fecham, e os três blocos derivados ('distribuicao', 'modalidades', 'ofertas'), que têm de fechar entre si.
/// </summary>
public sealed class EnvelopeCodecRecusaVocabularioEBlocosDerivadosTests
{
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
    [InlineData("classificacao.regraOrdemAlocacao", "ALOCACAO-OPCOES-RN04")]
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
    // Uma ação afirmativa que não remaneja para lugar nenhum, ou que segue a cascata da lei.
    [InlineData("AcaoAfirmativa", "Nenhuma")]
    [InlineData("AcaoAfirmativa", "SegueCascata")]
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
    /// A modalidade incondicional sai com a regra nula. A lista vazia de cláusulas é outra forma,
    /// que o encoder nunca produz: aceitá-la reidrataria em silêncio um snapshot que o certame não
    /// congelou.
    /// </summary>
    [Fact(DisplayName = "Regra de aplicabilidade gravada como lista vazia é recusada")]
    public void RegraDeAplicabilidadeVazia_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["modalidades"]!.AsArray()[1]!["regraDeAplicabilidade"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue("a ausência de condição é nula, nunca uma lista vazia");
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
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(RemoverModalidadesDaPrimeiraOferta);

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
        {
            JsonArray ofertas = envelope["ofertas"]!.AsArray();
            JsonObject extra = ofertas[0]!.DeepClone().AsObject();
            extra["ofertaCursoOrigemId"] = "44440000-0000-4000-8000-000000000009";
            ofertas.Add(extra);
        });

        AssertIncoerencia(resultado);
    }

    [Fact(DisplayName = "Oferta DUPLICADA em 'ofertas' é recusada")]
    public void OfertaDuplicadaNoBloco_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray ofertas = envelope["ofertas"]!.AsArray();
            ofertas.Add(ofertas[0]!.DeepClone());
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

    private static void RemoverModalidadesDaPrimeiraOferta(JsonObject envelope)
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
    }

    private static void AssertIncoerencia(Result<EnvelopeReidratado> resultado)
    {
        resultado.IsFailure.Should().BeTrue(
            "'distribuicao', 'modalidades' e 'ofertas' derivam da MESMA coleção (ADR-0110 D8). Recombiná-los em " +
            "silêncio quando não fecham reconstruiria um agregado que nunca existiu.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.BlocosDerivadosIncoerentes);
    }
}
