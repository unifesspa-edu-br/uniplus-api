namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using FluentValidation;

public sealed class RetificarProcessoSeletivoCommandValidator : AbstractValidator<RetificarProcessoSeletivoCommand>
{
    // Espelha a coluna varchar(60) de EditalConfiguration.Numero.
    private const int NumeroMaxLength = 60;

    // O MENOR dos dois limites que o motivo atravessa: a coluna de Seleção aceita 2000,
    // mas o mesmo motivo viaja para o ato em Publicações, onde o limite é 1000. Um motivo que
    // o outro módulo vai recusar tem de ser recusado aqui, com 422 no campo, antes de
    // qualquer escrita.
    //
    // O limite é aferido sobre o valor NORMALIZADO (Trim + NFC), que é o efetivamente
    // persistido: a normalização NFC pode expandir code points de composição-excluída
    // (ex.: U+0958 → U+0915 U+093C), então validar o comprimento cru deixaria passar um
    // input que estoura o limite só depois da normalização.
    private const int MotivoMaxLength = 1000;

    public RetificarProcessoSeletivoCommandValidator()
    {
        RegrasDoAto();
        RegrasDoProcesso();
        RegrasDoMotivo();
        RegrasDoEdital();
    }

    // O bloco documental do ato é validado ANTES da publicação ser gravada: o que o
    // formato pode recusar vira 422 no campo, antes de qualquer escrita. O bloco ausente é
    // recusado na conferência do ato, junto das demais causas.
    private void RegrasDoAto() =>
        RuleFor(x => x.Ato)
            .SetValidator(new DadosDoAtoValidator()!);

    private void RegrasDoProcesso() =>
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");

    private void RegrasDoMotivo() =>
        RuleFor(x => x.Motivo)
            .Must(motivo => motivo is null
                || HashCanonicalComputer.NormalizeNfc(motivo.Trim()).Length <= MotivoMaxLength)
            .WithMessage($"Motivo da retificação deve ter no máximo {MotivoMaxLength} caracteres.");

    private void RegrasDoEdital()
    {
        RuleFor(x => x.Numero)
            .MaximumLength(NumeroMaxLength)
            .WithMessage($"Número do Edital deve ter no máximo {NumeroMaxLength} caracteres.");

        // A lista é opcional (nula mantém os anexos da versão vigente), mas um item nulo dentro
        // dela é corpo malformado: recusado aqui, antes de o handler tentar lê-lo.
        RuleForEach(x => x.Anexos)
            .NotNull()
            .WithMessage("Item da lista de anexos não pode ser nulo.");
    }
}
