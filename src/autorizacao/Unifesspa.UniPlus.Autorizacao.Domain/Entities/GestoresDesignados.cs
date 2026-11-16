namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A regra dos gestores que a instalação designa: exatamente dois, distintos, cada um pelo emissor aceito, pelo <c>contaId</c>
/// no formato UUID e pelo e-mail institucional, e nenhum deles é quem executa a instalação.
/// </summary>
public static class GestoresDesignados
{
    private const int Quantidade = 2;

    /// <summary>Os gestores conferidos, ou a recusa: a falta do número exato e distinto vence a do executor entre eles.</summary>
    internal static Result<IReadOnlyList<GestorDaInstalacao>> Conferir(
        Identidade executor, IReadOnlyList<IdentidadeInformada> informados, string emissorAceito)
    {
        ArgumentNullException.ThrowIfNull(informados);
        ArgumentException.ThrowIfNullOrWhiteSpace(emissorAceito);

        IReadOnlyList<GestorDaInstalacao> conferidos = [.. informados.Select(i => GestorDaInstalacao.Conferir(i, emissorAceito)).OfType<GestorDaInstalacao>()];
        List<Identidade> identidades = [.. conferidos.Select(static g => g.Identidade)];

        return informados.Count != Quantidade || identidades.Count != Quantidade || identidades.Distinct().Count() != Quantidade
            ? Recusa(InstalacaoErrorCodes.GestoresInvalidos, "Informe exatamente dois gestores distintos, cada um pelo emissor aceito, pelo contaId no formato UUID e pelo e-mail institucional.")
            : identidades.Contains(executor)
                ? Recusa(InstalacaoErrorCodes.ExecutorEntreGestores, "Quem executa a instalação não pode estar entre os gestores instalados.")
                : Result<IReadOnlyList<GestorDaInstalacao>>.Success(conferidos);
    }

    private static Result<IReadOnlyList<GestorDaInstalacao>> Recusa(string codigo, string mensagem) =>
        Result<IReadOnlyList<GestorDaInstalacao>>.ValidationFailure([new("gestores", new DomainError(codigo, mensagem))]);
}
