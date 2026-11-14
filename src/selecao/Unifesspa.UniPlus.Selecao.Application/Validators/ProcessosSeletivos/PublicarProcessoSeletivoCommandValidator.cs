namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class PublicarProcessoSeletivoCommandValidator : AbstractValidator<PublicarProcessoSeletivoCommand>
{
    // Espelha a coluna varchar(60) de EditalConfiguration.Numero — sem este
    // limite aqui, um número acima do tamanho da coluna só falharia no
    // SaveChanges (erro de infraestrutura não tratado), nunca como 422.
    private const int NumeroMaxLength = 60;

    public PublicarProcessoSeletivoCommandValidator()
    {
        // O bloco documental do ato é validado ANTES da publicação ser gravada: o que o
        // formato pode recusar vira 422 no campo, antes de qualquer escrita.
        RuleFor(x => x.Ato)
            .NotNull()
            .WithMessage("Dados do ato normativo são obrigatórios.")
            .SetValidator(new DadosDoAtoValidator()!);

        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");

        RuleFor(x => x.DocumentoEditalId)
            .NotEmpty()
            .WithMessage("Referência ao documento do Edital é obrigatória.");

        RuleFor(x => x.Numero)
            .MaximumLength(NumeroMaxLength)
            .WithMessage($"Número do Edital deve ter no máximo {NumeroMaxLength} caracteres.");

        // A lista é opcional (nula ou vazia quando o edital não tem anexo), mas um item nulo dentro
        // dela é corpo malformado: recusado aqui, antes de o handler tentar lê-lo.
        RuleForEach(x => x.Anexos)
            .NotNull()
            .WithMessage("Item da lista de anexos não pode ser nulo.");
    }
}
