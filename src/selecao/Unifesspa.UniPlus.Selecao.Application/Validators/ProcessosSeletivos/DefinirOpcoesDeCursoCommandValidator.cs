namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Só o identificador de rota, que <c>ProcessoSeletivo.DefinirOpcoesDeCurso</c> não recebe (ADR-0125).
/// A faixa do número tem equivalente completo no agregado e fica fora daqui.
/// </summary>
public sealed class DefinirOpcoesDeCursoCommandValidator : AbstractValidator<DefinirOpcoesDeCursoCommand>
{
    public DefinirOpcoesDeCursoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");
    }
}
