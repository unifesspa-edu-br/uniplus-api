namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarFaseCanonicaCommand"/>. Antes de buscar a fase, recusa o que o
/// corpo decide sem o código: os campos comuns, a ausência de sinalizador e a exclusividade entre
/// coletar inscrição e coletar solicitação de isenção, para a validação vencer a inexistência
/// (ADR-0125). Encontrada a fase,
/// <c>Atualizar</c> confere o corpo inteiro contra o código gravado, que o comando não traz por
/// ser imutável: agrupamento de etapas, complementação documental e marca de solicitação de
/// isenção. A troca da unidade sugerida é conferida no
/// cadastro de unidades e acumula com as recusas do agregado; havendo recusa, o que a edição já
/// mudou na fase rastreada é descartado, porque o SaveChangesAsync do outbox roda depois do
/// handler mesmo na falha. A fase não é referenciada por chave estrangeira.
/// </summary>
public static class AtualizarFaseCanonicaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarFaseCanonicaCommand command,
        IFaseCanonicaRepository repository,
        IUnidadeReader unidadeReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result recusaDoQueNaoDependeDoCodigo = ValidarOQueNaoDependeDoCodigo(command);
        if (recusaDoQueNaoDependeDoCodigo.IsFailure)
        {
            return recusaDoQueNaoDependeDoCodigo;
        }
        FaseCanonica? fase = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        return fase is null
            ? Result.Failure(new DomainError(FaseCanonicaErrorCodes.NaoEncontrada, "Fase canônica não encontrada."))
            : await AplicarEdicaoAsync(fase, command, unidadeReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result ValidarOQueNaoDependeDoCodigo(AtualizarFaseCanonicaCommand command) =>
        FaseCanonica.ValidarOQueNaoDependeDoCodigo(
            command.Nome, command.Descricao, command.BaseLegal, command.OrigemData, command.Sinalizadores());

    private static async Task<Result> AplicarEdicaoAsync(
        FaseCanonica fase,
        AtualizarFaseCanonicaCommand command,
        IUnidadeReader unidadeReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<UnidadeSugerida?> unidade = await UnidadeSugeridaResolver
            .ResolverTrocaAsync(fase.UnidadeSugerida, command.UnidadeSugeridaId, unidadeReader, cancellationToken)
            .ConfigureAwait(false);
        Result campos = AtualizarCampos(fase, command);
        if (campos.IsFailure || unidade.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.ValidationFailure([.. campos.Errors, .. unidade.Errors]);
        }

        fase.DefinirUnidadeSugerida(unidade.Value);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static Result AtualizarCampos(FaseCanonica fase, AtualizarFaseCanonicaCommand command) =>
        fase.Atualizar(
            command.Nome,
            command.Descricao,
            command.BaseLegal,
            command.OrigemData,
            command.Sinalizadores());
}
