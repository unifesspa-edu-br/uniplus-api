namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using FluentValidation;

using Commands.ProcessosSeletivos;
using Domain.ValueObjects;

public sealed class RetificarProcessoSeletivoCommandValidator : AbstractValidator<RetificarProcessoSeletivoCommand>
{
    // Espelha a coluna varchar(60) de EditalConfiguration.Numero.
    private const int NumeroMaxLength = 60;

    // O MENOR dos dois limites que o motivo atravessa: a coluna de Seleção aceita 2000,
    // mas o mesmo motivo viaja para o ato em Publicações (ADR-0108), onde o limite é 1000.
    // Aceitar 1500 aqui publicaria o Edital com 204 e mataria o registro do ato na dead
    // letter — a retificação ficaria publicada sem o ato correspondente. Um motivo que o
    // outro módulo vai recusar tem de ser recusado aqui, com 422, antes de qualquer escrita.
    //
    // O limite é aferido sobre o valor NORMALIZADO (Trim + NFC), que é o efetivamente
    // persistido: a normalização NFC pode expandir code points de composição-excluída
    // (ex.: U+0958 → U+0915 U+093C), então validar o comprimento cru deixaria passar um
    // input que estoura o limite só depois da normalização.
    private const int MotivoMaxLength = 1000;

    public RetificarProcessoSeletivoCommandValidator()
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

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("Motivo da retificação é obrigatório.")
            .Must(motivo => motivo is null
                || HashCanonicalComputer.NormalizeNfc(motivo.Trim()).Length <= MotivoMaxLength)
            .WithMessage($"Motivo da retificação deve ter no máximo {MotivoMaxLength} caracteres.");

        RuleFor(x => x.DocumentoEditalId)
            .NotEmpty()
            .WithMessage("Referência ao documento do Edital é obrigatória.");

        RuleFor(x => x.Numero)
            .MaximumLength(NumeroMaxLength)
            .WithMessage($"Número do Edital deve ter no máximo {NumeroMaxLength} caracteres.");

        // DateOnly não-nullable: campo omitido do JSON binda para
        // default(DateOnly) — sem esta checagem, os dois defaults passariam
        // trivialmente na comparação abaixo, congelando período inválido.
        RuleFor(x => x.PeriodoInscricaoInicio)
            .NotEqual(default(DateOnly))
            .WithMessage("Início do período de inscrição é obrigatório.");

        RuleFor(x => x.PeriodoInscricaoFim)
            .NotEqual(default(DateOnly))
            .WithMessage("Fim do período de inscrição é obrigatório.")
            .GreaterThanOrEqualTo(x => x.PeriodoInscricaoInicio)
            .WithMessage("O fim do período de inscrição não pode anteceder o início.");
    }
}
