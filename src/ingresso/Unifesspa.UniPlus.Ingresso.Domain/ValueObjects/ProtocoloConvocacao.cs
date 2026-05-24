namespace Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;

using Kernel.Results;

public sealed record ProtocoloConvocacao
{
    public string Valor { get; }

    private ProtocoloConvocacao(string valor) => Valor = valor;

    public static ProtocoloConvocacao Gerar(int numeroChamada, int sequencial, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        // Prefixo de data vem do TimeProvider injetado (obrigatório, nunca
        // DateTimeOffset.UtcNow): determinístico sob um TimeProvider fixo em testes.
        string protocolo = $"CONV-{clock.GetUtcNow():yyyyMMdd}-{numeroChamada:D2}-{sequencial:D5}";
        return new ProtocoloConvocacao(protocolo);
    }

    public override string ToString() => Valor;
}
