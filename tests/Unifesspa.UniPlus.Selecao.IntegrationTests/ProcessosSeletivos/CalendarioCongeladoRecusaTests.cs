namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// O decoder do bloco de calendário é tão estrito quanto o caminho de escrita.
/// </summary>
/// <remarks>
/// A assimetria entre escrita e leitura é invisível ao round-trip byte a byte: o encoder reemite
/// o valor sujo tal qual, a prova de fidelidade passa, e a configuração restaurada carrega um
/// calendário que o domínio recusaria criar. Por isso cada mutação abaixo ataca uma invariante
/// que a publicação já garante, e espera recusa — não bytes iguais.
/// </remarks>
public sealed class CalendarioCongeladoRecusaTests
{
    private static ProcessoSeletivo ProcessoPublicado()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        CorpusEnvelope.Publicar(processo);
        return processo;
    }

    private static Result<EnvelopeReidratado> ReidratarComBlocoAdulterado(Action<JsonObject> adulterar)
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        JsonObject payload = JsonNode.Parse(congelado.Bytes)!.AsObject();
        adulterar(payload["calendarioDiasUteis"]!.AsObject());

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(payload);
        adulterados.Should().NotEqual(congelado.Bytes, "pré-condição: a mutação tem de mudar os bytes");

        return CorpusEnvelope.Registro.Reidratar(CorpusEnvelope.VersaoDeAbertura(processo, adulterados));
    }

    private static void DeveRecusar(Action<JsonObject> adulterar, string porque)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComBlocoAdulterado(adulterar);

        resultado.IsFailure.Should().BeTrue(porque);
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    private static JsonObject Dia(JsonObject bloco, string abrangencia) =>
        bloco["diasNaoUteis"]!.AsArray()
            .Single(d => d!["abrangencia"]!.GetValue<string>() == abrangencia)!.AsObject();

    [Fact(DisplayName = "Abrangência fora do vocabulário é recusada")]
    public void AbrangenciaDesconhecida_Recusada() => DeveRecusar(
        bloco => Dia(bloco, "NACIONAL")["abrangencia"] = "REGIONAL",
        "o vocabulário é fechado nas quatro abrangências que o cadastro admite");

    [Fact(DisplayName = "Dia estadual sem UF é recusado — a UF é o que diz onde ele incide")]
    public void EstadualSemUf_Recusado() => DeveRecusar(
        bloco => Dia(bloco, "ESTADUAL")["uf"] = null,
        "sem a UF, um feriado estadual não tem território, e a contagem não saberia se ele se aplica");

    [Fact(DisplayName = "Dia nacional com UF é recusado — ele incide em todo lugar")]
    public void NacionalComUf_Recusado() => DeveRecusar(
        bloco => Dia(bloco, "NACIONAL")["uf"] = "PA",
        "recorte territorial em dia nacional é combinação que o cadastro nunca produz");

    [Fact(DisplayName = "Dia municipal sem município é recusado")]
    public void MunicipalSemMunicipio_Recusado() => DeveRecusar(
        bloco => Dia(bloco, "MUNICIPAL")["municipioIbge"] = null,
        "o código IBGE é o valor normativo do dia municipal");

    [Fact(DisplayName = "Código IBGE incoerente com a UF é recusado pela verificação do cadastro de cidade")]
    public void IbgeIncoerenteComUf_Recusado() => DeveRecusar(
        bloco => Dia(bloco, "MUNICIPAL")["uf"] = "SP",
        "1504208 é do Pará: o prefixo do código determina a UF, e a incoerência tem recusa nomeada no Kernel");

    [Fact(DisplayName = "Dia duplicado é recusado, não deduplicado em silêncio")]
    public void DiaDuplicado_Recusado() => DeveRecusar(
        bloco =>
        {
            JsonArray dias = bloco["diasNaoUteis"]!.AsArray();
            JsonObject primeiro = dias[0]!.AsObject();
            dias.Add(JsonNode.Parse(primeiro.ToJsonString())!);
        },
        "absorver a duplicata esconderia cadastro inconsistente dentro de um artefato imutável");

    [Fact(DisplayName = "Ordem fora da canônica é recusada — o artefato tem de ser o que este encoder emite")]
    public void OrdemNaoCanonica_Recusada() => DeveRecusar(
        bloco =>
        {
            JsonArray dias = bloco["diasNaoUteis"]!.AsArray();
            JsonNode primeiro = JsonNode.Parse(dias[0]!.ToJsonString())!;
            JsonNode ultimo = JsonNode.Parse(dias[^1]!.ToJsonString())!;
            dias[0] = ultimo;
            dias[^1] = primeiro;
        },
        "reordenar em silêncio aceitaria um envelope montado à mão como se este encoder o tivesse emitido");

    [Fact(DisplayName = "Chave desconhecida dentro de um dia é recusada")]
    public void ChaveDesconhecidaNoDia_Recusada() => DeveRecusar(
        bloco => Dia(bloco, "NACIONAL")["descricao"] = "Confraternização Universal",
        "um leitor tolerante a chave extra deixaria passar configuração que o encoder nunca produziu");

    [Fact(DisplayName = "Versão do dataset vazia é recusada")]
    public void VersaoVazia_Recusada() => DeveRecusar(
        bloco => bloco["versaoDataset"] = string.Empty,
        "a versão identifica a remessa de onde a lista veio — vazia, não identifica nada");

    [Fact(DisplayName = "Bloco declarado ausente com conteúdo é recusado")]
    public void AusenteComConteudo_Recusado() => DeveRecusar(
        bloco => bloco["presente"] = false,
        "declarar ausência mantendo a lista é contradição interna do artefato");

    [Fact(DisplayName = "O envelope íntegro do corpus reidrata — as recusas acima não são falso positivo")]
    public void EnvelopeIntegro_Reidrata()
    {
        ProcessoSeletivo processo = ProcessoPublicado();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(
            CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.CalendarioDiasUteis!.DiasNaoUteis.Should().HaveCount(4);
    }

    [Fact(DisplayName = "Bloco presente com lista vazia é recusado")]
    public void ListaVazia_Recusada() => DeveRecusar(
        bloco => bloco["diasNaoUteis"] = new JsonArray(),
        "um calendário sem nenhum dia não útil afirmaria que todo dia é útil — o cadastro de origem também o recusa");

    [Fact(DisplayName = "Versão do dataset acima do limite do cadastro é recusada")]
    public void VersaoAcimaDoLimite_Recusada() => DeveRecusar(
        bloco => bloco["versaoDataset"] = new string('A', 61),
        "o cadastro de origem grava no máximo 60 caracteres — aceitar 61 reidrataria um calendário que não veio dele");

    [Fact(DisplayName = "Versão do dataset com espaço em volta é recusada em vez de aparada")]
    public void VersaoForaDaFormaCanonica_Recusada() => DeveRecusar(
        bloco => bloco["versaoDataset"] = " 2026 ",
        "a factory apara a versão, e aceitar o texto aparado devolveria um objeto que não representa os bytes decodificados");

    [Fact(DisplayName = "Ausência do bloco num processo com fase que aceita recurso é recusada")]
    public void AusenteComFaseQueAceitaRecurso_Recusado() => DeveRecusar(
        bloco =>
        {
            bloco["presente"] = false;
            bloco.Remove("origemId");
            bloco.Remove("versaoDataset");
            bloco.Remove("diasNaoUteis");
        },
        "nenhuma transição publica processo com recurso e sem calendário — o round-trip não acusaria, "
            + "porque o encoder reemitiria a mesma ausência");

    [Theory(DisplayName = "Texto em forma Unicode decomposta é recusado — mesmo vindo de bytes que não passaram pelo serializador canônico")]
    // O serializador canônico normaliza toda string para NFC na emissão, então este estado não
    // nasce de uma publicação: nasce de bytes montados fora dele. Por isso o artefato é
    // reserializado aqui pelo serializador comum, que preserva a forma decomposta — usar o
    // canônico desfaria a mutação e o teste passaria sem provar nada.
    [InlineData("versaoDataset", "202\u0036\u0301")]
    [InlineData("municipioNome", "Maraba\u0301")]
    public void TextoDecomposto_Recusado(string campo, string decomposto)
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        JsonObject payload = JsonNode.Parse(congelado.Bytes)!.AsObject();
        JsonObject bloco = payload["calendarioDiasUteis"]!.AsObject();
        if (campo == "versaoDataset")
        {
            bloco[campo] = decomposto;
        }
        else
        {
            Dia(bloco, "MUNICIPAL")[campo] = decomposto;
        }

        byte[] adulterados = System.Text.Encoding.UTF8.GetBytes(payload.ToJsonString());
        adulterados.Should().NotEqual(congelado.Bytes, "pré-condição: a mutação tem de mudar os bytes");

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(
            CorpusEnvelope.VersaoDeAbertura(processo, adulterados));

        resultado.IsFailure.Should().BeTrue(
            "o encoder emite estes campos em NFC; aceitar a forma decomposta faria os bytes mudarem na recanonicalização");
    }

    [Theory(DisplayName = "Texto fora da forma canônica é recusado em vez de normalizado")]
    // A factory do domínio apara espaço e sobe a UF para caixa alta, porque é o caminho de
    // escrita. No decoder isso aceitaria um artefato que este encoder nunca emitiu, e o objeto
    // reidratado passaria a divergir dos bytes que o originaram.
    [InlineData("abrangencia", " MUNICIPAL ")]
    [InlineData("municipioIbge", " 1504208 ")]
    [InlineData("municipioNome", " Marabá ")]
    [InlineData("uf", "pa")]
    public void TextoForaDaFormaCanonica_Recusado(string campo, string valor) => DeveRecusar(
        bloco => Dia(bloco, "MUNICIPAL")[campo] = valor,
        "normalizar aqui faria o valor divergir dos bytes congelados sem que nada acusasse na leitura");
}
