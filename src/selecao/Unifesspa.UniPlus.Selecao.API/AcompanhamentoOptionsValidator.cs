namespace Unifesspa.UniPlus.Selecao.API;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>Impede habilitar a janela temporária fora de ambientes controlados.</summary>
internal sealed class AcompanhamentoOptionsValidator(IHostEnvironment environment) : IValidateOptions<AcompanhamentoOptions>
{
    public ValidateOptionsResult Validate(string? name, AcompanhamentoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.ValidacaoHabilitada
            || environment.IsDevelopment()
            || environment.IsEnvironment("Testing")
            || environment.IsStaging())
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"{AcompanhamentoOptions.SectionName}:ValidacaoHabilitada só pode ser true nos ambientes Development, Testing ou Staging.");
    }
}
