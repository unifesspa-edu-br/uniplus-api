namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using FluentValidation;

/// <summary>Antecipa as invariantes de fronteira; o agregado repete a defesa.</summary>
public sealed class CriarTipoProcessoCommandValidator : AbstractValidator<CriarTipoProcessoCommand>
{
    private const char CaractereNulo = (char)0;

    public CriarTipoProcessoCommandValidator()
    {
        RuleFor(command => command.Codigo)
            .NotEmpty().WithMessage("Código do tipo de processo seletivo é obrigatório.")
            .MaximumLength(64).WithMessage("Código do tipo de processo seletivo deve ter no máximo 64 caracteres.")
            .NotEqual("*").WithMessage("O código '*' é reservado para obrigatoriedade legal universal.")
            .Must(static codigo => codigo is null || !codigo.Contains(CaractereNulo))
            .WithMessage("Código do tipo de processo seletivo não pode conter o caractere nulo (U+0000).");
        RuleFor(command => command.Nome)
            .NotEmpty().WithMessage("Nome do tipo de processo seletivo é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do tipo de processo seletivo deve ter no máximo 200 caracteres.")
            .Must(static nome => nome is null || !nome.Contains(CaractereNulo))
            .WithMessage("Nome do tipo de processo seletivo não pode conter o caractere nulo (U+0000).");
        RuleFor(command => command.Descricao)
            .MaximumLength(1000).WithMessage("Descrição do tipo de processo seletivo deve ter no máximo 1000 caracteres.")
            .Must(static descricao => descricao is null || !descricao.Contains(CaractereNulo))
            .WithMessage("Descrição do tipo de processo seletivo não pode conter o caractere nulo (U+0000).")
            .When(command => command.Descricao is not null);
    }
}
