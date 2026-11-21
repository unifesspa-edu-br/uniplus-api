namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A pergunta única do domínio "a fase pode começar?": toda decisão de início de fase passa por aqui, seja pelo período
/// que chegou ou por um fato registrado, para que nenhuma condição seja conferida num caminho e esquecida em outro.
/// </summary>
public static class CondicoesDoInicioDaFase
{
    /// <summary>
    /// Se a fase pode começar com o primeiro fato que a iniciaria. Responde sim, com o instante em que a fase começa, ou
    /// não, com o motivo da recusa. Hoje nenhuma condição impede o início, e o instante é o do primeiro fato.
    /// </summary>
    public static Result<DateTimeOffset> PodeComecar(FatoQuePodeIniciarAFase primeiroFato)
    {
        ArgumentNullException.ThrowIfNull(primeiroFato);

        return Result<DateTimeOffset>.Success(primeiroFato.Instante);
    }
}
