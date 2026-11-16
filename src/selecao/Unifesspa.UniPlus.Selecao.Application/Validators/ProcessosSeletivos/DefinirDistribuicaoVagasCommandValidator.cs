namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Trimado pela ADR-0125 só do que <see cref="Unifesspa.UniPlus.Selecao.Domain.Entities.ConfiguracaoDistribuicaoVagas.ValidarFormaBasica"/>
/// cobre de forma incondicional: as vagas ofertadas base (positividade), o percentual da reserva legal (faixa [0,5; 1]) e a lista de
/// distribuições vazia, que <c>ProcessoSeletivo.DistribuicaoVagasVazia</c> recusa com erro
/// nomeado — mantê-la aqui tornava a causa de domínio inalcançável por um cliente HTTP.
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

        // Rejeita item nulo no array antes das regras de campo — sem isso o handler desreferenciaria o item,
        // estourando como 500.
        RuleForEach(x => x.DistribuicaoVagas)
            .NotNull()
            .WithMessage("Item de distribuição de vagas não pode ser nulo.");

        // As regras de campo do item vivem em ConfiguracaoDistribuicaoVagasInputValidator, que a query de simulação
        // também usa: ela aceita o mesmo ConfiguracaoDistribuicaoVagasInput e precisa recusar o mesmo malformado.
        RuleForEach(x => x.DistribuicaoVagas).SetValidator(new ConfiguracaoDistribuicaoVagasInputValidator());
    }
}
