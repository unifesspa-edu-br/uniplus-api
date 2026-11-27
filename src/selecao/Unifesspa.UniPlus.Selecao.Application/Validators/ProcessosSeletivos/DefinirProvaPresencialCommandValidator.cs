namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// A lista de municípios nula ou omitida é recusada na leitura do corpo (400, campo obrigatório ausente);
/// o município nulo dentro da lista é recusado pelo domínio.
/// </summary>
public sealed class DefinirProvaPresencialCommandValidator : AbstractValidator<DefinirProvaPresencialCommand>
{
    public DefinirProvaPresencialCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId).NotEmpty().WithMessage("O processo seletivo é obrigatório.");
    }
}
