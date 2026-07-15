namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Antecipa (422) o formato dos códigos de antecessora/sucessora. As guardas que
/// dependem do grafo vigente (self-loop, aresta duplicada, ciclo) ficam no
/// agregado — o validator não consulta o cadastro.
/// </summary>
public sealed class CriarPrecedenciaFaseCommandValidator : AbstractValidator<CriarPrecedenciaFaseCommand>
{
    public CriarPrecedenciaFaseCommandValidator()
    {
        RuleFor(x => x.AntecessoraCodigo)
            .NotEmpty().WithMessage("Código da fase antecessora é obrigatório.")
            .Must(CodigoFase.EhValido)
            .WithMessage("Código da fase antecessora deve conter apenas letras maiúsculas e sublinhado "
                + "(sem hífen e sem dígito), com no máximo 60 caracteres.");

        RuleFor(x => x.SucessoraCodigo)
            .NotEmpty().WithMessage("Código da fase sucessora é obrigatório.")
            .Must(CodigoFase.EhValido)
            .WithMessage("Código da fase sucessora deve conter apenas letras maiúsculas e sublinhado "
                + "(sem hífen e sem dígito), com no máximo 60 caracteres.");
    }
}
