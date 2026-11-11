namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quando o processo inclui e retira motivos do cadastro (UNI-REQ-0285). No rascunho a escolha é
/// livre. Depois da publicação, só a retificação inclui — com justificativa e antes de começar a
/// avaliação daquela decisão (UNI-REQ-0124) — e nada se retira do que já foi publicado.
/// </summary>
/// <remarks>
/// A escolha vive fora da raiz do processo, mas obedece à mesma guarda de mutação dela: a sessão
/// de retificação aberta, a precondição <c>If-Match</c> e o avanço da revisão da sessão, para que
/// incluir motivo invalide o ETag como qualquer outra edição da configuração.
/// </remarks>
public static class EscolhaDosMotivos
{
    public const int JustificativaMaxLength = 1000;

    /// <summary>Inclui no processo a cópia do motivo do cadastro, ou devolve a recusa.</summary>
    public static Result<MotivoDoProcesso> Incluir(InclusaoDeMotivo inclusao)
    {
        ArgumentNullException.ThrowIfNull(inclusao);

        ProcessoSeletivo processo = inclusao.Processo;
        if ((processo.MutacaoBloqueada(inclusao.Precondicao) ?? RecusaDaOrigem(inclusao) ?? RecusaAposPublicacao(inclusao)) is { } recusa)
        {
            return Result<MotivoDoProcesso>.Failure(recusa);
        }

        string? justificativa = processo.Status == StatusProcesso.Publicado ? inclusao.Justificativa!.Trim() : null;
        processo.Rascunho?.IncrementarRevisao();
        return Result<MotivoDoProcesso>.Success(MotivoDoProcesso.Copiar(processo.Id, inclusao.Origem, justificativa));
    }

    /// <summary>
    /// Retira o motivo escolhido. O que já foi publicado não sai (UNI-REQ-0124): a decisão que o
    /// citou continua citando um motivo que o edital previu. A inclusão ainda pendente na sessão de
    /// retificação sai, porque ainda não é do edital.
    /// </summary>
    public static Result Retirar(ProcessoSeletivo processo, MotivoDoProcesso escolhido, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhido);

        if ((processo.MutacaoBloqueada(precondicao) ?? RecusaDaRetirada(escolhido)) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        processo.Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    private static DomainError? RecusaDaRetirada(MotivoDoProcesso escolhido) =>
        escolhido.Congelado
            ? new DomainError(
                MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao,
                $"O motivo {escolhido.Codigo.Valor} já consta do edital publicado e não pode ser retirado; a retificação só inclui motivos.")
            : null;

    private static DomainError? RecusaDaOrigem(InclusaoDeMotivo inclusao)
    {
        MotivoDeDecisao origem = inclusao.Origem;
        if (!origem.Ativo)
        {
            return new DomainError(
                MotivoDeDecisaoErrorCodes.MotivoInativo,
                $"O motivo {origem.Codigo.Valor} está desativado e não entra em novas escolhas do processo.");
        }

        return inclusao.Escolhidos.Any(e => e.MotivoDeDecisaoOrigemId == origem.Id)
            ? new DomainError(MotivoDeDecisaoErrorCodes.JaEscolhido, $"O processo já escolheu o motivo {origem.Codigo.Valor}.")
            : null;
    }

    private static DomainError? RecusaAposPublicacao(InclusaoDeMotivo inclusao)
    {
        if (inclusao.Processo.Status != StatusProcesso.Publicado)
        {
            return null;
        }

        return RecusaDaJustificativa(inclusao.Justificativa)
            ?? (InicioDaAvaliacao.JaComecou(inclusao.Processo, inclusao.Origem.Tipo, inclusao.PrimeiroRegistroDaDecisao, inclusao.Agora)
                ? new DomainError(
                    MotivoDeDecisaoErrorCodes.AvaliacaoIniciada,
                    $"A avaliação de {inclusao.Origem.Tipo.ToCodigo()} já começou neste processo; motivo novo desse tipo não entra mais.")
                : null);
    }

    private static DomainError? RecusaDaJustificativa(string? justificativa)
    {
        if (string.IsNullOrWhiteSpace(justificativa))
        {
            return new DomainError(
                MotivoDeDecisaoErrorCodes.JustificativaObrigatoria,
                "Incluir motivo no processo publicado exige justificativa, de uso interno da administração.");
        }

        if (ValidacaoDoMotivo.TemCaractereNulo(justificativa))
        {
            return new DomainError(MotivoDeDecisaoErrorCodes.JustificativaCaractereInvalido, "A justificativa não pode conter o caractere nulo.");
        }

        return justificativa.Trim().Length > JustificativaMaxLength
            ? new DomainError(MotivoDeDecisaoErrorCodes.JustificativaTamanho, $"A justificativa deve ter até {JustificativaMaxLength} caracteres.")
            : null;
    }
}

/// <summary>O pedido de inclusão de um motivo do cadastro no processo.</summary>
/// <param name="Escolhidos">Os motivos que o processo já escolheu.</param>
/// <param name="Agora">O instante da operação, comparado ao início da avaliação.</param>
/// <param name="PrimeiroRegistroDaDecisao">O primeiro registro de decisão do tipo no processo, quando houver.</param>
public sealed record InclusaoDeMotivo(
    ProcessoSeletivo Processo,
    IReadOnlyCollection<MotivoDoProcesso> Escolhidos,
    MotivoDeDecisao Origem,
    string? Justificativa,
    PrecondicaoIfMatch Precondicao,
    DateTimeOffset Agora,
    DateTimeOffset? PrimeiroRegistroDaDecisao);
