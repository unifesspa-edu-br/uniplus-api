namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Só o que é estrutural do pedido. A lista de relatórios nula ou omitida, a forma e o tamanho do código e da versão do leiaute, o leiaute repetido e o item nulo são recusados pelo domínio (422), depois
/// da recusa de origem, que vem primeiro.
/// </summary>
public sealed class DefinirImportacaoDoResultadoCommandValidator : AbstractValidator<DefinirImportacaoDoResultadoCommand>
{
    public DefinirImportacaoDoResultadoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId).NotEmpty().WithMessage("O processo seletivo é obrigatório.");
    }
}
