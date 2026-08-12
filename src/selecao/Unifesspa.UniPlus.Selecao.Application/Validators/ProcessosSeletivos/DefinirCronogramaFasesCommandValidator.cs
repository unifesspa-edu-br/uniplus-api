namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Enums;

using FluentValidation;

/// <summary>
/// Validação de <b>forma</b> do <see cref="DefinirCronogramaFasesCommand"/> — o que não
/// depende de leitura externa. As invariantes de negócio (piso mínimo, precedência,
/// bicondicional fase×etapa, resolução da regra/ato âncora) são do domínio e do handler
/// (ADR-0102).
/// </summary>
public sealed class DefinirCronogramaFasesCommandValidator : AbstractValidator<DefinirCronogramaFasesCommand>
{
    // Issue #1113: ArgsRegraPrazoRecurso.ResolverFimDaInterposicao faz (int)PrazoValor e
    // itera um dia por vez até contar essa quantidade de dias úteis — sem teto, um valor
    // gigantesco (inclusive > int.MaxValue) lançaria OverflowException ou giraria por
    // tempo inaceitável. 3650 (10 anos corridos) é generoso para qualquer prazo de
    // recurso real e ainda assim limita o pior caso do laço.
    private const decimal MagnitudeMaximaDiasUteis = 3650m;

    public DefinirCronogramaFasesCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // CA-06 (§4 do modelo — cardinalidade 1..*): a lista NUNCA é vazia; o domínio
        // ainda recusa com o erro nomeado ProcessoSeletivo.CronogramaFasesVazio, mas a
        // exigência de payload não-vazio já vale na borda.
        RuleFor(x => x.Fases)
            .NotEmpty()
            .WithMessage("O cronograma deve ter ao menos uma fase.");

        RuleForEach(x => x.Fases)
            .NotNull()
            .WithMessage("Item de fase não pode ser nulo.");

        RuleForEach(x => x.Fases).ChildRules(fase =>
        {
            fase.RuleFor(f => f.Ordem)
                .GreaterThan(0)
                .WithMessage("A ordem da fase deve ser maior que zero.");

            fase.RuleFor(f => f.FaseCanonicaId)
                .NotEmpty()
                .WithMessage("O id da fase canônica é obrigatório.");

            fase.RuleFor(f => f.Fim)
                .GreaterThanOrEqualTo(f => f.Inicio)
                .When(f => f.Inicio.HasValue && f.Fim.HasValue)
                .WithMessage("O fim da janela da fase não pode anteceder o início.");

            fase.RuleForEach(f => f.TiposBancaIds)
                .NotEmpty()
                .WithMessage("O id do tipo de banca não pode ser vazio.");

            fase.When(f => f.RegraRecurso is not null, () =>
            {
                fase.RuleFor(f => f.RegraRecurso!.RegraCodigo)
                    .NotEmpty()
                    .WithMessage("O código da regra de recurso é obrigatório.");

                fase.RuleFor(f => f.RegraRecurso!.RegraVersao)
                    .NotEmpty()
                    .WithMessage("A versão da regra de recurso é obrigatória.");

                fase.RuleFor(f => f.RegraRecurso!.PrazoValor)
                    .GreaterThan(0)
                    .WithMessage("O prazo de interposição deve ser maior que zero.");

                // Issue #1113: dias úteis é contagem discreta — uma fração (ex.: 1,5 dias
                // úteis) não tem significado sem uma convenção de arredondamento, e o
                // domínio (ArgsRegraPrazoRecurso.ResolverFimDaInterposicao) faz cast direto
                // para int. Recusar na borda evita truncamento silencioso.
                fase.RuleFor(f => f.RegraRecurso!.PrazoValor)
                    .Must(v => v == decimal.Truncate(v))
                    .When(f => f.RegraRecurso!.PrazoUnidade == UnidadePrazo.DiasUteis)
                    .WithMessage("O prazo de interposição em dias úteis não admite fração — informe um número inteiro de dias.");

                fase.RuleFor(f => f.RegraRecurso!.PrazoValor)
                    .LessThanOrEqualTo(MagnitudeMaximaDiasUteis)
                    .When(f => f.RegraRecurso!.PrazoUnidade == UnidadePrazo.DiasUteis)
                    .WithMessage($"O prazo de interposição em dias úteis não pode exceder {MagnitudeMaximaDiasUteis} dias.");

                fase.RuleFor(f => f.RegraRecurso!.PrazoUnidade)
                    .NotEqual(UnidadePrazo.Nenhuma)
                    .WithMessage("A unidade do prazo de interposição é obrigatória.")
                    .IsInEnum()
                    .WithMessage("Unidade do prazo de interposição inválida.");

                fase.RuleFor(f => f.RegraRecurso!.AtoAncoraCodigo)
                    .NotEmpty()
                    .WithMessage("O código do ato âncora é obrigatório.");

                // §3.6/§3.0: cada par (valor, unidade) da suspensividade é independente e
                // anulável — mas, quando informado, os dois lados do par têm de vir juntos.
                fase.RuleFor(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaUnidade)
                    .NotNull()
                    .When(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor.HasValue)
                    .WithMessage("A unidade da suspensividade da 1ª instância é obrigatória quando o valor é informado.");

                fase.RuleFor(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor)
                    .NotNull()
                    .When(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaUnidade.HasValue)
                    .WithMessage("O valor da suspensividade da 1ª instância é obrigatório quando a unidade é informada.");

                // Issue #1113: mesmo motivo do prazo de interposição acima.
                fase.RuleFor(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor)
                    .Must(v => v!.Value == decimal.Truncate(v.Value))
                    .When(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaUnidade == UnidadePrazo.DiasUteis
                        && f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor.HasValue)
                    .WithMessage("A suspensividade da 1ª instância em dias úteis não admite fração — informe um número inteiro de dias.");

                fase.RuleFor(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor)
                    .LessThanOrEqualTo(MagnitudeMaximaDiasUteis)
                    .When(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaUnidade == UnidadePrazo.DiasUteis
                        && f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor.HasValue)
                    .WithMessage($"A suspensividade da 1ª instância em dias úteis não pode exceder {MagnitudeMaximaDiasUteis} dias.");

                fase.RuleFor(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaUnidade)
                    .NotNull()
                    .When(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor.HasValue)
                    .WithMessage("A unidade da suspensividade da 2ª instância é obrigatória quando o valor é informado.");

                fase.RuleFor(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor)
                    .NotNull()
                    .When(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaUnidade.HasValue)
                    .WithMessage("O valor da suspensividade da 2ª instância é obrigatório quando a unidade é informada.");

                // Issue #1113: mesmo motivo do prazo de interposição acima.
                fase.RuleFor(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor)
                    .Must(v => v!.Value == decimal.Truncate(v.Value))
                    .When(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaUnidade == UnidadePrazo.DiasUteis
                        && f.RegraRecurso!.SuspensividadeSegundaInstanciaValor.HasValue)
                    .WithMessage("A suspensividade da 2ª instância em dias úteis não admite fração — informe um número inteiro de dias.");

                fase.RuleFor(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor)
                    .LessThanOrEqualTo(MagnitudeMaximaDiasUteis)
                    .When(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaUnidade == UnidadePrazo.DiasUteis
                        && f.RegraRecurso!.SuspensividadeSegundaInstanciaValor.HasValue)
                    .WithMessage($"A suspensividade da 2ª instância em dias úteis não pode exceder {MagnitudeMaximaDiasUteis} dias.");
            });
        });
    }
}
