namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Checa só a forma do <c>ProcessoSeletivoId</c> — um identificador de rota sem
/// equivalente no agregado (<c>ProcessoSeletivo.DefinirFormulario</c> não o
/// recebe como parâmetro). O tamanho de Título/TermoAceiteTexto tem equivalente
/// de domínio (ADR-0125) e ficou fora daqui — o agregado é a autoridade sobre
/// eles, via <c>ValidarCamposDoFormulario</c>.
/// </summary>
public sealed class DefinirFormularioCommandValidator : AbstractValidator<DefinirFormularioCommand>
{
    public DefinirFormularioCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");
    }
}
