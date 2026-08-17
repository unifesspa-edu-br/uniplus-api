namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Três checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; <c>RegraCodigo</c>/<c>RegraVersao</c> nunca chegam crus a um
/// <c>Criar</c> de domínio — só alimentam a leitura do <c>rol_de_regras</c>
/// (<c>IRegraCatalogoReader</c>) — e seus limites de tamanho espelham
/// <c>LimitesDoEnvelope.RegraCodigo/RegraVersao</c> (Infrastructure; a Application não pode
/// referenciar a Infrastructure) só para o par continuar coerente com o schema EF
/// (<c>LimitesDoEnvelopeBatemComOSchemaTests</c>), não para prevenir 500. FallbackCodigo, a
/// forma/coerência de Destinos e a forma de cada item já têm equivalente completo em
/// <see cref="Domain.Entities.ConfiguracaoCascataRemanejamento.Criar"/>/
/// <see cref="Domain.Entities.DestinoRemanejamento.Criar"/>.
/// </summary>
public sealed class DefinirCascataRemanejamentoCommandValidator : AbstractValidator<DefinirCascataRemanejamentoCommand>
{
    // Espelha LimitesDoEnvelope.RegraCodigo/RegraVersao (Infrastructure) — a
    // Application não pode referenciar a Infrastructure (Clean Architecture),
    // então o par é mantido coerente manualmente; LimitesDoEnvelopeBatemComOSchemaTests
    // prova que o par de Infrastructure bate com o schema EF.
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;

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
    }
}
