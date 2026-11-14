namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// As versões de configuração que os testes de recusa do codec entregam à reidratação: o envelope do
/// corpus, a linha forense que o acompanha e as formas de desfazer a coerência entre os dois.
/// </summary>
internal static class VersoesDoEnvelopeDeTeste
{
    public static ProcessoSeletivo ProcessoPublicado()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        CorpusEnvelope.Publicar(processo);
        return processo;
    }

    public static byte[] BytesDoCorpus() =>
        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico())).Bytes;

    public static byte[] BytesComRetificacaoDe(Guid atoRetificadoId, string motivo) =>
        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(
            CorpusEnvelope.ProcessoRico(),
            new RetificacaoDoEdital(atoRetificadoId, motivo))).Bytes;

    public static JsonObject EnvelopeDosBytes(byte[] bytes) =>
        JsonNode.Parse(Encoding.UTF8.GetString(bytes))!.AsObject();

    public static (VersaoConfiguracao Versao, byte[] Bytes) VersaoRica()
    {
        byte[] bytes = BytesDoCorpus();
        return (CorpusEnvelope.VersaoDeAbertura(ProcessoPublicado(), bytes), bytes);
    }

    public static VersaoConfiguracao VersaoComSchema(string schemaVersion) =>
        AbrirVersaoDoCorpus(schemaVersion, CorpusEnvelope.Codec.AlgoritmoHash, CorpusEnvelope.HashDocumento);

    public static VersaoConfiguracao VersaoComAlgoritmoDeHash(string algoritmoHash) =>
        AbrirVersaoDoCorpus(CorpusEnvelope.Codec.SchemaVersion, algoritmoHash, CorpusEnvelope.HashDocumento);

    public static VersaoConfiguracao VersaoComHashDoAtoCriador(string atoCriadorHash) =>
        AbrirVersaoDoCorpus(CorpusEnvelope.Codec.SchemaVersion, CorpusEnvelope.Codec.AlgoritmoHash, atoCriadorHash);

    public static Result<EnvelopeReidratado> ReidratarComoAbertura(byte[] bytes) =>
        CorpusEnvelope.Registro.Reidratar(CorpusEnvelope.VersaoDeAbertura(ProcessoPublicado(), bytes));

    public static Result<EnvelopeReidratado> ReidratarComoSucessora(byte[] abertura, byte[] sucessora)
    {
        VersaoConfiguracao v1 = CorpusEnvelope.VersaoDeAbertura(ProcessoPublicado(), abertura);
        return CorpusEnvelope.Registro.Reidratar(CorpusEnvelope.VersaoDeRetificacao(v1, sucessora));
    }

    /// <summary>
    /// Troca os bytes preservando <b>o hash original</b> — é o único jeito de simular a
    /// coluna adulterada sem que a linha forense acompanhe.
    /// </summary>
    public static VersaoConfiguracao ComBytes(VersaoConfiguracao original, byte[] bytes)
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

    private static VersaoConfiguracao AbrirVersaoDoCorpus(string schemaVersion, string algoritmoHash, string atoCriadorHash) =>
        VersaoConfiguracao.Abrir(
            ProcessoPublicado().Id,
            BytesDoCorpus(),
            schemaVersion,
            algoritmoHash,
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            atoCriadorHash: atoCriadorHash,
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);
}
