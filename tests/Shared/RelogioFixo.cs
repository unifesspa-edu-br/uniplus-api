namespace Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Um relógio parado num instante, para os testes cujo resultado depende do agora.</summary>
internal sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instante.ToUniversalTime();
}
