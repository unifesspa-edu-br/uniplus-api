namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Trimado pela ADR-0125 só do que <see cref="Unifesspa.UniPlus.Selecao.Domain.Entities.ConfiguracaoDistribuicaoVagas.ValidarFormaBasica"/>
/// cobre de forma incondicional: VoBase (positividade) e PR (faixa [0,5; 1]).
/// O resto permanece — a maioria por não ter equivalente de domínio possível (o
/// domínio nunca vê <see cref="ConfiguracaoDistribuicaoVagasInput.OfertaCursoId"/>/
/// <see cref="QuantidadeVagaInput.ModalidadeId"/> crus, só o já resolvido pelo
/// handler), e a checagem de <c>Quadro</c> sem <c>ModalidadeId</c> duplicado
/// especificamente por ter equivalente só PARCIAL: o handler monta
/// <c>quadroPorModalidade</c> com <c>ToDictionary</c> — uma chave repetida
/// dispara <see cref="ArgumentException"/> não tratada (500) antes de o domínio
/// sequer rodar. AGENTS.md L39-43: validator de shape sem equivalente
/// incondicional no domínio sobrevive, não é deletado cegamente.
/// </summary>
public sealed class DefinirDistribuicaoVagasCommandValidator : AbstractValidator<DefinirDistribuicaoVagasCommand>
{
    public DefinirDistribuicaoVagasCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.DistribuicaoVagas)
            .NotEmpty()
            .WithMessage("O processo deve ter ao menos uma distribuição de vagas configurada.");

        // Rejeita item nulo no array antes das regras de campo — mesma proteção
        // de DefinirEtapasCommandValidator (sem isso o handler desreferenciaria
        // o item, estourando como 500).
        RuleForEach(x => x.DistribuicaoVagas)
            .NotNull()
            .WithMessage("Item de distribuição de vagas não pode ser nulo.");

        RuleForEach(x => x.DistribuicaoVagas).ChildRules(item =>
        {
            item.RuleFor(d => d.OfertaCursoId)
                .NotEmpty()
                .WithMessage("OfertaCursoId é obrigatório.");

            // Persistido como numeric(5,4) — o limite de escala evita que um
            // valor com mais de 4 casas passe aqui e o banco arredonde
            // silenciosamente após o reload (mesma lição do Peso de EtapaProcesso).
            item.RuleFor(d => d.Pr)
                .PrecisionScale(5, 4, ignoreTrailingZeros: false)
                .WithMessage("PR deve ter no máximo 4 casas decimais.");

            item.RuleFor(d => d.RegraDistribuicaoCodigo)
                .NotEmpty()
                .WithMessage("Código da regra de distribuição é obrigatório.");

            item.RuleFor(d => d.RegraDistribuicaoVersao)
                .NotEmpty()
                .WithMessage("Versão da regra de distribuição é obrigatória.");

            item.RuleFor(d => d.Quadro)
                .NotNull()
                .WithMessage("O quadro é obrigatório — envie lista vazia quando não houver quantidade a fixar.")
                .Must(quadro => quadro is null
                    || quadro.Where(static q => q is not null).Select(static q => q.ModalidadeId).Distinct().Count()
                        == quadro.Count(static q => q is not null))
                .WithMessage("O quadro não pode repetir o mesmo ModalidadeId.");

            // Rejeita item nulo no array antes das regras de campo — mesma proteção
            // de DistribuicaoVagas acima (sem isso o Must de duplicidade e as
            // ChildRules abaixo desreferenciariam o item, estourando como 500).
            item.RuleForEach(d => d.Quadro)
                .NotNull()
                .WithMessage("Item do quadro não pode ser nulo.");

            item.RuleForEach(d => d.Quadro).ChildRules(quantidade =>
            {
                quantidade.RuleFor(q => q.ModalidadeId)
                    .NotEmpty()
                    .WithMessage("ModalidadeId do quadro é obrigatório.");

                quantidade.RuleFor(q => q.Quantidade)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("A quantidade de vagas de uma modalidade não pode ser negativa.");
            });
        });
    }
}
