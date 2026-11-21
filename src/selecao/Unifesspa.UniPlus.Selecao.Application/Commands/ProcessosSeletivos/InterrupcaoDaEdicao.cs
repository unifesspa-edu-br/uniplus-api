namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// A falha que interrompe a edição do processo, na montagem de uma fase ou de uma etapa. Cada resolução cross-módulo nomeia um insumo que falta
/// INTEIRO, e prosseguir a partir dela produziria diagnóstico sobre um estado que não existe; as recusas
/// já acumuladas descrevem defeitos reais do payload e seguem junto na resposta.
/// </summary>
internal static class InterrupcaoDaEdicao
{
    /// <summary>
    /// A interrupção entra no lote com o campo do insumo que falta, para que o cliente veja qual insumo
    /// recusou o cronograma, e não só que o lote foi interrompido.
    /// </summary>
    public static Result<T> Com<T>(string campo, DomainError erro) =>
        Result<T>.ValidationFailure([new FieldError(campo, erro)]);
}
