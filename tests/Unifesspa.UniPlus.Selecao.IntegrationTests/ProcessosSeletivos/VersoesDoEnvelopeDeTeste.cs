namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// As versões de configuração que os testes de recusa do codec entregam à reidratação: o envelope do
/// exemplo, a linha forense que o acompanha e as formas de desfazer a coerência entre os dois.
/// </summary>
internal static class VersoesDoEnvelopeDeTeste
{
    public static ProcessoSeletivo ProcessoPublicado()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico(comBancasExigidasNaPublicacao: true);
        ExemploDeProcessoSeletivoEntrada.Publicar(processo);
        return processo;
    }

    public static byte[] BytesDoExemplo() =>
        ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(ExemploDeProcessoSeletivoProcessoRico.ProcessoRico())).Bytes;

    public static byte[] BytesComRetificacaoDe(Guid atoRetificadoId, string motivo) =>
        ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(
            ExemploDeProcessoSeletivoProcessoRico.ProcessoRico(),
            new RetificacaoDoEdital(atoRetificadoId, motivo))).Bytes;

    public static JsonObject EnvelopeDosBytes(byte[] bytes) =>
        JsonNode.Parse(Encoding.UTF8.GetString(bytes))!.AsObject();

    public static (VersaoConfiguracao Versao, byte[] Bytes) VersaoRica()
    {
        byte[] bytes = BytesDoExemplo();
        return (ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(ProcessoPublicado(), bytes), bytes);
    }

    public static VersaoConfiguracao VersaoComSchema(string schemaVersion) =>
        AbrirVersaoDoExemplo(schemaVersion, PerfilCanonicoV1.Instancia.Algoritmo, ExemploDeProcessoSeletivo.HashDocumento);

    public static VersaoConfiguracao VersaoComAlgoritmoDeHash(string algoritmoHash) =>
        AbrirVersaoDoExemplo(SnapshotPublicacaoCanonicalizer.SchemaVersionAtual, algoritmoHash, ExemploDeProcessoSeletivo.HashDocumento);

    public static VersaoConfiguracao VersaoComHashDoAtoCriador(string atoCriadorHash) =>
        AbrirVersaoDoExemplo(SnapshotPublicacaoCanonicalizer.SchemaVersionAtual, PerfilCanonicoV1.Instancia.Algoritmo, atoCriadorHash);

    public static Result<EnvelopeReidratado> ReidratarComoAbertura(byte[] bytes) =>
        ExemploDeProcessoSeletivo.Registro.Reidratar(ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(ProcessoPublicado(), bytes));

    public static Result<EnvelopeReidratado> ReidratarComoSucessora(byte[] abertura, byte[] sucessora)
    {
        VersaoConfiguracao v1 = ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(ProcessoPublicado(), abertura);
        return ExemploDeProcessoSeletivo.Registro.Reidratar(ExemploDeProcessoSeletivoEntrada.VersaoDeRetificacao(v1, sucessora));
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

    private static VersaoConfiguracao AbrirVersaoDoExemplo(string schemaVersion, string algoritmoHash, string atoCriadorHash) =>
        VersaoConfiguracao.Abrir(
            ProcessoPublicado().Id, new DocumentoCongelado(BytesDoExemplo(), schemaVersion, algoritmoHash, atoCriadorHash),
            atoCriadorId: ExemploDeProcessoSeletivo.AtoAbertura, atorUsuarioContaId: ExemploDeProcessoSeletivo.Ator, instante: DateTimeOffset.UnixEpoch);
}
