namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Falha com contexto na leitura para evitar NullReferenceException tardia.
/// O conteúdo gravado não entra na mensagem porque ela chega ao log e pode conter dado pessoal.
/// </summary>
public static class ObjetoDeValorGravado
{
    public static T Reidratar<T>(Result<T> criacao, string nomeDoValor) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(criacao);

        if (criacao.IsFailure || criacao.Value is null)
        {
            string codigo = criacao.Error?.Code ?? "Desconhecido";
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nomeDoValor}: {codigo}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return criacao.Value;
    }
}
