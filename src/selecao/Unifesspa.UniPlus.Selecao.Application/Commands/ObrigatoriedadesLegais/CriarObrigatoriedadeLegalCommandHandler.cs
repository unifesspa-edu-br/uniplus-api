namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="CriarObrigatoriedadeLegalCommand"/>.
/// Converte códigos de área para <c>AreaCodigo</c>, autoriza por RBAC
/// área-scoped (ADR-0057), checa colisão de <c>RegraCodigo</c>, chama a
/// factory canônica da entity e persiste via repositório com reconciliação
/// atômica da junction temporal (ADR-0060).
/// </summary>
public static class CriarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<HashSet<AreaCodigo>> areasResult = ConverterAreas(command.AreasDeInteresse);
        if (areasResult.IsFailure)
        {
            return Result<Guid>.Failure(areasResult.Error!);
        }

        Result<AreaCodigo?> proprietarioResult = ConverterProprietario(command.Proprietario);
        if (proprietarioResult.IsFailure)
        {
            return Result<Guid>.Failure(proprietarioResult.Error!);
        }

        Result authz = AreaScopedAuthorization.Autorizar(userContext, proprietarioResult.Value);
        if (authz.IsFailure)
        {
            return Result<Guid>.Failure(authz.Error!);
        }

        bool duplicado = await repository.ExisteRegraCodigoAtivoAsync(
            command.RegraCodigo,
            excluirId: null,
            cancellationToken).ConfigureAwait(false);
        if (duplicado)
        {
            return Result<Guid>.Failure(new DomainError(
                "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                $"Já existe regra ativa com RegraCodigo '{command.RegraCodigo}'."));
        }

        Result<ObrigatoriedadeLegal> regraResult = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: command.TipoEditalCodigo,
            categoria: command.Categoria,
            regraCodigo: command.RegraCodigo,
            predicado: command.Predicado,
            descricaoHumana: command.DescricaoHumana,
            baseLegal: command.BaseLegal,
            vigenciaInicio: command.VigenciaInicio,
            vigenciaFim: command.VigenciaFim,
            atoNormativoUrl: command.AtoNormativoUrl,
            portariaInternaCodigo: command.PortariaInternaCodigo,
            proprietario: proprietarioResult.Value,
            areasDeInteresse: areasResult.Value);
        if (regraResult.IsFailure)
        {
            return Result<Guid>.Failure(regraResult.Error!);
        }

        ObrigatoriedadeLegal regra = regraResult.Value!;

        await repository.AdicionarComBindingsAsync(
            regra,
            areasResult.Value!,
            timeProvider.GetUtcNow(),
            userContext.UserId ?? "system",
            cancellationToken).ConfigureAwait(false);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(regra.Id);
    }

    /// <summary>
    /// Converte cada string de área para <see cref="AreaCodigo"/> via
    /// <see cref="AreaCodigo.From"/>. Falha no primeiro inválido.
    /// </summary>
    internal static Result<HashSet<AreaCodigo>> ConverterAreas(IReadOnlySet<string> areas)
    {
        HashSet<AreaCodigo> convertidas = [];
        foreach (string raw in areas)
        {
            Result<AreaCodigo> resultado = AreaCodigo.From(raw);
            if (resultado.IsFailure)
            {
                return Result<HashSet<AreaCodigo>>.Failure(resultado.Error!);
            }
            convertidas.Add(resultado.Value);
        }
        return Result<HashSet<AreaCodigo>>.Success(convertidas);
    }

    internal static Result<AreaCodigo?> ConverterProprietario(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<AreaCodigo?>.Success(null);
        }

        Result<AreaCodigo> resultado = AreaCodigo.From(raw);
        return resultado.IsFailure
            ? Result<AreaCodigo?>.Failure(resultado.Error!)
            : Result<AreaCodigo?>.Success(resultado.Value);
    }
}
