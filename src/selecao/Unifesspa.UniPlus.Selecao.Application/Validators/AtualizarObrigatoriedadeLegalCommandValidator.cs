namespace Unifesspa.UniPlus.Selecao.Application.Validators;

using FluentValidation;

using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Validação de fronteira HTTP do <see cref="AtualizarObrigatoriedadeLegalCommand"/>.
/// Mesmas regras de shape do <see cref="CriarObrigatoriedadeLegalCommandValidator"/>
/// + obrigatoriedade de <see cref="AtualizarObrigatoriedadeLegalCommand.Id"/>
/// não-vazio.
/// </summary>
public sealed class AtualizarObrigatoriedadeLegalCommandValidator
    : AbstractValidator<AtualizarObrigatoriedadeLegalCommand>
{
    public AtualizarObrigatoriedadeLegalCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("Id é obrigatório.");

        RuleFor(x => x.TipoEditalCodigo)
            .NotEmpty()
            .WithMessage("TipoEditalCodigo é obrigatório.")
            .MaximumLength(64);

        RuleFor(x => x.Categoria)
            .NotEqual(CategoriaObrigatoriedade.Nenhuma)
            .WithMessage("Categoria não pode ser Nenhuma.")
            .IsInEnum();

        RuleFor(x => x.RegraCodigo)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Predicado)
            .NotNull();

        RuleFor(x => x.DescricaoHumana)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(x => x.BaseLegal)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.AtoNormativoUrl)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.AtoNormativoUrl));

        RuleFor(x => x.PortariaInternaCodigo)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.PortariaInternaCodigo));

        RuleFor(x => x)
            .Must(x => x.VigenciaFim is null || x.VigenciaFim.Value > x.VigenciaInicio)
            .WithName(nameof(AtualizarObrigatoriedadeLegalCommand.VigenciaFim))
            .WithMessage("VigenciaFim deve ser estritamente posterior a VigenciaInicio.");

        RuleFor(x => x.AreasDeInteresse)
            .NotNull()
            .WithMessage("AreasDeInteresse é obrigatório (passe coleção vazia para regra global).");
    }
}
