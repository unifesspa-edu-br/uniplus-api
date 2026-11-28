namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirOpcoesDeclaradasCommandValidator : AbstractValidator<DefinirOpcoesDeclaradasCommand>
{
    public DefinirOpcoesDeclaradasCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.FatoCodigo)
            .NotEmpty()
            .WithMessage("O código do fato é obrigatório.");

        // Os itens não podem ser nulos: o handler percorre a lista, e um item nulo estouraria como 500
        // em vez de um 4xx. A lista ausente é recusada pelo handler (422), e a lista vazia pelo agregado,
        // com código próprio.
        RuleForEach(x => x.Opcoes)
            .NotNull()
            .WithMessage("Uma opção da lista não pode ser nula.");
    }
}
