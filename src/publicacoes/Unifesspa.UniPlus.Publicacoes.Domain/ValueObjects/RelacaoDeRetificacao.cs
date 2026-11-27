namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

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
    private const int MotivoMaximo = 1000;

    /// <summary>
    /// A relação conferida, ou a exceção da primeira recusa de <see cref="Conferir"/>. A auto-referência é
    /// impossível por esta via (o identificador do ato é gerado depois); o insert cru fica barrado por
    /// <c>CHECK</c> no banco.
    /// </summary>
    internal RelacaoDeRetificacao Conferida() => Conferir().ValorOuLancar();

    /// <summary>
    /// Todas as recusas da relação: primeiro as do ato retificado (identificador vazio ou par pela
    /// metade), depois as do motivo. O identificador vazio é sempre recusado e, por contar como
    /// ausente, não gera uma segunda recusa do par.
    /// </summary>
    internal ParteConferida<RelacaoDeRetificacao> Conferir()
    {
        ParteConferida<string> motivo = TextoDoAto.Opcional(Motivo, MotivoMaximo, "motivoRetificacao", "O motivo da retificação");
        IReadOnlyList<FieldError> doPar = RecusaDoPar();
        Guid? atoRetificadoId = AtoRetificadoId;

        return ParteConferida<RelacaoDeRetificacao>.Compor(
            [.. doPar, .. motivo.Recusas],
            () => new RelacaoDeRetificacao(atoRetificadoId, motivo.Valor));
    }

    private IReadOnlyList<FieldError> RecusaDoPar() =>
        (AtoRetificadoId, TemMotivo: !string.IsNullOrWhiteSpace(Motivo)) switch
        {
            (var id, _) when id == Guid.Empty =>
                [Incompleta("atoRetificadoId", "O identificador do ato retificado não pode ser vazio.")],
            (null, true) =>
                [Incompleta("atoRetificadoId", "Informe o ato retificado, junto com o motivo da retificação.")],
            (not null, false) =>
                [Incompleta("motivoRetificacao", "Informe o motivo da retificação, junto com o ato retificado.")],
            _ => [],
        };

    private static FieldError Incompleta(string campo, string mensagem) =>
        new(campo, new DomainError(AtoNormativoErrorCodes.RetificacaoIncompleta, mensagem));
}
