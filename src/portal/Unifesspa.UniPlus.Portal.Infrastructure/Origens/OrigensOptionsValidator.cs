namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using Microsoft.Extensions.Options;

/// <summary>
/// Recusa, na subida do processo, configuração das origens que tornaria a página pública
/// impossível de compor — em vez de descobrir na primeira leitura de um candidato.
/// </summary>
internal sealed class OrigensOptionsValidator : IValidateOptions<OrigensOptions>
{
    public ValidateOptionsResult Validate(string? name, OrigensOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> falhas =
        [
            .. FalhasDosEnderecos(options),
            .. FalhasDaPaginacaoDosAtos(options),
            .. FalhasDosPrazos(options),
            .. FalhasDaInterrupcao(options),
            .. FalhasDoReencaminhamento(options),
        ];

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static IEnumerable<string> FalhasDosEnderecos(OrigensOptions options) =>
        new[]
        {
            FalhaDoEndereco(options.SelecaoBaseUrl, nameof(OrigensOptions.SelecaoBaseUrl)),
            FalhaDoEndereco(options.PublicacoesBaseUrl, nameof(OrigensOptions.PublicacoesBaseUrl)),
        }.OfType<string>();

    private static IEnumerable<string> FalhasDaPaginacaoDosAtos(OrigensOptions options) =>
        new[]
        {
            options.ItensPorPaginaDeAtos is < 1 or > OrigensOptions.MaximoItensPorPaginaDeAtos
                ? $"Origens:ItensPorPaginaDeAtos precisa estar entre 1 e {OrigensOptions.MaximoItensPorPaginaDeAtos}, que é o limite imposto pela origem."
                : null,
            PeloMenos(options.LimiteDePaginasDeAtos, 1, nameof(OrigensOptions.LimiteDePaginasDeAtos)),
        }.OfType<string>();

    /// <summary>
    /// A biblioteca de resiliência também recusa esses valores, mas só ao montar a política —
    /// na primeira consulta, não na subida.
    /// </summary>
    private static IEnumerable<string> FalhasDosPrazos(OrigensOptions options) =>
        new[]
        {
            PeloMenos(options.TimeoutPorTentativaEmSegundos, 1, nameof(OrigensOptions.TimeoutPorTentativaEmSegundos)),
            options.TimeoutTotalEmSegundos <= options.TimeoutPorTentativaEmSegundos
                ? "Origens:TimeoutTotalEmSegundos precisa ser maior que TimeoutPorTentativaEmSegundos; do contrário nenhuma retentativa cabe no limite da consulta."
                : null,
            options.MaximoDeRetentativas < 0 ? "Origens:MaximoDeRetentativas não pode ser negativo." : null,
            options.EsperaBaseEntreTentativasEmMilissegundos < 0 ? "Origens:EsperaBaseEntreTentativasEmMilissegundos não pode ser negativa." : null,
        }.OfType<string>();

    private static IEnumerable<string> FalhasDaInterrupcao(OrigensOptions options) =>
        new[]
        {
            PeloMenos(options.ChamadasMinimasParaAvaliarInterrupcao, 2, nameof(OrigensOptions.ChamadasMinimasParaAvaliarInterrupcao)),
            options.JanelaDeAmostragemDasFalhasEmSegundos < 2 * options.TimeoutPorTentativaEmSegundos
                ? "Origens:JanelaDeAmostragemDasFalhasEmSegundos precisa ser pelo menos o dobro de TimeoutPorTentativaEmSegundos."
                : null,
            options.ProporcaoDeFalhaParaInterromper is <= 0 or > 1
                ? "Origens:ProporcaoDeFalhaParaInterromper precisa estar entre 0 (exclusivo) e 1."
                : null,
            PeloMenos(options.DuracaoDaInterrupcaoEmSegundos, 1, nameof(OrigensOptions.DuracaoDaInterrupcaoEmSegundos)),
        }.OfType<string>();

    private static IEnumerable<string> FalhasDoReencaminhamento(OrigensOptions options) =>
        PeloMenos(options.ReencaminharAposEmSegundos, 1, nameof(OrigensOptions.ReencaminharAposEmSegundos)) is { } falha
            ? [falha]
            : [];

    private static string? FalhaDoEndereco(Uri? valor, string campo) => valor switch
    {
        null => $"Origens:{campo} é obrigatório.",
        { IsAbsoluteUri: true, Scheme: "http" or "https" } => null,
        _ => $"Origens:{campo} precisa ser um endereço http ou https absoluto.",
    };

    private static string? PeloMenos(int valor, int minimo, string campo) =>
        valor < minimo ? $"Origens:{campo} precisa ser pelo menos {minimo}." : null;
}
