namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using System.Text.RegularExpressions;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Valida a forma do <see cref="DefinirCascataRemanejamentoCommand"/> antes do
/// handler — RN-CASCATA-4 (forma) e os limites de tamanho rodam de novo na
/// factory (fonte de verdade); aqui é só para recusar cedo, com 422 do
/// middleware, em vez de deixar o payload chegar ao domínio.
/// </summary>
public sealed partial class DefinirCascataRemanejamentoCommandValidator : AbstractValidator<DefinirCascataRemanejamentoCommand>
{
    // Espelha LimitesDoEnvelope.RegraCodigo/RegraVersao (Infrastructure) — a
    // Application não pode referenciar a Infrastructure (Clean Architecture),
    // então o par é mantido coerente manualmente; LimitesDoEnvelopeBatemComOSchemaTests
    // prova que o par de Infrastructure bate com o schema EF.
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int ModalidadeCodigoMaxLength = 60;
    private const int MaxOrigens = 8;
    private const int MaxDestinos = 56;

    [GeneratedRegex("^[A-Z0-9_]+$")]
    private static partial Regex CodigoValido();

    public DefinirCascataRemanejamentoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // RegraCodigo nulo = remover a cascata (toggle por ausência) — as
        // demais regras só se aplicam quando uma cascata está sendo definida.
        RuleFor(x => x.RegraCodigo)
            .MaximumLength(RegraCodigoMaxLength)
            .WithMessage($"Código da regra deve ter no máximo {RegraCodigoMaxLength} caracteres.");

        RuleFor(x => x.RegraVersao)
            .NotEmpty()
            .MaximumLength(RegraVersaoMaxLength)
            .When(x => x.RegraCodigo is not null)
            .WithMessage($"Versão da regra é obrigatória e deve ter no máximo {RegraVersaoMaxLength} caracteres quando RegraCodigo é informado.");

        RuleFor(x => x.FallbackCodigo)
            .NotEmpty()
            .MaximumLength(ModalidadeCodigoMaxLength)
            .Matches(CodigoValido())
            .When(x => x.RegraCodigo is not null)
            .WithMessage($"Código de fallback é obrigatório, precisa casar com ^[A-Z0-9_]+$ e ter no máximo {ModalidadeCodigoMaxLength} caracteres.");

        RuleFor(x => x.Destinos)
            .NotEmpty()
            .WithMessage("Ao menos um destino é obrigatório quando RegraCodigo é informado.")
            .When(x => x.RegraCodigo is not null);

        RuleFor(x => x.Destinos)
            // Um item nulo (JSON "destinos":[null,...]) não pode chegar aos Musts abaixo nem
            // ao ChildRules em seguida — os dois desreferenciam o item sem checar nulidade.
            .Must(static destinos => destinos!.All(static d => d is not null))
            .WithMessage("Nenhum item da lista de destinos pode ser nulo.")
            .Must(static destinos => destinos!.Count <= MaxDestinos)
            .WithMessage($"A cascata não pode declarar mais de {MaxDestinos} destinos.")
            .Must(static destinos => destinos!.Where(static d => d is not null).Select(static d => d.ModalidadeOrigemCodigo).Distinct(StringComparer.Ordinal).Count() <= MaxOrigens)
            .WithMessage($"A cascata não pode declarar mais de {MaxOrigens} origens.")
            .When(x => x.Destinos is { Count: > 0 });

        RuleForEach(x => x.Destinos).NotNull()
            .WithMessage("Nenhum item da lista de destinos pode ser nulo.");

        RuleForEach(x => x.Destinos).ChildRules(item =>
        {
            item.RuleFor(d => d.ModalidadeOrigemCodigo)
                .NotEmpty()
                .MaximumLength(ModalidadeCodigoMaxLength)
                .Matches(CodigoValido())
                .WithMessage($"Código de origem precisa casar com ^[A-Z0-9_]+$ e ter no máximo {ModalidadeCodigoMaxLength} caracteres.");

            item.RuleFor(d => d.ModalidadeDestinoCodigo)
                .NotEmpty()
                .MaximumLength(ModalidadeCodigoMaxLength)
                .Matches(CodigoValido())
                .WithMessage($"Código de destino precisa casar com ^[A-Z0-9_]+$ e ter no máximo {ModalidadeCodigoMaxLength} caracteres.");

            item.RuleFor(d => d.Ordem)
                .GreaterThanOrEqualTo(1)
                .WithMessage("Ordem do destino deve ser ≥ 1.");
        });
    }
}
