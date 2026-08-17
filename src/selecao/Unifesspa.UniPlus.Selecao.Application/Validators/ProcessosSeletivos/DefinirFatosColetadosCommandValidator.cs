namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

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
        RuleFor(x => x.Fatos)
            .NotNull()
            .WithMessage("Lista de fatos coletados é obrigatória (pode ser vazia).");

        RuleForEach(x => x.Fatos)
            .NotNull()
            .WithMessage("Item de fato coletado não pode ser nulo.");

        RuleForEach(x => x.Fatos).ChildRules(fato =>
        {
            // Ausência de pré-condição é null, nunca []. Uma lista externa vazia, uma cláusula
            // interna vazia ou uma condição nula deixariam a semântica DNF ambígua (um predicado
            // sem cláusula, ou uma cláusula sem condição, avaliaria falso — o oposto de "sem
            // pré-condição") ou fariam o handler desreferenciar um item nulo.
            fato.RuleFor(f => f.Precondicao)
                .Must(precondicao => precondicao is null
                    || (precondicao.Count > 0 && precondicao.All(static clausula =>
                        clausula is { Count: > 0 } && clausula.All(static condicao => condicao is not null))))
                .WithMessage("A pré-condição, quando presente, não pode ser uma lista vazia, conter cláusulas "
                    + "vazias ou condições nulas — a ausência de pré-condição é representada por null.");
        });
    }
}
