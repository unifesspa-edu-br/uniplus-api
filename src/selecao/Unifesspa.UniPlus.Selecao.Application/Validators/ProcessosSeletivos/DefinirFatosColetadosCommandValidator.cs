namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Duas checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; a lista de fatos e cada item não podem ser nulos — o handler
/// desreferencia sem checagem defensiva. FatoCodigo/Ordem/Rotulo/TipoRenderizacao (via a
/// coerência de <c>FatoColetado.Criar</c>) e a autorreferência de pré-condição já têm
/// equivalente completo no domínio e ficaram fora daqui. A forma da pré-condição
/// (<c>Must</c> abaixo) permanece: não tem equivalente em <c>FatoColetado.Criar</c>, que só
/// recebe a lista já montada — a montagem e a checagem de forma bruta acontecem na
/// Application (<c>DefinirFatosColetadosCommandHandler</c>), que resolve o vocabulário
/// cross-módulo.
/// </summary>
public sealed class DefinirFatosColetadosCommandValidator : AbstractValidator<DefinirFatosColetadosCommand>
{
    public DefinirFatosColetadosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Lista obrigatória, mas pode ser vazia — vazia zera a coleta do processo. Sem esta
        // regra um payload nulo chegaria ao handler e estouraria no foreach em vez de 400.
        RuleFor(x => x.Itens)
            .NotNull()
            .WithMessage("Lista de itens do formulário é obrigatória (pode ser vazia).");

        // Acima do teto — itens, grupos e campos de grupo — o pedido é recusado inteiro pela
        // quantidade, sem um erro por item nem por grupo.
        When(static x => x.Itens is not null && x.QuantidadeNoTeto <= FormaDoItem.MaximoDeItens, () =>
        {
            RuleForEach(x => x.Itens)
                .NotNull()
                .WithMessage("Item de fato coletado não pode ser nulo.");

            RuleForEach(x => x.Itens).ChildRules(static fato => RegrasDoCampo(fato));
        });

        // Os grupos são opcionais; quando vêm, nenhum é nulo, cada um traz a lista de campos, e os
        // campos seguem a forma dos itens.
        When(static x => x.Grupos is not null && x.QuantidadeNoTeto <= FormaDoItem.MaximoDeItens, () =>
        {
            RuleForEach(x => x.Grupos)
                .NotNull()
                .WithMessage("Grupo repetível não pode ser nulo.");

            RuleForEach(x => x.Grupos).ChildRules(static grupo =>
            {
                grupo.RuleFor(g => g.Subitens)
                    .NotNull()
                    .WithMessage("A lista de campos do grupo é obrigatória.");
                grupo.RuleFor(g => g.Exibicao)
                    .Must(PredicadoBemFormado)
                    .WithMessage(MensagemDoPredicado);
                grupo.RuleFor(g => g.PredicadoObrigatoriedade)
                    .Must(PredicadoBemFormado)
                    .WithMessage(MensagemDoPredicado);
                grupo.RuleForEach(g => g.Subitens)
                    .NotNull()
                    .WithMessage("Campo do grupo não pode ser nulo.");
                grupo.RuleForEach(g => g.Subitens).ChildRules(static campo => RegrasDoCampo(campo));
            });
        });
    }

    private const string MensagemDoPredicado = "O predicado, quando presente, não pode ser uma lista vazia, conter cláusulas "
        + "vazias ou condições nulas — a ausência de condição é representada por null.";

    // Ausência de pré-condição é null, nunca []. Uma lista externa vazia, uma cláusula interna vazia
    // ou uma condição nula deixariam a semântica DNF ambígua (um predicado sem cláusula, ou uma
    // cláusula sem condição, avaliaria falso — o oposto de "sem pré-condição") ou fariam o handler
    // desreferenciar um item nulo.
    private static void RegrasDoCampo(InlineValidator<FatoColetadoInput> fato)
    {
        fato.RuleFor(f => f.Precondicao)
            .Must(PredicadoBemFormado)
            .WithMessage(MensagemDoPredicado);
        fato.RuleFor(f => f.PredicadoObrigatoriedade)
            .Must(PredicadoBemFormado)
            .WithMessage(MensagemDoPredicado);
    }

    private static bool PredicadoBemFormado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado) =>
        predicado is null
        || (predicado.Count > 0 && predicado.All(static clausula =>
            clausula is { Count: > 0 } && clausula.All(static condicao => condicao is not null)));
}
