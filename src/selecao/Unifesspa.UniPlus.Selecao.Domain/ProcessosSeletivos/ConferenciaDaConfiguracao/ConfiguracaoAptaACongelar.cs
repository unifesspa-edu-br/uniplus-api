namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A configuração do processo que a conferência julgou sem impedimento: só nasce de <see cref="Conferir"/>, vale durante a
/// unidade de trabalho que a produziu e não guarda conteúdo. O conteúdo congelado é o documento canônico.
/// </summary>
public sealed record ConfiguracaoAptaACongelar
{
    private ConfiguracaoAptaACongelar(Guid processoId) => ProcessoId = processoId;

    public Guid ProcessoId { get; }

    /// <summary>
    /// Julga a configuração por todas as famílias de regra, numa ordem única, contra os cadastros lidos. Só há configuração
    /// apta quando nenhuma causa se acumula; senão a falha traz todas, e a primeira é a que orienta.
    /// </summary>
    public static Result<ConfiguracaoAptaACongelar> Conferir(Guid processoId, ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(cadastros);

        IReadOnlyList<FieldError> causas = CausasQueImpedemCongelar.De(configuracao, cadastros);

        return causas.Count == 0
            ? Result<ConfiguracaoAptaACongelar>.Success(new ConfiguracaoAptaACongelar(processoId))
            : Result<ConfiguracaoAptaACongelar>.ValidationFailure(causas);
    }
}
