namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class AtualizarOfertaCursoCommandValidator
    : AbstractValidator<AtualizarOfertaCursoCommand>
{
    public AtualizarOfertaCursoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da oferta de curso é obrigatório.");

        RuleFor(x => x.ProgramaDeOferta)
            .NotEmpty().WithMessage("Programa de oferta é obrigatório.")
            .Must(ProgramasDeOferta.EhValido)
            .WithMessage($"Programa de oferta deve ser um de: {string.Join(", ", ProgramasDeOferta.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.ProgramaDeOferta), ApplyConditionTo.CurrentValidator);

        // Formato em branco equivale a "default PRESENCIAL" (o domínio aplica);
        // só valida o domínio fechado quando há valor efetivo.
        RuleFor(x => x.FormatoPedagogico)
            .Must(FormatosPedagogicos.EhValido)
            .WithMessage($"Formato pedagógico deve ser um de: {string.Join(", ", FormatosPedagogicos.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.FormatoPedagogico));

        // Turno em branco equivale a "sem turno" (o domínio normaliza para nulo).
        RuleFor(x => x.Turno)
            .Must(TurnosOferta.EhValido)
            .WithMessage($"Turno da oferta deve ser um de: {string.Join(", ", TurnosOferta.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Turno));

        RuleFor(x => x.VagasAnuaisAutorizadas)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Vagas anuais autorizadas não podem ser negativas (zero é aceito).")
            .When(x => x.VagasAnuaisAutorizadas.HasValue);

        RuleFor(x => x.EMecCodigo)
            .MaximumLength(20).WithMessage("Código e-MEC da oferta deve ter no máximo 20 caracteres.");

        RuleFor(x => x.CodigoSga)
            .MaximumLength(30).WithMessage("Código no sistema de gestão acadêmica deve ter no máximo 30 caracteres.");

        RuleFor(x => x.BaseLegal)
            .MaximumLength(500).WithMessage("Base legal da oferta deve ter no máximo 500 caracteres.");

        RuleFor(x => x.AtoAutorizacaoMec)
            .MaximumLength(300).WithMessage("Ato de autorização MEC deve ter no máximo 300 caracteres.");
    }
}
