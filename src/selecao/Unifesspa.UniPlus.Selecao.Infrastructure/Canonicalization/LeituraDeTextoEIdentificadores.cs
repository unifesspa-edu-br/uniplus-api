namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura de textos e identificadores do envelope, com a guarda de comprimento e de normalização.
/// </summary>
internal static class LeituraDeTextoEIdentificadores
{
    public static string Texto(this LeitorEnvelope leitor, JsonObject pai, string chave, string path, int maxLength = 0)
    {
        if (leitor.Falhou)
        {
            return default!;
        }

        string? valor = leitor.TextoOpcional(pai, chave, path, maxLength);
        if (leitor.Falhou)
        {
            return default!;
        }

        return valor ?? leitor.Malformado<string>($"{path}.{chave}", "esperado um texto, encontrado null.");
    }

    /// <summary>
    /// Texto que as factories do domínio exigem <b>não-branco</b> — e o exigem
    /// <b>lançando</b> (<c>ArgumentException.ThrowIfNullOrWhiteSpace</c>), não devolvendo
    /// <c>Result</c>. Sem esta guarda, um envelope com <c>"nome": ""</c> atravessaria a
    /// leitura e explodiria dentro de <c>EtapaProcesso.Reidratar</c> — uma exceção não
    /// tratada no meio de um descarte, que viraria 500 em vez de recusa nomeada.
    /// </summary>
    /// <param name="maxLength">
    /// O limite da <b>coluna</b> que vai receber o valor (<see cref="LimitesDoEnvelope"/>).
    /// Passe <c>0</c> quando o destino não for uma coluna com limite — o caso dos campos
    /// que vivem dentro de um <c>json</c>/<c>jsonb</c>.
    /// </param>
    public static string TextoNaoVazio(this LeitorEnvelope leitor, JsonObject pai, string chave, string path, int maxLength = 0)
    {
        if (leitor.Falhou)
        {
            return default!;
        }

        string texto = leitor.Texto(pai, chave, path, maxLength);
        if (leitor.Falhou)
        {
            return default!;
        }

        return string.IsNullOrWhiteSpace(texto)
            ? leitor.Malformado<string>($"{path}.{chave}", "esperado um texto não vazio.")
            : texto;
    }

    /// <summary>
    /// Recusa o texto que <b>não cabe na coluna</b>.
    /// </summary>
    /// <remarks>
    /// O limite não é capricho: um <c>etapas[].nome</c> com 301 caracteres passa por toda a
    /// leitura, satisfaz o domínio, <b>recanonicaliza nos mesmos bytes</b> — o encoder o
    /// reemite tal qual — e a prova de round-trip <b>aprova</b>. A recusa só chega no
    /// <c>SaveChanges</c>, como <c>DbUpdateException</c> do Postgres (22001, <i>value too
    /// long</i>), no meio do descarte: <b>500 não tratado</b> em vez de recusa nomeada. É a
    /// mesma classe de falha que a guarda de não-vazio fecha, e o caminho de comando já a
    /// barra no <c>FluentValidation</c>.
    /// </remarks>
    private static string GuardarComprimento(this LeitorEnvelope leitor, string texto, string path, int maxLength) =>
        maxLength > 0 && texto.Length > maxLength
            ? leitor.Malformado<string>(path, $"o texto tem {texto.Length} caracteres e a coluna comporta no máximo {maxLength}.")
            : texto;

    public static string? TextoOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, string path, int maxLength = 0)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        if (node is null)
        {
            return null;
        }

        if (node.GetValueKind() != System.Text.Json.JsonValueKind.String)
        {
            return leitor.Malformado<string?>($"{path}.{chave}", "esperado um texto.");
        }

        return leitor.GuardarComprimento(node.GetValue<string>(), $"{path}.{chave}", maxLength);
    }

    /// <summary>
    /// Guid na forma <b>exata</b> em que o encoder o escreve: <c>D</c>, minúsculo. Aceitar
    /// maiúsculo ou chaves faria a reserialização produzir bytes distintos dos lidos — o
    /// round-trip acusaria, mas só depois de a leitura já ter dado um valor por bom.
    /// </summary>
    public static Guid Identificador(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        string texto = leitor.Texto(pai, chave, path);
        if (leitor.Falhou)
        {
            return default;
        }

        if (!Guid.TryParseExact(texto, "D", out Guid valor) || !string.Equals(valor.ToString(), texto, StringComparison.Ordinal))
        {
            return leitor.Malformado<Guid>($"{path}.{chave}", $"esperado um Guid canônico (minúsculo, formato D), encontrado '{texto}'.");
        }

        // Guid vazio é sintaticamente um Guid, e é aí que ele engana. As factories reagem de
        // dois modos, ambos errados: EtapaProcesso.Reidratar e os filhos do atendimento
        // LANÇAM (500 no meio de um descarte, em vez de recusa nomeada), enquanto
        // ConfiguracaoDistribuicaoVagas.Criar simplesmente o ACEITA como oferta de curso — e
        // o grafo inválido restaura, persiste e faz round-trip perfeito. Nenhum id do
        // envelope pode ser vazio; recusar aqui é a única barreira que vale para todos.
        if (valor == Guid.Empty)
        {
            return leitor.Malformado<Guid>($"{path}.{chave}", "o identificador não pode ser o Guid vazio.");
        }

        return valor;
    }

    /// <summary>
    /// Identificador opcional (<c>DocumentoExigido.GrupoSatisfacaoId</c>/
    /// <c>IdadeMaximaEmissao.ReferenciaFaseId</c>, ambos <c>Guid?</c> por natureza).
    /// <see langword="null"/> quando a chave é <c>null</c>; do contrário, mesma validação
    /// estrita de <see cref="Identificador"/> (formato canônico, nunca o Guid vazio).
    /// </summary>
    public static Guid? IdentificadorOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        string? texto = leitor.TextoOpcional(pai, chave, path);
        if (leitor.Falhou || texto is null)
        {
            return null;
        }

        if (!Guid.TryParseExact(texto, "D", out Guid valor) || !string.Equals(valor.ToString(), texto, StringComparison.Ordinal))
        {
            return leitor.Malformado<Guid?>($"{path}.{chave}", $"esperado um Guid canônico (minúsculo, formato D), encontrado '{texto}'.");
        }

        return valor == Guid.Empty
            ? leitor.Malformado<Guid?>($"{path}.{chave}", "o identificador não pode ser o Guid vazio.")
            : valor;
    }
}
