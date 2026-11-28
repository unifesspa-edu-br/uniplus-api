namespace Unifesspa.UniPlus.Selecao.Application.Validators.AtosDoProcesso;

using Commands.AtosDoProcesso;

using Domain.Entities;

using FluentValidation;

/// <summary>
/// As regras de formato que todo ato do processo tem antes de qualquer conferência. Os limites são os do
/// registro do ato do processo e do ato normativo em Publicações: o que a coluna ou o registro central
/// recusariam depois tem de virar recusa aqui, e não incidente na fila de mensagens que falharam.
/// </summary>
public abstract class DadosDoAtoPublicadoValidator<TComando> : AbstractValidator<TComando>
    where TComando : IDadosDoAtoPublicado
{
    protected DadosDoAtoPublicadoValidator()
    {
        RegrasDoProcessoEDoNumero();
        RegrasDoAto();
        RegrasDosDocumentos();
        RegrasDaCorrecao();
    }

    private void RegrasDoProcessoEDoNumero()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");

        RuleFor(x => x.Numero)
            .MaximumLength(AtoDoProcesso.NumeroMaxLength)
            .WithMessage($"Número do ato deve ter no máximo {AtoDoProcesso.NumeroMaxLength} caracteres.");
    }

    private void RegrasDoAto()
    {
        // A data ausente é recusada na preparação, com o código de campo obrigatório; aqui, só a data zero informada.
        RuleFor(x => x.DataDoAto)
            .NotEqual(default(DateOnly))
            .WithMessage("Data do ato declarada pelo documento é inválida.");

        RuleFor(x => x.Assinante)
            .NotEmpty()
            .WithMessage("Assinante do ato é obrigatório.")
            .MaximumLength(AtoDoProcesso.AssinanteMaxLength)
            .WithMessage($"Assinante deve ter no máximo {AtoDoProcesso.AssinanteMaxLength} caracteres.");
    }

    private void RegrasDosDocumentos() =>
        // A lista é opcional, mas um item nulo dentro dela é corpo malformado.
        RuleForEach(x => x.Anexos)
            .NotNull()
            .WithMessage("Item da lista de anexos não pode ser nulo.");

    // O par ato corrigido e motivo é regra do agregado (AtoDoProcesso.ValidarCorrecao); aqui só o tamanho.
    private void RegrasDaCorrecao() =>
        RuleFor(x => x.MotivoDaCorrecao)
            .MaximumLength(AtoDoProcesso.MotivoDaCorrecaoMaxLength)
            .WithMessage($"O motivo da correção deve ter no máximo {AtoDoProcesso.MotivoDaCorrecaoMaxLength} caracteres.");
}
