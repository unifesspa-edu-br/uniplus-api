namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using FluentValidation;

using Commands.ProcessosSeletivos;

public sealed class DefinirReferenciaTemporalFatosCommandValidator : AbstractValidator<DefinirReferenciaTemporalFatosCommand>
{
    private static readonly string[] TiposValidos = ["FIM_INSCRICAO", "INICIO_FASE", "FIM_FASE", "DATA_ESPECIFICA"];

    public DefinirReferenciaTemporalFatosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Tipo nulo = remover a referência — as demais regras só se aplicam quando uma
        // referência está sendo definida. A coerência tudo-ou-nada por variante (Data só
        // com DATA_ESPECIFICA, FaseId só com INICIO_FASE/FIM_FASE) é do domínio
        // (ReferenciaTemporalFatos.Criar) — aqui só a forma do token de wire.
        RuleFor(x => x.Tipo)
            .Must(valor => TiposValidos.Contains(valor, StringComparer.Ordinal))
            .When(x => x.Tipo is not null)
            .WithMessage($"Tipo deve ser um de: {string.Join(", ", TiposValidos)}.");
    }
}
