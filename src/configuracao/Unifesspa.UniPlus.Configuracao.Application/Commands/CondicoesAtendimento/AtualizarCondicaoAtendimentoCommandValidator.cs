namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public sealed class AtualizarCondicaoAtendimentoCommandValidator
    : AbstractValidator<AtualizarCondicaoAtendimentoCommand>
{
    public AtualizarCondicaoAtendimentoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da condição de atendimento especializado é obrigatório.");

        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código da condição de atendimento especializado é obrigatório.")
            .Must(CodigoCondicao.EhValido)
            .WithMessage("Código da condição deve iniciar com letra maiúscula e conter apenas letras "
                + "maiúsculas, dígitos e sublinhado, com 2 a 50 caracteres (ex.: PCD, DISLEXIA, LACTANTE).");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome da condição de atendimento especializado é obrigatório.")
            .MaximumLength(200).WithMessage("Nome da condição deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Descricao)
            .MaximumLength(1000).WithMessage("Descrição da condição deve ter no máximo 1000 caracteres.")
            .When(x => x.Descricao is not null);
    }
}
