namespace Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;

using Kernel.Results;

public sealed record ProtocoloConvocacao
{
    public string Valor { get; }

    private ProtocoloConvocacao(string valor) => Valor = valor;

    public static ProtocoloConvocacao Gerar(int numeroChamada, int sequencial)
    {
        string protocolo = $"CONV-{DateTimeOffset.UtcNow:yyyyMMdd}-{numeroChamada:D2}-{sequencial:D5}";
        return new ProtocoloConvocacao(protocolo);
    }

    public override string ToString() => Valor;
}
