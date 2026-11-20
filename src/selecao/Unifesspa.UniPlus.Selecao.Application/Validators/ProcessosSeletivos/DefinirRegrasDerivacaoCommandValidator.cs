namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Confere o que o agregado e a leitura das condições não alcançam (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; as listas de configurações e de regras, e cada item delas, não podem ser nulos, porque o
/// código que as lê as percorre sem checagem defensiva; e o predicado <c>quando</c> não pode ser uma lista externa
/// vazia, pois a regra incondicional tem <c>quando</c> nulo. O restante da forma é recusado adiante:
/// código do fato, presença de regras, unicidade de ordem e regras com e sem código por
/// <c>ConfiguracaoDerivacaoFato.ValidarFormaBasica</c>; ordem e contribuição por
/// <c>RegraDerivacaoConfigurada.ValidarFormaBasica</c>; cláusula vazia e condição nula por
/// <c>EntradaDeRegras.Condicoes</c>.
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

        RuleForEach(x => x.Configuracoes).SetValidator(new ConfiguracaoDerivacaoInputValidator());
    }
}

internal sealed class ConfiguracaoDerivacaoInputValidator : AbstractValidator<ConfiguracaoDerivacaoInput>
{
    public ConfiguracaoDerivacaoInputValidator()
    {
        // RuleForEach só reporta item nulo dentro da lista; a lista nula exige o NotNull explícito, senão
        // um payload com Regras nulo estouraria ao ser percorrido.
        RuleFor(c => c.Regras)
            .NotNull()
            .WithMessage("Lista de regras de derivação é obrigatória.");

        RuleForEach(c => c.Regras)
            .NotNull()
            .WithMessage("Item de regra de derivação não pode ser nulo.");

        RuleForEach(c => c.Regras).SetValidator(new RegraDerivacaoInputValidator());
    }
}

internal sealed class RegraDerivacaoInputValidator : AbstractValidator<RegraDerivacaoInput>
{
    public RegraDerivacaoInputValidator()
    {
        // A regra incondicional tem Quando nulo; a lista externa vazia deixaria o predicado ambíguo.
        RuleFor(r => r.Quando)
            .Must(static quando => quando is null || quando.Count > 0)
            .WithMessage("O predicado 'quando', quando presente, não pode ser uma lista vazia — "
                + "uma regra incondicional tem 'quando' nulo.");
    }
}
