namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura do bloco <c>retificacao</c> e a conferência de coerência entre o envelope e a
/// versão de configuração que o guarda (hash do ato criador, cadeia de retificação).
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// O envelope não pode contradizer a linha que o guarda. O hash do ato aparece nos
    /// dois lugares, e a cadeia de retificação também: a versão 1 <b>não</b> tem o 18º
    /// bloco, e toda versão <c>N &gt; 1</c> tem — apontando para o mesmo ato que a
    /// coluna aponta. Divergir aqui significa que uma das duas evidências está errada, e
    /// não há como saber qual.
    /// </summary>
    private static DomainError? VerificarCoerenciaComAVersao(
        VersaoConfiguracao versao,
        string hashDocumento,
        RetificacaoInfo? retificacao)
    {
        if (!string.Equals(hashDocumento, versao.AtoCriadorHash, StringComparison.Ordinal))
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                "O hash do documento congelado no bloco 'hashesEdital' não é o do ato criador da versão.");
        }

        if (versao.NumeroVersao == 1)
        {
            return retificacao is null
                ? null
                : new DomainError(
                    ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                    "A versão 1 abre a cadeia e não retifica ato algum — o bloco 'retificacao' não pode existir nela.");
        }

        if (retificacao is null)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                $"A versão {versao.NumeroVersao} sucede outra e tem de carregar o bloco 'retificacao'.");
        }

        return retificacao.EditalRetificadoId == versao.AtoCriadorRetificaId
            ? null
            : new DomainError(
                ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                "O ato retificado declarado no bloco 'retificacao' não é o que a versão registra ter emendado.");
    }

    private static RetificacaoInfo? LerRetificacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "retificacao", "$");
        leitor.ExigirChaves(bloco, "retificacao", "editalRetificadoId", "motivo");

        Guid atoRetificado = leitor.Identificador(bloco, "editalRetificadoId", "retificacao");
        string motivo = leitor.TextoNaoVazio(bloco, "motivo", "retificacao");

        return leitor.Falhou ? null : new RetificacaoInfo(atoRetificado, motivo);
    }
}
