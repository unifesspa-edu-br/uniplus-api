namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Duas checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; cada item, grupo e campo de grupo não pode ser nulo — o handler
/// desreferencia sem checagem defensiva. A lista de itens e a lista de campos do grupo nulas ou
/// omitidas não são conferidas aqui: a leitura do corpo as recusa com 400 e o código de campo
/// obrigatório ausente, mas só para a propriedade, nunca para o elemento nulo da lista. O código do
/// dado do candidato, a ordem, o rótulo, o tipo de campo (via a coerência de
/// <c>FatoColetado.Criar</c>) e a autorreferência de exibição já têm equivalente completo no domínio e ficaram fora daqui. A forma do predicado
/// (<c>PredicadoBemFormado</c> abaixo) permanece: não tem equivalente em <c>FatoColetado.Criar</c>, que só
/// recebe a lista já montada — a montagem e a checagem de forma bruta acontecem na
/// Application (<c>DefinirFatosColetadosCommandHandler</c>), que resolve o vocabulário
/// cross-módulo.
/// </summary>
public sealed class DefinirFatosColetadosCommandValidator : AbstractValidator<DefinirFatosColetadosCommand>
{
    private const string MensagemDoPredicado = "O predicado, quando presente, não pode ser uma lista vazia, conter cláusulas "
        + "vazias ou condições nulas — a ausência de condição é representada por null.";

    public DefinirFatosColetadosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Acima do teto — itens, grupos e campos de grupo — o pedido é recusado inteiro pela
        // quantidade, sem um erro por item nem por grupo.
        When(static x => x.QuantidadeNoTeto <= FormaDoItem.MaximoDeItens, RegrasDosItens);
        When(static x => x.Grupos is not null && x.QuantidadeNoTeto <= FormaDoItem.MaximoDeItens, RegrasDosGrupos);
    }

    private void RegrasDosItens()
    {
        RuleForEach(x => x.Itens)
            .NotNull()
            .WithMessage("Item de dado coletado do candidato não pode ser nulo.");

        RuleForEach(x => x.Itens).ChildRules(static item => PredicadosDoItem(item));
    }

    // Os grupos são opcionais; quando vêm, nenhum é nulo e os campos seguem a forma dos itens.
    private void RegrasDosGrupos()
    {
        RuleForEach(x => x.Grupos)
            .NotNull()
            .WithMessage("Grupo repetível não pode ser nulo.");

        RuleForEach(x => x.Grupos).ChildRules(static grupo => RegrasDoGrupo(grupo));
    }

    private static void RegrasDoGrupo(InlineValidator<GrupoColetadoInput> grupo)
    {
        grupo.RuleFor(g => g.Regras).ChildRules(static regras => PredicadosDoGrupo(regras));
        grupo.RuleForEach(g => g.Subitens)
            .NotNull()
            .WithMessage("Campo do grupo não pode ser nulo.");
        grupo.RuleForEach(g => g.Subitens).ChildRules(static campo => PredicadosDoItem(campo));
    }

    // Ausência de exibição é null, nunca []. Uma lista externa vazia, uma cláusula interna vazia
    // ou uma condição nula deixariam a semântica DNF ambígua (um predicado sem cláusula, ou uma
    // cláusula sem condição, avaliaria falso — o oposto de "sem condição") ou fariam o handler
    // desreferenciar um item nulo. As regras e a obrigatoriedade ausentes não são conferidas aqui: o domínio as recusa;
    // os filhos de ChildRules só rodam com o pai presente, por isso o `!`.
    private static void PredicadosDoItem(InlineValidator<FatoColetadoInput> item) =>
        item.RuleFor(f => f.Regras).ChildRules(static regras =>
        {
            regras.RuleFor(r => r!.Exibicao)
                .Must(PredicadoBemFormado)
                .WithMessage(MensagemDoPredicado);
            regras.RuleFor(r => r!.Obrigatoriedade).ChildRules(static obrigatoriedade => PredicadoDaObrigatoriedade(obrigatoriedade));
        });

    private static void PredicadosDoGrupo(InlineValidator<ExibicaoEObrigatoriedadeInput?> regras)
    {
        regras.RuleFor(r => r!.Exibicao)
            .Must(PredicadoBemFormado)
            .WithMessage(MensagemDoPredicado);
        regras.RuleFor(r => r!.Obrigatoriedade).ChildRules(static obrigatoriedade => PredicadoDaObrigatoriedade(obrigatoriedade));
    }

    private static void PredicadoDaObrigatoriedade(InlineValidator<ObrigatoriedadeInput?> obrigatoriedade) =>
        obrigatoriedade.RuleFor(o => o!.Predicado)
            .Must(PredicadoBemFormado)
            .WithMessage(MensagemDoPredicado);

    private static bool PredicadoBemFormado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado) =>
        predicado is null
        || (predicado.Count > 0 && predicado.All(static clausula =>
            clausula is { Count: > 0 } && clausula.All(static condicao => condicao is not null)));
}
