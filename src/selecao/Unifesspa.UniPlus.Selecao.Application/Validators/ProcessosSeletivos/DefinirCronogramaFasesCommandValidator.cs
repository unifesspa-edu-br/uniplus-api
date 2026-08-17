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
/// <remarks>
/// A checagem de janela (Fim ≥ Início) NÃO está aqui, de propósito — desde a ADR-0125,
/// <c>FaseCronograma.JanelaInvertida</c> acumula no domínio junto das demais violações da
/// mesma fase (ex.: ato produzido ausente). Mantê-la também aqui faria o FluentValidation
/// (middleware, sempre roda primeiro) bloquear sozinho um payload com janela invertida +
/// outra violação, entregando ao cliente só o erro de janela — a acumulação do domínio
/// nunca chegaria a rodar. Ordem (só <c>throw</c> no domínio, nunca acumulada) não tem
/// esse conflito e continua validada aqui.
/// </remarks>
public sealed class DefinirCronogramaFasesCommandValidator : AbstractValidator<DefinirCronogramaFasesCommand>
{
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

                // UNI-REQ-0081: o valor congelado é sempre estritamente positivo — zero ou
                // negativo tornaria o prazo inutilizável desde a publicação (imutável).
                // Ausência (null) já é a desativação (ver <remarks> de ArgsRegraPrazoRecurso)
                // — um valor presente, mas <= 0, não representa nenhuma janela real.
                fase.RuleFor(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor)
                    .GreaterThan(0)
                    .When(f => f.RegraRecurso!.SuspensividadePrimeiraInstanciaValor.HasValue)
                    .WithMessage("O valor da suspensividade da 1ª instância deve ser maior que zero.");

                fase.RuleFor(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaUnidade)
                    .NotNull()
                    .When(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor.HasValue)
                    .WithMessage("A unidade da suspensividade da 2ª instância é obrigatória quando o valor é informado.");

                fase.RuleFor(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor)
                    .NotNull()
                    .When(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaUnidade.HasValue)
                    .WithMessage("O valor da suspensividade da 2ª instância é obrigatório quando a unidade é informada.");

                // UNI-REQ-0081: mesmo motivo da 1ª instância acima.
                fase.RuleFor(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor)
                    .GreaterThan(0)
                    .When(f => f.RegraRecurso!.SuspensividadeSegundaInstanciaValor.HasValue)
                    .WithMessage("O valor da suspensividade da 2ª instância deve ser maior que zero.");
            });
        });
    }
}
