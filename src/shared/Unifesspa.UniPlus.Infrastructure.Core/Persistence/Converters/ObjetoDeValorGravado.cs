namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reidrata valores gravados sem expor seu conteúdo nas falhas de leitura.</summary>
public static class ObjetoDeValorGravado
{
    public static T Reidratar<T>(Result<T> criacao, string nomeDoValor)
    {
        ArgumentNullException.ThrowIfNull(criacao);
        if (criacao.IsFailure)
        {
            throw DadoInvalido(nomeDoValor, string.Join(", ", criacao.Errors.Select(static erro => erro.Error.Code).Distinct()));
        }

        return criacao.Value!;
    }

    public static T DesserializarDocumento<T>(Func<T> desserializacao, string nomeDoValor)
    {
        ArgumentNullException.ThrowIfNull(desserializacao);
        try
        {
            return desserializacao();
        }
        catch (Exception erro) when (erro is JsonException or FormatException)
        {
            // A exceção da biblioteca e a conversão de texto da plataforma repetem o valor lido na mensagem.
            throw DadoInvalido(nomeDoValor, MotivoDaFalhaDeLeitura(erro));
        }
    }

    /// <summary>Como <see cref="DesserializarDocumento{T}"/>, mas o JSON <c>null</c> falha: coluna nula não vira coleção vazia.</summary>
    public static T DesserializarDocumentoNaoNulo<T>(Func<T?> desserializacao, string nomeDoValor)
        where T : class =>
        DesserializarDocumento(desserializacao, nomeDoValor) ?? throw DadoInvalido(nomeDoValor, "documento JSON nulo");

    private static string MotivoDaFalhaDeLeitura(Exception erro) =>
        erro is JsonException ? "documento JSON inválido" : "valor fora do formato esperado";

    private static InvalidOperationException DadoInvalido(string nomeDoValor, string motivo) =>
        new($"Dado inválido no banco ao reidratar {nomeDoValor}: {motivo}. " +
            "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
}
