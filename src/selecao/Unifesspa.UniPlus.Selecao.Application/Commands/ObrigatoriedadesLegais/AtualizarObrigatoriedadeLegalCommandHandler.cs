namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// Handler convention-based do <see cref="AtualizarObrigatoriedadeLegalCommand"/>.
/// Semântica full-replace (ADR-0058 Emenda 1): o caller deve repassar todos os
/// campos. A regra é cross-cutting por tipo de processo — sem proprietário nem
/// áreas de interesse.
/// </summary>
public static class AtualizarObrigatoriedadeLegalCommandHandler
{
    [NonTransactional]
    public static async Task<Result> Handle(
        AtualizarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        ITipoProcessoReader tipoProcessoReader,
        ITipoEtapaReader tipoEtapaReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tipoProcessoReader);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ObrigatoriedadeLegal? regra = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.NaoEncontrada",
                $"ObrigatoriedadeLegal {command.Id} não encontrada."));
        }

        if (!await CriarObrigatoriedadeLegalCommandHandler
                .TipoProcessoEhUniversalOuAtivoAsync(command.TipoProcessoCodigo, tipoProcessoReader, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure(CriarObrigatoriedadeLegalCommandHandler.TipoProcessoNaoEncontradoOuInativo(command.TipoProcessoCodigo));
        }

        // Mesma normalização do Criar: o código é aparado ANTES de persistir, não só na
        // consulta ao reader — do contrário a regra aceita na atualização nunca mais bateria
        // em AvaliadorConformidadeLegal (igualdade ordinal exata contra o código congelado).
        PredicadoObrigatoriedade predicado = command.Predicado;
        if (predicado is EtapaObrigatoria etapaObrigatoria)
        {
            // Mesma defesa do Criar: TipoEtapaCodigo pode chegar null em runtime apesar do tipo
            // non-nullable (System.Text.Json não impõe NRT; FluentValidation só valida Predicado
            // não-nulo, não os campos do subtipo) — sem o '?.' seria NullReferenceException (500)
            // em vez de 422 pelo reader logo abaixo.
            string codigoNormalizado = etapaObrigatoria.TipoEtapaCodigo?.Trim() ?? string.Empty;
            if (!await CriarObrigatoriedadeLegalCommandHandler
                    .TipoEtapaEhAtivoAsync(codigoNormalizado, tipoEtapaReader, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Result.Failure(CriarObrigatoriedadeLegalCommandHandler.TipoEtapaNaoEncontradoOuInativo(codigoNormalizado));
            }

            predicado = etapaObrigatoria with { TipoEtapaCodigo = codigoNormalizado };
        }

        bool duplicado = await repository.ExisteRegraCodigoAtivoAsync(
            command.RegraCodigo,
            excluirId: command.Id,
            cancellationToken).ConfigureAwait(false);
        if (duplicado)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                $"Já existe outra regra ativa com RegraCodigo '{command.RegraCodigo}'."));
        }

        Result atualizado = regra.Atualizar(
            tipoProcessoCodigo: command.TipoProcessoCodigo,
            categoria: command.Categoria,
            regraCodigo: command.RegraCodigo,
            predicado: predicado,
            descricaoHumana: command.DescricaoHumana,
            baseLegal: command.BaseLegal,
            vigenciaInicio: command.VigenciaInicio,
            vigenciaFim: command.VigenciaFim,
            atoNormativoUrl: command.AtoNormativoUrl,
            portariaInternaCodigo: command.PortariaInternaCodigo);
        if (atualizado.IsFailure)
        {
            return atualizado;
        }

        repository.Atualizar(regra);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint)
        {
            // Mesmo tratamento do Criar: race entre o ExisteRegraCodigoAtivoAsync
            // e o UPDATE (caller troca RegraCodigo para um valor que outra escrita
            // concorrente acabou de assumir) dispara a constraint do banco.
            if (UniqueConstraintViolation.IsRegraCodigoConflict(constraint))
            {
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                    $"Já existe outra regra ativa com RegraCodigo '{command.RegraCodigo}'."));
            }
            if (UniqueConstraintViolation.IsHashConflict(constraint))
            {
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.HashColisao",
                    "Já existe regra ativa com o mesmo conteúdo canônico após esta atualização."));
            }
            throw;
        }

        return Result.Success();
    }
}
