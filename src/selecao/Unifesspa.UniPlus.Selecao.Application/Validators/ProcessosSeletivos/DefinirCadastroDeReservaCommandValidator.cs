namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// A lista de chamadas nula ou omitida é recusada na leitura do corpo (400, campo obrigatório ausente);
/// a chamada nula dentro da lista é recusada pelo domínio.
/// </summary>
public sealed class DefinirCadastroDeReservaCommandValidator : AbstractValidator<DefinirCadastroDeReservaCommand>
{
    public DefinirCadastroDeReservaCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId).NotEmpty().WithMessage("O processo seletivo é obrigatório.");
    }
}
