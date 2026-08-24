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

                fase.RuleFor(f => f.RegraRecurso!.AtoAncoraCodigo)
                    .NotEmpty()
                    .WithMessage("O código do ato âncora é obrigatório.");

                // Magnitude, unidade declarável e completude do par de suspensividade não
                // aparecem aqui: são invariantes de RegraRecursoFase.Criar, e o domínio é a
                // fonte única de validação (ADR-0125). Repeti-las devolveria a resposta
                // genérica do validator no lugar do erro de negócio nomeado, e deixaria a
                // mesma regra escrita em dois lugares que passam a divergir sozinhos. O que
                // sobra é checagem de forma do DTO, sem equivalente no agregado.

            });
        });
    }
}
