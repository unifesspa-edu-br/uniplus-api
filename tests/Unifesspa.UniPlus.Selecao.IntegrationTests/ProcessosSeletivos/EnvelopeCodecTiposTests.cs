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
/// A leitura é <b>total</b>: todo valor é recusado antes de ser usado, nunca depois
/// (Story #859).
/// </summary>
/// <remarks>
/// <para>
/// Um envelope pode ter <b>todas</b> as chaves certas e ainda assim ser lixo: um array
/// onde deveria haver objeto, um número onde deveria haver texto, um inteiro que não cabe
/// em <c>Int32</c>, uma string vazia num campo que a factory do domínio exige não-branco.
/// </para>
/// <para>
/// A string vazia é o caso mais traiçoeiro. <c>EtapaProcesso.Reidratar</c>,
/// <c>OfertaCondicao.Criar</c> e as irmãs validam com
/// <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> — elas <b>lançam</b>, não devolvem
/// <c>Result</c>. Sem a guarda de não-vazio no leitor, um <c>"nome": ""</c> atravessaria a
/// leitura e explodiria dentro da factory: <b>exceção não tratada no meio de um
/// descarte</b>, que sai como 500 em vez de recusa nomeada.
/// </para>
/// </remarks>
public sealed class EnvelopeCodecTiposTests
{
    [Theory(DisplayName = "Tipo JSON errado é recusado — nunca lido como se fosse o tipo certo")]
    [InlineData("etapas", "objeto onde deveria haver array")]
    [InlineData("criteriosDesempate", "objeto onde deveria haver array")]
    [InlineData("atendimento", "array onde deveria haver objeto")]
    [InlineData("bonusRegional", "array onde deveria haver objeto")]
    public void TipoErrado_Recusa(string bloco, string _)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope[bloco] = envelope[bloco] is JsonArray ? new JsonObject() : new JsonArray());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Número onde deveria haver texto é recusado")]
    public void NumeroNoLugarDeTexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["nome"] = 42);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Texto onde deveria haver inteiro é recusado")]
    public void TextoNoLugarDeInteiro_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["voBase"] = "sessenta");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Inteiro fora de Int32 é recusado — não silenciosamente truncado")]
    public void InteiroForaDeFaixa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["voBase"] = 9_999_999_999L);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Número fracionário onde deveria haver inteiro é recusado")]
    public void FracionarioNoLugarDeInteiro_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["classificacao"]!["nOpcoesAlocacao"] = 1.5);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// O decimal é escrito como <b>string com escala declarada</b> (ADR-0100 item 2).
    /// Aceitar <c>"1.0"</c> onde o encoder escreve <c>"1.0000"</c> deixaria a escala do
    /// envelope à mercê de quem o escrevesse por fora — e a nota de corte de um certame é
    /// uma dessas escalas.
    /// </summary>
    [Theory(DisplayName = "Decimal fora da escala canônica é recusado")]
    [InlineData("3.5")]
    [InlineData("3.50000")]
    [InlineData("3,5000")]
    public void DecimalForaDaEscala_Recusa(string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["peso"] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Decimal como NÚMERO JSON (e não string) é recusado")]
    public void DecimalComoNumero_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["peso"] = 3.5);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Sem a guarda de não-vazio, isto <b>lançaria</b> dentro de
    /// <c>EtapaProcesso.Reidratar</c> — 500 no meio de um descarte, em vez de recusa.
    /// </summary>
    [Theory(DisplayName = "Texto vazio onde o caminho de escrita exige não-branco é RECUSADO")]
    [InlineData("etapas.0.nome")]
    [InlineData("atendimento.condicoes.0.condicaoCodigo")]
    [InlineData("atendimento.condicoes.0.condicaoNome")]
    [InlineData("atendimento.recursos.0.recursoNome")]
    [InlineData("atendimento.tiposDeficiencia.0.tipoDeficienciaNome")]
    [InlineData("modalidades.0.codigo")]
    [InlineData("modalidades.0.baseLegal")]
    [InlineData("distribuicao.0.referenciaDemografica.censoReferencia")]
    [InlineData("distribuicao.0.referenciaDemografica.baseLegal")]
    [InlineData("distribuicao.0.regraDistribuicao.versao")]
    // As duas primeiras do predicado de fato (fato/operador) passam por leitor.TextoNaoVazio,
    // que recusa em branco ANTES de chegar a CondicaoDnf.Criar — mesma família de erro
    // (EnvelopeMalformado) dos demais campos deste teorema.
    [InlineData("criteriosDesempate.3.args.fato")]
    [InlineData("criteriosDesempate.3.args.operador")]
    public void TextoVazio_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope => Definir(envelope, caminho, "   "));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' é exigido não-branco pelo caminho de escrita, mas o domínio não o valida — e o encoder " +
            "reemite a string vazia tal qual, de modo que o round-trip PASSA. Sem a guarda no leitor, o descarte " +
            "restauraria configuração inválida (ou explodiria numa factory como 500).");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// <c>criteriosDesempate[].args.valor</c> (ADR-0111, Story #847) não passa por
    /// <c>leitor.TextoNaoVazio</c> — <c>Valor</c> é polimórfico (escalar ou array, via
    /// <c>leitor.Valor</c>) — mas <c>CondicaoDnf.Criar</c> recusa uma string em branco como
    /// forma incoerente com o operador, com o código de domínio (não o genérico
    /// <c>EnvelopeMalformado</c>): é a mesma família de recusa nomeada (nunca 500), só que
    /// mais específica — <c>ReferenciaRegra.Criar</c> já propaga seu próprio código pelo
    /// mesmo caminho (<see cref="LeitorEnvelope.Propagar{T}"/>).
    /// </summary>
    [Fact(DisplayName = "Texto vazio em criteriosDesempate.args.valor é recusado pelo código de domínio de CondicaoDnf")]
    public void TextoVazio_NoValorDoPredicado_RecusaComCodigoDeDominio()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            Definir(envelope, "criteriosDesempate.3.args.valor", "   "));

        resultado.IsFailure.Should().BeTrue(
            "um valor em branco no predicado de fato faria round-trip PERFEITO e restauraria um desempate que não decide nada");
        resultado.Error!.Code.Should().Be("CondicaoDnf.FormaIncoerenteComOperador");
    }

    /// <summary>
    /// O valor cabe na leitura, satisfaz o domínio e <b>recanonicaliza nos mesmos bytes</b> —
    /// a prova de round-trip <b>aprova</b>. A recusa só chegaria no <c>SaveChanges</c>, como
    /// <c>DbUpdateException</c> (22001 / 22003) no meio do descarte: <b>500 não tratado</b>.
    /// </summary>
    [Theory(DisplayName = "Texto que NÃO CABE NA COLUNA é recusado pelo decoder, não pelo Postgres")]
    [InlineData("etapas.0.nome", 301)]
    [InlineData("modalidades.0.codigo", 61)]
    [InlineData("modalidades.0.descricao", 301)]
    [InlineData("modalidades.0.baseLegal", 501)]
    [InlineData("modalidades.1.acaoQuandoIndeferido", 31)]
    [InlineData("atendimento.condicoes.0.condicaoCodigo", 51)]
    [InlineData("atendimento.condicoes.0.condicaoNome", 301)]
    [InlineData("bonusRegional.municipioConvenio", 201)]
    [InlineData("bonusRegional.baseLegal", 501)]
    [InlineData("distribuicao.0.referenciaDemografica.censoReferencia", 21)]
    public void TextoAlemDoLimiteDaColuna_Recusa(string caminho, int comprimento)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            Definir(envelope, caminho, new string('X', comprimento)));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' com {comprimento} caracteres não cabe na coluna. Sem a guarda, ele atravessaria a leitura, " +
            "satisfaria o domínio, recanonicalizaria nos MESMOS bytes (a prova de round-trip aprovaria) e só " +
            "estouraria no SaveChanges — 500 no meio do descarte, em vez de recusa nomeada.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Decimal com escala impecável mas DÍGITOS DEMAIS para a coluna é recusado")]
    // numeric(18,4) — 19 dígitos não cabem.
    [InlineData("etapas.0.peso", "123456789012345.6789")]
    [InlineData("etapas.0.notaMinima", "123456789012345.6789")]
    // numeric(6,4) — o bônus comporta no máximo 99.9999.
    [InlineData("bonusRegional.fator", "100.0000")]
    [InlineData("bonusRegional.teto", "12345.6789")]
    public void DecimalAlemDaPrecisaoDaColuna_Recusa(string caminho, string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope => Definir(envelope, caminho, valor));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' = {valor} tem a escala canônica correta — e é justamente por isso que ele passaria: o " +
            "encoder o reemite idêntico, e a prova de round-trip aprova. O numeric(p,s) só estouraria no " +
            "SaveChanges (22003), no meio do descarte.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Guid não-canônico (maiúsculo, com chaves, truncado) é recusado")]
    [InlineData("AAAA0000-0000-4000-8000-000000000001")]
    [InlineData("{aaaa0000-0000-4000-8000-000000000001}")]
    [InlineData("aaaa0000000040008000000000000001")]
    [InlineData("nao-e-um-guid")]
    public void GuidNaoCanonico_Recusa(string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["id"] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// O Guid vazio é sintaticamente um Guid — e é aí que ele engana. As factories reagem de
    /// dois modos, ambos errados: <c>EtapaProcesso.Reidratar</c> e os filhos do atendimento
    /// <b>lançam</b> (500 no meio de um descarte); já <c>ConfiguracaoDistribuicaoVagas.Criar</c>
    /// simplesmente o <b>aceita</b> como oferta de curso — e o grafo inválido restaura,
    /// persiste e faz round-trip perfeito.
    /// </summary>
    [Theory(DisplayName = "Guid VAZIO é recusado em qualquer campo de identidade")]
    [InlineData("etapas.0.id")]
    [InlineData("distribuicao.0.ofertaCursoOrigemId")]
    [InlineData("modalidades.0.modalidadeOrigemId")]
    [InlineData("atendimento.condicoes.0.condicaoOrigemId")]
    [InlineData("hashesEdital.documentoEditalId")]
    [InlineData("distribuicao.0.referenciaDemografica.origemId")]
    [InlineData("criteriosDesempate.1.args.etapaRef")]
    public void GuidVazio_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            Definir(envelope, caminho, Guid.Empty.ToString()));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' com o Guid vazio ou explode numa factory (500 num descarte) ou entra como identidade " +
            "válida — nenhum dos dois é aceitável num documento com peso jurídico");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Guid VAZIO no bloco 'ofertas' é recusado")]
    public void GuidVazioEmOfertas_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["ofertas"]!.AsArray()[0] = Guid.Empty.ToString());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().BeOneOf(
            ErrosCodecEnvelope.EnvelopeMalformado,
            ErrosCodecEnvelope.BlocosDerivadosIncoerentes);
    }

    [Theory(DisplayName = "Enum fora do conjunto (ou com case trocado) é recusado")]
    [InlineData("carater", "classificatoria")]
    [InlineData("carater", "Inexistente")]
    [InlineData("carater", "Nenhum")]
    public void EnumInvalido_Recusa(string chave, string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]![chave] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Data fora do formato canônico é recusada")]
    public void DataInvalida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["periodo"]!["inicio"] = "02/03/2026");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// <c>0001-01-01</c> é como uma data <b>omitida</b> se materializa — e é por isso que os
    /// validators de publicar e de retificar a recusam explicitamente. <c>DadosEdital.Criar</c>
    /// só checa <c>fim &gt;= inicio</c>, e o default satisfaz isso; o encoder a reemite tal
    /// qual, então o round-trip passaria e o certame ficaria com um período de inscrição
    /// impossível.
    /// </summary>
    [Theory(DisplayName = "Data default (0001-01-01) é recusada — é como uma data omitida se materializa")]
    [InlineData("periodo.inicio")]
    [InlineData("periodo.fim")]
    public void DataDefault_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope => Definir(envelope, caminho, "0001-01-01"));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' zerado passa em DadosEdital.Criar (0001-01-01 <= 0001-01-01) e recanonicaliza nos mesmos " +
            "bytes — só os validators do caminho de escrita o barravam");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Número do ato além do limite dos validators (60) é recusado")]
    public void NumeroDoAtoAlemDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["periodo"]!["numero"] = new string('9', 61));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Item de array que não é objeto é recusado")]
    public void ItemDeArrayNaoObjeto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0] = "sou uma string");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Campo obrigatório com null explícito é recusado")]
    public void ObrigatorioNulo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["nome"] = null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    private static void Definir(JsonObject raiz, string caminho, string valor)
    {
        string[] partes = caminho.Split('.');
        JsonNode atual = raiz;

        for (int i = 0; i < partes.Length - 1; i++)
        {
            atual = int.TryParse(partes[i], System.Globalization.CultureInfo.InvariantCulture, out int indice)
                ? atual.AsArray()[indice]!
                : atual.AsObject()[partes[i]]!;
        }

        atual.AsObject()[partes[^1]] = valor;
    }

    private static Result<EnvelopeReidratado> Reidratar(Action<JsonObject> adulterar)
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        byte[] originais = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        CorpusEnvelope.Publicar(processo);

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(originais))!.AsObject();
        adulterar(envelope);

        byte[] adulterados = HashCanonicalComputer.ComputeSnapshotBytes(envelope);
        adulterados.Should().NotEqual(originais, "pré-condição: a adulteração tem de mudar os bytes");

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, adulterados);
        return CorpusEnvelope.Registro.Reidratar(versao);
    }
}
