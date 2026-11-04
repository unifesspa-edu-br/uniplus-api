namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// A falha que interrompe a montagem do cronograma. Cada resolução cross-módulo nomeia um insumo que falta
/// INTEIRO, e prosseguir a partir dela produziria diagnóstico sobre um estado que não existe; as recusas
/// já acumuladas descrevem defeitos reais do payload e seguem junto na resposta.
/// </summary>
internal static class InterrupcaoDaFase
{
    /// <summary>
    /// A interrupção entra no lote COM CAMPO. Um erro sem campo sairia no wire como <c>"field": null</c>,
    /// que a ADR-0023 não admite: cada elemento de <c>errors[]</c> declara o caminho do que falhou.
    /// </summary>
    public static Result<T> Com<T>(string campo, DomainError erro) =>
        Result<T>.ValidationFailure([new FieldError(campo, erro)]);
}
