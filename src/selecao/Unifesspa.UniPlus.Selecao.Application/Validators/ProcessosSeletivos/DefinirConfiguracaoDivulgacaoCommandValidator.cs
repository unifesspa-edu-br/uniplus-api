namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Checa só a forma do <c>ProcessoSeletivoId</c> — um identificador de rota sem
/// equivalente no agregado (<c>ProcessoSeletivo.DefinirConfiguracaoDivulgacao</c>
/// não o recebe como parâmetro). Todo o resto (vocabulário, piso, exclusividade
/// de identificação, justificativa) tem equivalente de domínio e ficou fora
/// daqui (ADR-0125) — <c>ConfiguracaoDivulgacao.Criar</c> é a autoridade.
/// </summary>
public sealed class DefinirConfiguracaoDivulgacaoCommandValidator : AbstractValidator<DefinirConfiguracaoDivulgacaoCommand>
{
    public DefinirConfiguracaoDivulgacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");
    }
}
