namespace Unifesspa.UniPlus.Selecao.Application.Validators.AtosDoProcesso;

using Commands.AtosDoProcesso;

using Domain.Entities;

using FluentValidation;

/// <summary>
/// Valida o formato do comando de publicação do ato avulso antes de qualquer conferência. Os limites
/// são os do registro do ato do processo e do ato normativo em Publicações: o que a coluna ou o registro
/// central recusariam depois tem de virar recusa aqui, e não incidente na fila de mensagens que falharam.
/// </summary>
public sealed class PublicarAtoAvulsoDoProcessoCommandValidator : AbstractValidator<PublicarAtoAvulsoDoProcessoCommand>
{
    public PublicarAtoAvulsoDoProcessoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");

        RuleFor(x => x.TipoAtoCodigo)
            .NotEmpty()
            .WithMessage("Tipo do ato é obrigatório.")
            .MaximumLength(AtoDoProcesso.TipoCodigoMaxLength)
            .WithMessage($"Tipo do ato deve ter no máximo {AtoDoProcesso.TipoCodigoMaxLength} caracteres.")
            // Só a forma: a lista de tipos é cadastro, e conhecê-la seria ramificar por tipo (ADR-0103).
            .Matches("^[A-Z]+(_[A-Z]+)*$")
            .WithMessage("Tipo do ato deve estar em forma canônica (ex.: AVISO).");

        RuleFor(x => x.Numero)
            .MaximumLength(AtoDoProcesso.NumeroMaxLength)
            .WithMessage($"Número do ato deve ter no máximo {AtoDoProcesso.NumeroMaxLength} caracteres.");

        // DateOnly omitido binda para 0001-01-01.
        RuleFor(x => x.DataPublicacao)
            .NotEqual(default(DateOnly))
            .WithMessage("Data de publicação declarada pelo ato é obrigatória.");

        RuleFor(x => x.Assinante)
            .NotEmpty()
            .WithMessage("Assinante do ato é obrigatório.")
            .MaximumLength(AtoDoProcesso.AssinanteMaxLength)
            .WithMessage($"Assinante deve ter no máximo {AtoDoProcesso.AssinanteMaxLength} caracteres.");

        RuleFor(x => x.DocumentoPrincipalId)
            .NotEmpty()
            .WithMessage("Referência ao documento principal do ato é obrigatória.");

        // A lista é opcional, mas um item nulo dentro dela é corpo malformado.
        RuleForEach(x => x.Anexos)
            .NotNull()
            .WithMessage("Item da lista de anexos não pode ser nulo.");

        // O par ato corrigido e motivo é regra do agregado (AtoDoProcesso.ValidarCorrecao); aqui só o tamanho.
        RuleFor(x => x.MotivoDaCorrecao)
            .MaximumLength(AtoDoProcesso.MotivoDaCorrecaoMaxLength)
            .WithMessage($"O motivo da correção deve ter no máximo {AtoDoProcesso.MotivoDaCorrecaoMaxLength} caracteres.");
    }
}
