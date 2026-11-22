namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Trimado pela ADR-0125: Nome (obrigatoriedade + tamanho), Carater (obrigatoriedade +
/// enum válido), Peso (positividade), NotaMinima (não negatividade), Ordem (positividade) e a
/// coerência da escala (nota máxima, passo) saíram daqui — <see cref="Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas.ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica"/>
/// os cobre, acumulando, na primeira passada do <c>DefinirEtapasCommandHandler</c>. O que
/// permanece a validação de <see cref="EtapaProcessoInput.TipoEtapaOrigemId"/> e da estrutura
/// necessária para o handler processar o pedido.
/// </summary>
public sealed class DefinirEtapasCommandValidator : AbstractValidator<DefinirEtapasCommand>
{
    public DefinirEtapasCommandValidator()
    {
        RegrasDoCorpo();

        // Rejeita item nulo no array (ex.: `[null]`) antes das regras de campo — sem isso o ChildRules não gera falha para o
        // elemento nulo e o handler desreferenciaria a etapa, estourando como 500.
        RuleForEach(x => x.Etapas)
            .NotNull()
            .WithMessage("Item de etapa não pode ser nulo.");

        RuleForEach(x => x.Etapas).ChildRules(RegrasDaEtapa);
    }

    private void RegrasDoCorpo()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Lista vazia é um payload válido (processo sem prova, ex. SiSU) — só a nulidade do array em si é recusada aqui.
        RuleFor(x => x.Etapas)
            .NotNull()
            .WithMessage("O campo Etapas é obrigatório (pode ser uma lista vazia).");
    }

    private static void RegrasDaEtapa(InlineValidator<EtapaProcessoInput> etapa)
    {
        // Toda etapa declara o tipo de origem — sem produção em nenhum ambiente, não há transição com tipo opcional.
        etapa.RuleFor(e => e.TipoEtapaOrigemId)
            .NotEmpty()
            .WithMessage("TipoEtapaOrigemId é obrigatório.");

        ItensDeAvaliacao(etapa);
        EscalaDeAtendimento(etapa);
        ColecoesNaoNulasDaEtapa(etapa);
        ItensNaoNulosDaEtapa(etapa);
        ProdutosDaEtapa(etapa);
    }

    private static void ItensDeAvaliacao(InlineValidator<EtapaProcessoInput> etapa) =>
        etapa.RuleForEach(e => e.ItensDeAvaliacao)
            .NotNull()
            .WithMessage("Item de avaliação da etapa não pode ser nulo.");

    private static void EscalaDeAtendimento(InlineValidator<EtapaProcessoInput> etapa) =>
        etapa.RuleForEach(e => e.EscalaDeAtendimento)
            .NotNull()
            .WithMessage("Nível da escala de atendimento não pode ser nulo.");

    // A COLEÇÃO em si não pode ser nula. `[JsonRequired]` recusa a carga que omite a chave, mas `"produtos": null` atravessa a
    // desserialização e chega até aqui — o tipo não-anulável não a barra, e a exigência implícita do `[ApiController]` não
    // alcança propriedade de item de coleção no corpo. Como a gravação substitui a coleção inteira, um nulo apagaria em silêncio
    // o que a etapa declara. A lista vazia continua válida: é a declaração explícita de que não há nenhum. Item nulo nas
    // coleções aninhadas passa incólume pelo ChildRules, e o handler o desreferencia — o papel do produto, o tipo da banca, a
    // evento que inicia o prazo do recurso — estourando como 500.
    private static void ColecoesNaoNulasDaEtapa(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleFor(e => e.Produtos)
            .NotNull()
            .WithMessage("Declare os produtos da etapa: lista vazia se ela não publica nenhum.");

        etapa.RuleFor(e => e.Bancas)
            .NotNull()
            .WithMessage("Declare as bancas da etapa: lista vazia se ela não requer nenhuma.");

        etapa.RuleFor(e => e.Recursos)
            .NotNull()
            .WithMessage("Declare a configuração do recurso da etapa: lista vazia se ela não admite recurso.");
    }

    private static void ItensNaoNulosDaEtapa(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleForEach(e => e.Produtos)
            .NotNull()
            .WithMessage("Item de produto da etapa não pode ser nulo.");

        etapa.RuleForEach(e => e.Bancas)
            .NotNull()
            .WithMessage("Item de banca da etapa não pode ser nulo.");

        etapa.RuleForEach(e => e.Recursos)
            .NotNull()
            .WithMessage("Item da configuração do recurso da etapa não pode ser nulo.");
    }

    // Forma do item, e só ela — mesma regra que os produtos da fase já tinham. Sem ela, o código vazio chega a
    // ProdutoDaEtapa.Criar, cuja guarda de argumento LANÇA: um corpo malformado viraria 500 em vez da recusa de validação que
    // nomeia o campo. Que o papel seja declarável e que o ato exista no catálogo continuam sendo resolução do handler.
    private static void ProdutosDaEtapa(InlineValidator<EtapaProcessoInput> etapa) =>
        etapa.RuleForEach(e => e.Produtos).ChildRules(produto =>
            produto.RuleFor(p => p.AtoCodigo)
                .NotEmpty()
                .WithMessage("O código do ato do produto é obrigatório."));
}
