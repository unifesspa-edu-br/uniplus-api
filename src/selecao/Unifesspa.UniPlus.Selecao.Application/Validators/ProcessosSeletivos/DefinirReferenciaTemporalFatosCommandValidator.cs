namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

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

        // A remoção (Tipo nulo) é o único caso em que Data/FaseId ficam de fora da
        // coerência tudo-ou-nada acima — mas isso não os torna aceitáveis soltos: dado de
        // formulário obsoleto (ou um Tipo omitido por engano) que ainda carregue Data ou
        // FaseId não pode apagar a referência em silêncio confundindo remoção com edição.
        RuleFor(x => x)
            .Must(x => x.Data is null && x.FaseId is null)
            .When(x => x.Tipo is null)
            .WithMessage("Data e FaseId não são aceitos ao remover a referência (Tipo nulo) — omita-os ou informe um Tipo.");
    }
}
