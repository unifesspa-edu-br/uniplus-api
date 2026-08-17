namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; os pares <c>RegraCodigo</c>/<c>RegraVersao</c> (cálculo, arredondamento,
/// ordem de alocação, cada regra de eliminação) nunca chegam crus a
/// <c>ConfiguracaoClassificacao.Criar</c> — só alimentam a leitura do <c>rol_de_regras</c>
/// (<c>IRegraCatalogoReader</c>); a lista de regras de eliminação e cada item não podem ser
/// nulos — o handler desreferencia sem checagem defensiva. <c>NOpcoesAlocacao</c> (via
/// <c>ConfiguracaoClassificacao.ValidarNOpcoesAlocacao</c>) e a coerência de arredondamento
/// (via <c>Criar</c>) já têm equivalente completo no domínio e ficaram fora daqui.
/// </summary>
public sealed class DefinirClassificacaoCommandValidator : AbstractValidator<DefinirClassificacaoCommand>
{
    public DefinirClassificacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.RegraCalculoCodigo)
            .NotEmpty()
            .WithMessage("Código da regra de cálculo é obrigatório.");

        RuleFor(x => x.RegraCalculoVersao)
            .NotEmpty()
            .WithMessage("Versão da regra de cálculo é obrigatória.");

        // RegraArredondamentoCodigo nulo é válido (INV-B8: classificação
        // importada dispensa precisão local) — a versão só é exigida quando o
        // código é informado.
        RuleFor(x => x.RegraArredondamentoVersao)
            .NotEmpty()
            .When(x => x.RegraArredondamentoCodigo is not null)
            .WithMessage("Versão da regra de arredondamento é obrigatória quando o código é informado.");

        RuleFor(x => x.RegraOrdemAlocacaoCodigo)
            .NotEmpty()
            .WithMessage("Código da regra de ordem de alocação é obrigatório.");

        RuleFor(x => x.RegraOrdemAlocacaoVersao)
            .NotEmpty()
            .WithMessage("Versão da regra de ordem de alocação é obrigatória.");

        // RuleForEach por si só não falha sobre uma coleção nula (apenas não
        // itera) — sem esta regra, um payload malformado que omita o campo
        // chegaria ao handler como null e estouraria no foreach em vez de
        // devolver 400.
        RuleFor(x => x.RegrasEliminacao)
            .NotNull()
            .WithMessage("Lista de regras de eliminação é obrigatória (pode ser vazia).");

        RuleForEach(x => x.RegrasEliminacao)
            .NotNull()
            .WithMessage("Item de regra de eliminação não pode ser nulo.");

        RuleForEach(x => x.RegrasEliminacao).ChildRules(item =>
        {
            item.RuleFor(r => r.RegraCodigo)
                .NotEmpty()
                .WithMessage("Código da regra de eliminação é obrigatório.");

            item.RuleFor(r => r.RegraVersao)
                .NotEmpty()
                .WithMessage("Versão da regra de eliminação é obrigatória.");
        });
    }
}
