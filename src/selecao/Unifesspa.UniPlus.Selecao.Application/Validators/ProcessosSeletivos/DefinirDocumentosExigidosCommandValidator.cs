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

    private static readonly string[] OperadoresValidos = ["IGUAL", "EM", "MAIOR_IGUAL", "MENOR_IGUAL"];

    private static readonly string[] AbrangenciasValidas = ["FEDERAL", "ESTADUAL", "MUNICIPAL", "INTERNA_NORMA", "INTERNA_EDITAL"];

    private static readonly string[] StatusBaseLegalValidos = ["PENDENTE", "RESOLVIDO"];

    // Espelham DocumentoExigidoBaseLegalConfiguration (HasMaxLength) — sem este teto aqui,
    // um PUT com referência/observação longa demais passa pela validação de forma e só
    // falha no SaveChanges (DbUpdateException/500), em vez de 400 acionável.
    private const int ReferenciaBaseLegalMaxLength = 500;
    private const int ObservacaoBaseLegalMaxLength = 1000;

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

            item.RuleFor(i => i.Condicoes)
                .NotNull()
                .WithMessage("A lista de condições de gatilho não pode ser nula.");

            item.RuleForEach(i => i.Condicoes).ChildRules(condicao =>
            {
                condicao.RuleFor(c => c.Clausula)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("O ordinal da cláusula não pode ser negativo.");

                condicao.RuleFor(c => c.Fato)
                    .NotEmpty()
                    .WithMessage("O fato da condição é obrigatório.");

                condicao.RuleFor(c => c.Operador)
                    .Must(valor => OperadoresValidos.Contains(valor, StringComparer.Ordinal))
                    .WithMessage($"Operador deve ser um de: {string.Join(", ", OperadoresValidos)}.");

                condicao.RuleFor(c => c.Valor)
                    .NotEmpty()
                    .WithMessage("O valor da condição é obrigatório.");
            });

            item.RuleFor(i => i.BasesLegais)
                .NotNull()
                .WithMessage("A lista de bases legais não pode ser nula.");

            item.RuleForEach(i => i.BasesLegais).ChildRules(baseLegal =>
            {
                baseLegal.RuleFor(b => b.Referencia)
                    .NotEmpty()
                    .WithMessage("A referência da base legal é obrigatória.")
                    .MaximumLength(ReferenciaBaseLegalMaxLength)
                    .WithMessage($"A referência da base legal deve ter no máximo {ReferenciaBaseLegalMaxLength} caracteres.");

                baseLegal.RuleFor(b => b.Abrangencia)
                    .Must(valor => AbrangenciasValidas.Contains(valor, StringComparer.Ordinal))
                    .WithMessage($"Abrangência da base legal deve ser um de: {string.Join(", ", AbrangenciasValidas)}.");

                baseLegal.RuleFor(b => b.Status)
                    .Must(valor => StatusBaseLegalValidos.Contains(valor, StringComparer.Ordinal))
                    .WithMessage($"Status da base legal deve ser um de: {string.Join(", ", StatusBaseLegalValidos)}.");

                baseLegal.RuleFor(b => b.Observacao)
                    .MaximumLength(ObservacaoBaseLegalMaxLength)
                    .When(b => b.Observacao is not null)
                    .WithMessage($"A observação da base legal deve ter no máximo {ObservacaoBaseLegalMaxLength} caracteres.");
            });
        });
    }
}
