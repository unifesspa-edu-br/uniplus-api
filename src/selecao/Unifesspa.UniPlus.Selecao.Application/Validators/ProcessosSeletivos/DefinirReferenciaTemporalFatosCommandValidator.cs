namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Duas checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; a coerência de Data/FaseId com Tipo nulo (remoção) não passa por
/// <c>ReferenciaTemporalFatos.Criar</c> — o VO só é construído quando <c>Tipo</c> não é
/// nulo, então essa combinação nunca chega ao domínio. O vocabulário de <c>Tipo</c> e a
/// coerência tudo-ou-nada por variante (quando <c>Tipo</c> presente) têm equivalente de
/// domínio e ficaram fora daqui.
/// </summary>
public sealed class DefinirReferenciaTemporalFatosCommandValidator : AbstractValidator<DefinirReferenciaTemporalFatosCommand>
{
    public DefinirReferenciaTemporalFatosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // A remoção (Tipo nulo) é o único caso em que Data/FaseId não passam pela coerência
        // tudo-ou-nada do domínio — mas isso não os torna aceitáveis soltos: dado de
        // formulário obsoleto (ou um Tipo omitido por engano) que ainda carregue Data ou
        // FaseId não pode apagar a referência em silêncio confundindo remoção com edição.
        RuleFor(x => x)
            .Must(x => x.Data is null && x.FaseId is null)
            .When(x => x.Tipo is null)
            .WithMessage("Data e FaseId não são aceitos ao remover a referência (Tipo nulo) — omita-os ou informe um Tipo.");
    }
}
