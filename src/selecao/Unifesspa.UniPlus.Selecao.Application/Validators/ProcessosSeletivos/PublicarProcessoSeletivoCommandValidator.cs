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
        // O bloco documental do ato é validado ANTES da publicação ser gravada: o
        // registro do ato acontece depois, por mensagem durável (ADR-0108), e o que o
        // formato pode recusar tem de virar 422 na hora — não incidente na dead letter.
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
    }
}
