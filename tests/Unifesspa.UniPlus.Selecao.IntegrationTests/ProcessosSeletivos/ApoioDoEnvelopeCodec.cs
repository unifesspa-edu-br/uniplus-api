namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

internal static class ApoioDoEnvelopeCodec
{
    internal const string SchemaCorrente = "0.0.52";

    internal static (ProcessoSeletivo Processo, FaseCronograma Fase) ProcessoDeInscricao(string titulo)
    {
        FaseCronograma fase = ProcessoBaseDeEnvelope.FaseUnica("INSCRICAO", "INSCRICAO", permiteComplementacao: true);
        return (ProcessoBaseDeEnvelope.Montar(titulo, fase), fase);
    }

    internal static DadosEdital DadosDeEditalDoEnvelope() =>
        DadosEdital.Criar(
            numero: "099/2026",
            periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            documentoEditalId: Guid.CreateVersion7()).Value!;

    internal static void ConferirSchemaCorrente(SnapshotCanonico congelado) =>
        congelado.SchemaVersion.Should().Be(SchemaCorrente, "pré-condição: o codec corrente emite a forma única");

    internal static VersaoConfiguracao Publicado(
        ProcessoSeletivo processo, EntradaCanonicalizacao entrada, SnapshotCanonico congelado, string contaId)
    {
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = entrada.Dados, Documento = new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, entrada.HashDocumento), AtorUsuarioContaId = contaId, Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        return publicacao.Value!;
    }

    internal static EnvelopeReidratado Reidratado(VersaoConfiguracao versao)
    {
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        return reidratado.Value!;
    }

    internal static JsonObject Envelope(SnapshotCanonico snapshot) =>
        JsonNode.Parse(Encoding.UTF8.GetString(snapshot.Bytes))!.AsObject();

    /// <summary>
    /// Troca <b>um</b> valor num path <c>a.0.b</c>, preservando o <b>tipo JSON</b> do
    /// original — o envelope escreve decimais como string e inteiros como número, e trocar
    /// um pelo outro testaria o parser de tipos, não a leitura do campo.
    /// </summary>
    internal static void Mutar(JsonObject raiz, string caminho, string valorNovo)
    {
        string[] partes = caminho.Split('.');
        JsonNode atual = PaiDoCampo(raiz, partes);

        MutarNo(atual, partes[^1], caminho, valorNovo);
    }

    private static void MutarNo(JsonNode atual, string chave, string caminho, string valorNovo)
    {
        if (int.TryParse(chave, CultureInfo.InvariantCulture, out int posicao))
        {
            TrocarElemento(atual.AsArray(), posicao, valorNovo, caminho);
            return;
        }

        JsonObject objeto = atual.AsObject();
        objeto[chave].Should().NotBeNull($"pré-condição: o caminho '{caminho}' tem de existir no envelope");
        objeto[chave] = ValorComoNo(objeto[chave]!, valorNovo);
    }

    /// <summary>
    /// Sem o ramo <c>True</c>/<c>False</c>, os campos booleanos mutados por este helper
    /// (<c>baseadoEmEnem</c>, <c>notaDeOrigemNoEnem</c>) virariam string JSON e seriam
    /// recusados por <see cref="LeitorEnvelope.Booleano"/>, que só aceita
    /// <c>JsonValueKind.True</c>/<c>False</c>.
    /// </summary>
    internal static JsonValue ValorComoNo(JsonNode original, string valor) => original.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.Number => JsonValue.Create(int.Parse(valor, CultureInfo.InvariantCulture)),
        System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False =>
            JsonValue.Create(bool.Parse(valor)),
        _ => JsonValue.Create(valor),
    };

    /// <summary>
    /// Congela o processo rico, muda o envelope congelado e o reidrata: o que a leitura entende do envelope mudado.
    /// O caminho pode nomear mais de uma ocorrência, separadas por '|': a mesma modalidade é ofertada pelos dois cursos
    /// do processo rico, e o processo não aceita duas cópias diferentes dela.
    /// </summary>
    internal static EnvelopeReidratado ReidratarMutado(string caminho, string valorNovo)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        return ReidratarMutado(mutado =>
        {
            foreach (string ocorrencia in caminho.Split('|'))
            {
                Mutar(mutado, ocorrencia, valorNovo);
            }
        });
    }

    internal static EnvelopeReidratado ReidratarMutado(Action<JsonObject> mutar)
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);
        JsonObject mutado = Envelope(congelado);
        mutar(mutado);
        byte[] bytesMutados = PerfilCanonicoV1.Instancia.Serializar(mutado);
        bytesMutados.Should().NotEqual(congelado.Bytes, "pré-condição: a mutação tem de mudar os bytes");

        return Reidratado(CorpusEnvelopeEntrada.VersaoDeAbertura(processo, bytesMutados));
    }

    /// <summary>
    /// Reidrata e recanonicaliza <b>com o encoder da versão dela</b>, nunca com o corrente: no dia da próxima versão do
    /// esquema, recanonicalizar uma versão anterior com o encoder corrente produziria bytes da nova, e a fidelidade da
    /// reidratação de tudo o que já foi publicado deixaria de ser verificável.
    /// </summary>
    internal static EnvelopeReidratado AssertRoundTrip(ProcessoSeletivo processo, VersaoConfiguracao versao, SnapshotCanonico congelado)
    {
        EnvelopeReidratado envelope = Reidratado(versao);
        SnapshotCanonico recodificado = Recodificado(processo, versao, envelope);

        recodificado.Bytes.Should().Equal(congelado.Bytes,
            "reidratar e recanonicalizar tem de reproduzir os bytes congelados inteiros: qualquer campo que o decoder perca sai daqui como divergência");
        recodificado.SchemaVersion.Should().Be(congelado.SchemaVersion);
        recodificado.AlgoritmoHash.Should().Be(congelado.AlgoritmoHash);

        return envelope;
    }

    private static SnapshotCanonico Recodificado(ProcessoSeletivo processo, VersaoConfiguracao versao, EnvelopeReidratado envelope)
    {
        Result<SnapshotCanonico> recodificado = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(processo, envelope.Dados, envelope.HashDocumento, FusoInstitucional.ZoneId, envelope.Retificacao, envelope.Conformidade, DadosDeCatalogo: envelope.DadosDeCatalogo));
        recodificado.IsSuccess.Should().BeTrue(recodificado.Error?.Message);

        return recodificado.Value!;
    }

    private static JsonNode PaiDoCampo(JsonObject raiz, string[] partes)
    {
        JsonNode atual = raiz;
        for (int i = 0; i < partes.Length - 1; i++)
        {
            atual = int.TryParse(partes[i], CultureInfo.InvariantCulture, out int indice)
                ? atual.AsArray()[indice]!
                : atual.AsObject()[partes[i]]!;
        }
        return atual;
    }

    private static void TrocarElemento(JsonArray array, int posicao, string valorNovo, string caminho)
    {
        array[posicao].Should().NotBeNull($"pré-condição: o caminho '{caminho}' tem de existir no envelope");
        array[posicao] = ValorComoNo(array[posicao]!, valorNovo);
    }
}
