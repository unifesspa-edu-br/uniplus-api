namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirAtendimentoEspecializadoCommandValidator : AbstractValidator<DefinirAtendimentoEspecializadoCommand>
{
    public DefinirAtendimentoEspecializadoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RegrasDosIdentificadores();
    }

    // As listas ausentes ou nulas são recusadas pelo domínio (422), junto das demais recusas; aqui só
    // se confere o identificador de cada item informado.
    private void RegrasDosIdentificadores()
    {
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
