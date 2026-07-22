namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirOfertaAtendimentoCommandValidator : AbstractValidator<DefinirOfertaAtendimentoCommand>
{
    public DefinirOfertaAtendimentoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // As três listas são opcionais (podem ser vazias — ofertar nada é
        // válido), mas não podem ser nulas: RuleForEach ignora a coleção nula
        // e o handler faria foreach sobre null, estourando como 500 em vez de
        // um 4xx. NotNull fecha essa fresta.
        RuleFor(x => x.CondicaoIds)
            .NotNull()
            .WithMessage("A lista de condições de atendimento não pode ser nula.");

        RuleFor(x => x.RecursoIds)
            .NotNull()
            .WithMessage("A lista de recursos de acessibilidade não pode ser nula.");

        RuleFor(x => x.TipoDeficienciaIds)
            .NotNull()
            .WithMessage("A lista de tipos de deficiência não pode ser nula.");

        RuleForEach(x => x.CondicaoIds)
            .NotEmpty()
            .WithMessage("Identificador de condição de atendimento não pode ser vazio.");

        RuleForEach(x => x.RecursoIds)
            .NotEmpty()
            .WithMessage("Identificador de recurso de acessibilidade não pode ser vazio.");

        RuleForEach(x => x.TipoDeficienciaIds)
            .NotEmpty()
            .WithMessage("Identificador de tipo de deficiência não pode ser vazio.");
    }
}
