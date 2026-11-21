namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Duas checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; a precisão/escala decimal de <c>Fator</c>/<c>Teto</c> é limite de forma
/// de wire/coluna (<c>numeric(6,4)</c>), não regra de negócio. A presença da versão da regra,
/// de <c>Fator</c> e da base legal quando o código da regra é informado é do
/// handler, que a recusa com <c>ConfiguracaoBonusRegional.CamposObrigatorios</c> antes de
/// resolver a regra e a base legal.
/// </summary>
public sealed class DefinirBonusRegionalCommandValidator : AbstractValidator<DefinirBonusRegionalCommand>
{
    public DefinirBonusRegionalCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        PrecisaoDoFatorEDoTeto();
    }

    private void PrecisaoDoFatorEDoTeto()
    {
        RuleFor(x => x.Fator)
            .PrecisionScale(6, 4, ignoreTrailingZeros: false)
            .When(x => x.Regra?.Codigo is not null && x.Fator.HasValue)
            .WithMessage("Fator do bônus, quando informado com a regra, deve ter no máximo 4 casas decimais.");

        RuleFor(x => x.Teto)
            .PrecisionScale(6, 4, ignoreTrailingZeros: false)
            .When(x => x.Teto.HasValue)
            .WithMessage("Teto do bônus, quando informado, deve ter no máximo 4 casas decimais.");
    }
}
