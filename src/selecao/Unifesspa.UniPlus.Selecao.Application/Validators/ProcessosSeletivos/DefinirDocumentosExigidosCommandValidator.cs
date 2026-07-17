namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using FluentValidation;

using Commands.ProcessosSeletivos;

/// <summary>
/// Validação de <b>forma</b> do <see cref="DefinirDocumentosExigidosCommand"/> — o que
/// não depende de leitura externa. As invariantes de negócio (coerência de
/// aplicabilidade, pertencimento da fase ao processo) são do domínio e do handler
/// (ADR-0102).
/// </summary>
public sealed class DefinirDocumentosExigidosCommandValidator : AbstractValidator<DefinirDocumentosExigidosCommand>
{
    private static readonly string[] AplicabilidadesValidas = ["GERAL", "CONDICIONAL"];

    private static readonly string[] ConsequenciasValidas =
    [
        "ELIMINA",
        "RECLASSIFICA_AC",
        "REMOVE_VANTAGEM",
        "PENDENCIA_REENVIO",
    ];

    public DefinirDocumentosExigidosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.Itens)
            .NotNull()
            .WithMessage("A lista de documentos exigidos não pode ser nula.");

        RuleForEach(x => x.Itens)
            .NotNull()
            .WithMessage("Item de documento exigido não pode ser nulo.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.ExigidoNaFaseId)
                .NotEmpty()
                .WithMessage("A fase em que o documento é exigido é obrigatória.");

            item.RuleFor(i => i.TipoDocumentoId)
                .NotEmpty()
                .WithMessage("O id do tipo de documento é obrigatório.");

            item.RuleFor(i => i.Aplicabilidade)
                .Must(valor => AplicabilidadesValidas.Contains(valor, StringComparer.Ordinal))
                .WithMessage($"Aplicabilidade deve ser um de: {string.Join(", ", AplicabilidadesValidas)}.");

            item.RuleFor(i => i.ConsequenciaIndeferimento)
                .Must(valor => ConsequenciasValidas.Contains(valor, StringComparer.Ordinal))
                .When(i => !string.IsNullOrWhiteSpace(i.ConsequenciaIndeferimento))
                .WithMessage($"Consequência de indeferimento deve ser um de: {string.Join(", ", ConsequenciasValidas)}.");
        });
    }
}
