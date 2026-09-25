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

        List<string> falhas = [];

        ValidarEndereco(options.SelecaoBaseUrl, nameof(OrigensOptions.SelecaoBaseUrl), falhas);
        ValidarEndereco(options.PublicacoesBaseUrl, nameof(OrigensOptions.PublicacoesBaseUrl), falhas);

        if (options.ItensPorPaginaDeAtos is < 1 or > OrigensOptions.MaximoItensPorPaginaDeAtos)
        {
            falhas.Add(
                $"Origens:ItensPorPaginaDeAtos precisa estar entre 1 e {OrigensOptions.MaximoItensPorPaginaDeAtos}, "
                + "que é o teto imposto pela origem.");
        }

        if (options.TetoDePaginasDeAtos < 1)
        {
            falhas.Add("Origens:TetoDePaginasDeAtos precisa ser pelo menos 1.");
        }

        if (options.TimeoutPorTentativaEmSegundos < 1)
        {
            falhas.Add("Origens:TimeoutPorTentativaEmSegundos precisa ser pelo menos 1.");
        }

        if (options.TimeoutTotalEmSegundos <= options.TimeoutPorTentativaEmSegundos)
        {
            falhas.Add(
                "Origens:TimeoutTotalEmSegundos precisa ser maior que TimeoutPorTentativaEmSegundos; "
                + "do contrário nenhuma retentativa cabe no limite da consulta.");
        }

        if (options.MaximoDeRetentativas < 0)
        {
            falhas.Add("Origens:MaximoDeRetentativas não pode ser negativo.");
        }

        // A biblioteca de resiliência também recusa esses valores, mas só ao montar a política —
        // na primeira consulta, não na subida.
        if (options.EsperaBaseEntreTentativasEmMilissegundos < 0)
        {
            falhas.Add("Origens:EsperaBaseEntreTentativasEmMilissegundos não pode ser negativa.");
        }

        if (options.ChamadasMinimasParaAvaliarCorte < 2)
        {
            falhas.Add("Origens:ChamadasMinimasParaAvaliarCorte precisa ser pelo menos 2.");
        }

        if (options.JanelaDeAmostragemDoCorteEmSegundos < 2 * options.TimeoutPorTentativaEmSegundos)
        {
            falhas.Add(
                "Origens:JanelaDeAmostragemDoCorteEmSegundos precisa ser pelo menos o dobro de "
                + "TimeoutPorTentativaEmSegundos.");
        }

        if (options.ProporcaoDeFalhaParaAbrirCorte is <= 0 or > 1)
        {
            falhas.Add("Origens:ProporcaoDeFalhaParaAbrirCorte precisa estar entre 0 (exclusivo) e 1.");
        }

        if (options.DuracaoDoCorteEmSegundos < 1)
        {
            falhas.Add("Origens:DuracaoDoCorteEmSegundos precisa ser pelo menos 1.");
        }

        if (options.ReencaminharAposEmSegundos < 1)
        {
            falhas.Add("Origens:ReencaminharAposEmSegundos precisa ser pelo menos 1.");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static void ValidarEndereco(string valor, string campo, List<string> falhas)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            falhas.Add($"Origens:{campo} é obrigatório.");
        }
        else if (!Uri.TryCreate(valor, UriKind.Absolute, out Uri? endereco)
            || (endereco.Scheme != Uri.UriSchemeHttp && endereco.Scheme != Uri.UriSchemeHttps))
        {
            falhas.Add($"Origens:{campo} precisa ser um endereço http ou https absoluto.");
        }
    }
}
