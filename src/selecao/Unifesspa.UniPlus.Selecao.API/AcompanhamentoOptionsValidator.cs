namespace Unifesspa.UniPlus.Selecao.API;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Impede habilitar a janela temporária em ambiente implantado: HML e produção rodam como
/// <c>Production</c> (ADR-0053), e só o ambiente de desenvolvimento local tem dados fictícios
/// garantidos.
/// </summary>
internal sealed class AcompanhamentoOptionsValidator(IHostEnvironment environment) : IValidateOptions<AcompanhamentoOptions>
{
    public ValidateOptionsResult Validate(string? name, AcompanhamentoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.ValidacaoHabilitada || environment.IsDevelopment())
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"{AcompanhamentoOptions.SectionName}:ValidacaoHabilitada só pode ser true no ambiente Development.");
    }
}
