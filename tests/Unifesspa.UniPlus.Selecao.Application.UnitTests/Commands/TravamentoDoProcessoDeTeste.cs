namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Registra, no instante em que o processo é travado, tudo o que as portas já tinham recebido: o travamento precisa vir
/// antes de qualquer leitura, de qualquer fonte, e não só antes de uma delas.
/// </summary>
internal sealed class TravamentoDoProcessoDeTeste
{
    private readonly List<string> _leiturasAntes = [];

    private TravamentoDoProcessoDeTeste()
    {
    }

    public bool Travou { get; private set; }

    /// <summary>As chamadas que as portas já tinham recebido quando o travamento aconteceu.</summary>
    public IReadOnlyList<string> LeiturasAntes => _leiturasAntes;

    public static TravamentoDoProcessoDeTeste Observar(IProcessoSeletivoRepository processos, params object[] portas)
    {
        TravamentoDoProcessoDeTeste travamento = new();
        processos.When(p => p.TravarAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())).Do(_ =>
        {
            travamento.Travou = true;
            travamento._leiturasAntes.AddRange(
                portas.Append(processos)
                    .SelectMany(porta => porta.ReceivedCalls())
                    .Select(chamada => chamada.GetMethodInfo().Name)
                    .Where(nome => nome != nameof(IProcessoSeletivoRepository.TravarAsync)));
        });
        return travamento;
    }
}
