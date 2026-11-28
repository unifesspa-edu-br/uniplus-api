namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; os pares <c>RegraCodigo</c>/<c>RegraVersao</c> (cálculo, arredondamento,
/// ordem de alocação, cada regra de eliminação) nunca chegam crus a
/// <c>ConfiguracaoClassificacao.Criar</c> — só alimentam a leitura do <c>regras_do_catalogo</c>
/// (<c>IRegraCatalogoReader</c>); cada item da lista de regras de eliminação não pode ser
/// nulo — o handler desreferencia sem checagem defensiva; a lista ausente é recusada pelo domínio. A coerência de arredondamento
/// (via <c>Criar</c>) já tem equivalente completo no domínio e ficou fora daqui.
/// </summary>
public sealed class DefinirClassificacaoCommandValidator : AbstractValidator<DefinirClassificacaoCommand>
{
    public DefinirClassificacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RegrasDaFormulaDaNota();
        RegrasDoArredondamento();
        RegrasDaOrdemDeAlocacao();
        RegrasDasEliminacoes();
    }

    private void RegrasDaFormulaDaNota()
    {
        RuleFor(x => x.RegraCalculoCodigo)
            .NotEmpty()
            .WithMessage("Código da regra de cálculo é obrigatório.");

        RuleFor(x => x.RegraCalculoVersao)
            .NotEmpty()
            .WithMessage("Versão da regra de cálculo é obrigatória.");
    }

    // RegraArredondamentoCodigo nulo é válido (classificação importada dispensa precisão local) — a
    // versão só é exigida quando o código é informado.
    private void RegrasDoArredondamento()
    {
        RuleFor(x => x.RegraArredondamentoVersao)
            .NotEmpty()
            .When(x => x.RegraArredondamentoCodigo is not null)
            .WithMessage("Versão da regra de arredondamento é obrigatória quando o código é informado.");
    }

    private void RegrasDaOrdemDeAlocacao()
    {
        RuleFor(x => x.RegraOrdemAlocacaoCodigo)
            .NotEmpty()
            .WithMessage("Código da regra de ordem de alocação é obrigatório.");

        RuleFor(x => x.RegraOrdemAlocacaoVersao)
            .NotEmpty()
            .WithMessage("Versão da regra de ordem de alocação é obrigatória.");
    }

    // A lista ausente ou nula é recusada pela criação da classificação (422), junto das demais
    // recusas; aqui só se confere cada item informado.
    private void RegrasDasEliminacoes()
    {
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
