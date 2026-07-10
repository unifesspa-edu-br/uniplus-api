namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests;

/// <summary>
/// <see cref="TimeProvider"/> parado num instante, para os testes que exercitam o
/// fallback "hoje" das queries. Evita depender de um pacote de fakes só por isto.
/// </summary>
internal sealed class RelogioFixo : TimeProvider
{
    private readonly DateTimeOffset _agora;

    public RelogioFixo(DateTimeOffset agora) => _agora = agora;

    public override DateTimeOffset GetUtcNow() => _agora;
}
