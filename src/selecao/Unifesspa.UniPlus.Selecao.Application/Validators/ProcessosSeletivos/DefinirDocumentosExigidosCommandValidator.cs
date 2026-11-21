namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Validação de <b>forma</b> do <see cref="DefinirDocumentosExigidosCommand"/> — o que
/// não depende de leitura externa. As invariantes de negócio da árvore (grupo não vazio,
/// sem ciclo, mesma fase, cardinalidade por tipo de nó) são do domínio
/// (<c>NoExigencia.CriarGrupo</c>, ADR-0102); aqui só a FORMA de cada nó e um teto
/// OPERACIONAL de profundidade (segurança/DoS, não regra de satisfação — a árvore em si
/// não tem limite semântico de profundidade).
/// </summary>
public sealed class DefinirDocumentosExigidosCommandValidator : AbstractValidator<DefinirDocumentosExigidosCommand>
{
    /// <summary>Teto operacional — não é regra de satisfação (essa é ilimitada, ver <see cref="Domain.Entities.NoExigencia"/>).</summary>
    private const int MaxProfundidadeArvore = 20;

    public DefinirDocumentosExigidosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");
        RuleFor(x => x.Raizes)
            .NotNull()
            .WithMessage("A lista de raízes da árvore de satisfação não pode ser nula.");
        RegrasDasRaizes();
    }

    private void RegrasDasRaizes()
    {
        RuleForEach(x => x.Raizes)
            .NotNull()
            .WithMessage("Nó raiz não pode ser nulo.")
            .SetValidator(new NoExigenciaInputValidator());

        RuleFor(x => x.Raizes)
            .Must(static raizes => ProfundidadeMaxima(raizes, 1) <= MaxProfundidadeArvore)
            .When(static x => x.Raizes is { Count: > 0 })
            .WithMessage($"A árvore de satisfação excede a profundidade operacional máxima ({MaxProfundidadeArvore}).");
    }

    private static int ProfundidadeMaxima(IReadOnlyList<NoExigenciaInput>? nos, int profundidadeAtual) =>
        nos is not { Count: > 0 }
            ? profundidadeAtual - 1
            : nos.Max(no => ProfundidadeMaxima(no.Filhos, profundidadeAtual + 1));
}
