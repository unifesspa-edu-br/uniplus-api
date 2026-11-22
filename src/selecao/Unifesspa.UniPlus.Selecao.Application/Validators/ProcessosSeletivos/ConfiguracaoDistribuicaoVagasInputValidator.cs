namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Regras da entrada de uma distribuição de vagas, compartilhadas por
/// <see cref="DefinirDistribuicaoVagasCommandValidator"/> e pelo validador da simulação: o comando que persiste
/// e a query que só simula aceitam o mesmo <see cref="ConfiguracaoDistribuicaoVagasInput"/> e recusam o mesmo
/// pedido malformado. A quantidade de vagas não é conferida aqui: a regra é do domínio.
/// </summary>
public sealed class ConfiguracaoDistribuicaoVagasInputValidator : AbstractValidator<ConfiguracaoDistribuicaoVagasInput>
{
    public ConfiguracaoDistribuicaoVagasInputValidator()
    {
        RegrasDaOferta();
        RegrasDasRegras();
        RegrasDasModalidades();
        RegrasDoQuadro();
    }

    private void RegrasDaOferta()
    {
        RuleFor(d => d.OfertaCursoId)
            .NotEmpty()
            .WithMessage("OfertaCursoId é obrigatório.");

    }

    private void RegrasDasRegras()
    {
        RuleFor(d => d.RegraDistribuicaoCodigo)
            .NotEmpty()
            .WithMessage("Código da regra de distribuição é obrigatório.");

        RuleFor(d => d.RegraDistribuicaoVersao)
            .NotEmpty()
            .WithMessage("Versão da regra de distribuição é obrigatória.");
    }

    // NotNull (não NotEmpty): a lista vazia é responsabilidade do domínio
    // (ConfiguracaoDistribuicaoVagas.ModalidadesVazias, ADR-0125) — mas a
    // nulidade em si precisa ser barrada aqui, sem isso o handler
    // desreferenciaria ModalidadeIds (Contains/foreach), estourando como 500.
    private void RegrasDasModalidades() =>
        RuleFor(d => d.ModalidadeIds)
            .NotNull()
            .WithMessage("O campo ModalidadeIds é obrigatório (pode ser uma lista vazia).");

    // Rejeita item nulo no array antes das regras de campo — sem isso o Must de duplicidade e as ChildRules
    // desreferenciariam o item, estourando como 500.
    private void RegrasDoQuadro()
    {
        RuleFor(d => d.Quadro)
            .NotNull()
            .WithMessage("O quadro é obrigatório — envie lista vazia quando não houver quantidade a fixar.")
            .Must(quadro => quadro is null
                || quadro.Where(static q => q is not null).Select(static q => q.ModalidadeId).Distinct().Count()
                    == quadro.Count(static q => q is not null))
            .WithMessage("O quadro não pode repetir o mesmo ModalidadeId.");

        RuleForEach(d => d.Quadro)
            .NotNull()
            .WithMessage("Item do quadro não pode ser nulo.");

        RuleForEach(d => d.Quadro).ChildRules(RegrasDaQuantidadeDeVagas);
    }

    private static void RegrasDaQuantidadeDeVagas(InlineValidator<QuantidadeVagaInput> quantidade) =>
        quantidade.RuleFor(q => q.ModalidadeId)
            .NotEmpty()
            .WithMessage("ModalidadeId do quadro é obrigatório.");
}
