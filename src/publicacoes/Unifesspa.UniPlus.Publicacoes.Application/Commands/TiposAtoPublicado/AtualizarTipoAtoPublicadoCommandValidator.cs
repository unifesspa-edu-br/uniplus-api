namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using FluentValidation;

/// <summary>
/// Espelha o <see cref="CriarTipoAtoPublicadoCommandValidator"/>, acrescido do
/// identificador. As regras avaliam o valor normalizado, como o agregado faz.
/// </summary>
public sealed class AtualizarTipoAtoPublicadoCommandValidator
    : AbstractValidator<AtualizarTipoAtoPublicadoCommand>
{
    public AtualizarTipoAtoPublicadoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(TipoAtoPublicadoRegras.IdObrigatorio);

        RuleFor(x => TipoAtoPublicadoRegras.Normalizar(x.Codigo))
            .NotEmpty().WithMessage(TipoAtoPublicadoRegras.CodigoObrigatorio)
            .MaximumLength(TipoAtoPublicadoRegras.CodigoMaxLength).WithMessage(TipoAtoPublicadoRegras.CodigoTamanho)
            .Matches(TipoAtoPublicadoRegras.CodigoPattern).WithMessage(TipoAtoPublicadoRegras.CodigoFormato)
            .OverridePropertyName(nameof(AtualizarTipoAtoPublicadoCommand.Codigo));

        RuleFor(x => TipoAtoPublicadoRegras.Normalizar(x.Nome))
            .NotEmpty().WithMessage(TipoAtoPublicadoRegras.NomeObrigatorio)
            .Length(TipoAtoPublicadoRegras.NomeMinLength, TipoAtoPublicadoRegras.NomeMaxLength)
            .WithMessage(TipoAtoPublicadoRegras.NomeTamanho)
            .OverridePropertyName(nameof(AtualizarTipoAtoPublicadoCommand.Nome));

        RuleFor(x => TipoAtoPublicadoRegras.Normalizar(x.BaseLegal))
            .MaximumLength(TipoAtoPublicadoRegras.BaseLegalMaxLength).WithMessage(TipoAtoPublicadoRegras.BaseLegalTamanho)
            .OverridePropertyName(nameof(AtualizarTipoAtoPublicadoCommand.BaseLegal))
            .When(x => x.BaseLegal is not null);

        RuleFor(x => x.VigenciaFim)
            .GreaterThan(x => x.VigenciaInicio).WithMessage(TipoAtoPublicadoRegras.VigenciaFim)
            .When(x => x.VigenciaFim.HasValue);
    }
}
