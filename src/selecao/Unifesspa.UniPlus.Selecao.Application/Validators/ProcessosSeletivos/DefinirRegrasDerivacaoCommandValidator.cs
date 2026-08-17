namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Três checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; as listas de configurações e de regras — e cada item delas — não
/// podem ser nulas, porque o handler as desreferencia sem checagem defensiva
/// (<c>configInput.Regras.Count</c> na primeira passada, achado de revisão: <c>RuleForEach</c>
/// não reporta a coleção nula em si, só seus itens — a ausência dela exige o <c>NotNull</c>
/// explícito abaixo, distinto do <c>NotEmpty</c> removido). Código do fato/presença de
/// regras/unicidade de ordem (via <c>ConfiguracaoDerivacaoFato.ValidarFormaBasica</c>) e
/// ordem/contribuição (via <c>RegraDerivacaoConfigurada.ValidarFormaBasica</c>) já têm
/// equivalente completo no domínio e ficaram fora daqui. A forma do predicado <c>quando</c>
/// permanece: não tem equivalente em <c>RegraDerivacaoConfigurada.Criar</c>, que só recebe a
/// lista já montada — a montagem e a checagem de forma bruta acontecem na Application
/// (<c>DefinirRegrasDerivacaoCommandHandler</c>), que resolve o vocabulário cross-módulo.
/// </summary>
public sealed class DefinirRegrasDerivacaoCommandValidator : AbstractValidator<DefinirRegrasDerivacaoCommand>
{
    public DefinirRegrasDerivacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Lista obrigatória, mas pode ser vazia — vazia zera as regras de derivação do processo.
        RuleFor(x => x.Configuracoes)
            .NotNull()
            .WithMessage("Lista de configurações de derivação é obrigatória (pode ser vazia).");

        RuleForEach(x => x.Configuracoes)
            .NotNull()
            .WithMessage("Item de configuração de derivação não pode ser nulo.");

        RuleForEach(x => x.Configuracoes).ChildRules(config =>
        {
            // RuleForEach abaixo só reporta ITEM nulo dentro da lista — a lista em si sendo nula
            // passa batido por ele (achado de revisão): sem este NotNull explícito, um payload
            // com Regras nulo chegaria à primeira passada do handler e estouraria em
            // configInput.Regras.Count antes de qualquer validação rodar.
            config.RuleFor(c => c.Regras)
                .NotNull()
                .WithMessage("Lista de regras de derivação é obrigatória.");

            config.RuleForEach(c => c.Regras)
                .NotNull()
                .WithMessage("Item de regra de derivação não pode ser nulo.");

            config.RuleForEach(c => c.Regras).ChildRules(regra =>
            {
                // Regra âncora (incondicional) tem Quando null. Uma lista externa vazia, uma
                // cláusula interna vazia ou uma condição nula deixariam a semântica DNF ambígua ou
                // fariam o handler desreferenciar um item nulo — a âncora é representada por null.
                regra.RuleFor(r => r.Quando)
                    .Must(quando => quando is null
                        || (quando.Count > 0 && quando.All(static clausula =>
                            clausula is { Count: > 0 } && clausula.All(static condicao => condicao is not null))))
                    .WithMessage("O predicado 'quando', quando presente, não pode ser uma lista vazia, conter "
                        + "cláusulas vazias ou condições nulas — uma regra incondicional (âncora) tem 'quando' null.");
            });
        });
    }
}
