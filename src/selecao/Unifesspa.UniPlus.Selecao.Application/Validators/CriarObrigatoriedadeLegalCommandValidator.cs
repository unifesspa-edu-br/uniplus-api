namespace Unifesspa.UniPlus.Selecao.Application.Validators;

using FluentValidation;

using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Validação de fronteira HTTP do <see cref="CriarObrigatoriedadeLegalCommand"/>.
/// Erros de shape básico (campos vazios, comprimento, vigência inconsistente)
/// retornam 422 ProblemDetails antes do command chegar ao handler. Regras de
/// domínio (governance Invariante 1 do ADR-0057, hash colision, regra
/// duplicada) são verificadas pelo handler — geram <see cref="Kernel.Results.DomainError"/>
/// específico.
/// </summary>
public sealed class CriarObrigatoriedadeLegalCommandValidator
    : AbstractValidator<CriarObrigatoriedadeLegalCommand>
{
    public CriarObrigatoriedadeLegalCommandValidator()
    {
        RuleFor(x => x.TipoEditalCodigo)
            .NotEmpty()
            .WithMessage("TipoEditalCodigo é obrigatório — use \"*\" para regras universais.")
            .MaximumLength(64);

        RuleFor(x => x.Categoria)
            .NotEqual(CategoriaObrigatoriedade.Nenhuma)
            .WithMessage("Categoria não pode ser Nenhuma (sentinel default).")
            .IsInEnum()
            .WithMessage("Categoria inválida.");

        RuleFor(x => x.RegraCodigo)
            .NotEmpty()
            .WithMessage("RegraCodigo é obrigatório.")
            .MaximumLength(128);

        RuleFor(x => x.Predicado)
            .NotNull()
            .WithMessage("Predicado é obrigatório.");

        RuleFor(x => x.DescricaoHumana)
            .NotEmpty()
            .WithMessage("DescricaoHumana é obrigatória.")
            .MaximumLength(1000);

        RuleFor(x => x.BaseLegal)
            .NotEmpty()
            .WithMessage("BaseLegal é obrigatória.")
            .MaximumLength(500);

        RuleFor(x => x.AtoNormativoUrl)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.AtoNormativoUrl));

        RuleFor(x => x.PortariaInternaCodigo)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.PortariaInternaCodigo));

        RuleFor(x => x)
            .Must(x => x.VigenciaFim is null || x.VigenciaFim.Value > x.VigenciaInicio)
            .WithName(nameof(CriarObrigatoriedadeLegalCommand.VigenciaFim))
            .WithMessage("VigenciaFim deve ser estritamente posterior a VigenciaInicio.");

        RuleFor(x => x.AreasDeInteresse)
            .NotNull()
            .WithMessage("AreasDeInteresse é obrigatório — passe coleção vazia para regra global.");
    }
}
