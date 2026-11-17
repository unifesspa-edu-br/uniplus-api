namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// A relação de retificação (ADR-0103): o par (ato retificado, motivo), simétrico — um existe se e
/// somente se o outro existe. O valor <c>default</c> é o ato que não retifica nenhum. As invariantes que
/// dependem do ato retificado (classe de congelamento coincidente, linearidade da cadeia) são do handler;
/// aqui fica só a simetria de forma.
/// </summary>
/// <param name="AtoRetificadoId">Ato que este retifica. Referência intra-módulo.</param>
/// <param name="Motivo">Motivo declarado da retificação.</param>
public readonly record struct RelacaoDeRetificacao(Guid? AtoRetificadoId, string? Motivo)
{
    /// <summary>
    /// A relação conferida: o ato retificado não é vazio e o par está completo ou ausente. O
    /// <see cref="Motivo"/> já chega normalizado, e a auto-referência é impossível por esta via (o
    /// identificador do ato é gerado depois); o insert cru fica barrado por <c>CHECK</c> no banco.
    /// </summary>
    internal RelacaoDeRetificacao ParConferido()
    {
        Exigir(AtoRetificadoId != Guid.Empty, "Identificador do ato retificado não pode ser vazio.", "atoRetificadoId");
        Exigir(
            (AtoRetificadoId is not null) == (Motivo is not null),
            "A retificação é o par (ato retificado, motivo) completo, ou nenhum dos dois.",
            "atoRetificadoId");

        return this;
    }

    private static void Exigir(bool valido, string mensagem, string paramName)
    {
        if (!valido)
        {
            throw new ArgumentException(mensagem, paramName);
        }
    }
}
