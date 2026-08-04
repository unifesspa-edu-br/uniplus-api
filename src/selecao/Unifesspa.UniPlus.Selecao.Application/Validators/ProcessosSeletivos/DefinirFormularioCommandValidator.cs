namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirFormularioCommandValidator : AbstractValidator<DefinirFormularioCommand>
{
    public DefinirFormularioCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Alinhado a ProcessoSeletivoConfiguration (varchar(300)/varchar(4000)) — sem o limite
        // aqui, um valor mais longo passa a validação e só falha em SaveChanges com erro de banco
        // em vez de 422 (mesmo achado já corrigido em DefinirBonusRegionalCommandValidator).
        RuleFor(x => x.Titulo)
            .MaximumLength(300)
            .WithMessage("Título do formulário deve ter no máximo 300 caracteres.");

        RuleFor(x => x.TermoAceiteTexto)
            .MaximumLength(4000)
            .WithMessage("Termo de aceite do formulário deve ter no máximo 4000 caracteres.");
    }
}
