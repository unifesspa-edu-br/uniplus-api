namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// Handler convention-based do <see cref="CriarObrigatoriedadeLegalCommand"/>.
/// Checa colisão de <c>RegraCodigo</c>, chama a factory canônica da entity e
/// persiste via repositório. A regra é cross-cutting por tipo de processo —
/// sem proprietário nem áreas de interesse.
/// </summary>
public static class CriarObrigatoriedadeLegalCommandHandler
{
    [NonTransactional]
    public static async Task<Result<Guid>> Handle(
        CriarObrigatoriedadeLegalCommand command,
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

        if (!await TipoProcessoEhUniversalOuAtivoAsync(command.TipoProcessoCodigo, tipoProcessoReader, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(TipoProcessoNaoEncontradoOuInativo(command.TipoProcessoCodigo));
        }

        // Issue #1071: uma obrigatoriedade nova só pode usar código de tipo de etapa ativo —
        // a validação de fronteira (shape) já garantiu que o predicado existe; aqui é a
        // checagem cross-módulo (existência + atividade) que só o handler pode fazer.
        //
        // O código é normalizado (Trim) ANTES de persistir, não só na consulta ao reader: se
        // guardássemos o valor com espaços supérfluos, a regra seria aceita na criação (o
        // reader normaliza a busca) mas nunca mais bateria em AvaliadorConformidadeLegal, que
        // compara por igualdade ordinal exata contra o código congelado da etapa.
        PredicadoObrigatoriedade predicado = command.Predicado;
        if (predicado is EtapaObrigatoria etapaObrigatoria)
        {
            string codigoNormalizado = etapaObrigatoria.TipoEtapaCodigo.Trim();
            if (!await TipoEtapaEhAtivoAsync(codigoNormalizado, tipoEtapaReader, cancellationToken).ConfigureAwait(false))
            {
                return Result<Guid>.Failure(TipoEtapaNaoEncontradoOuInativo(codigoNormalizado));
            }

            predicado = etapaObrigatoria with { TipoEtapaCodigo = codigoNormalizado };
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
        if (regraResult.IsFailure)
        {
            return Result<Guid>.Failure(regraResult.Error!);
        }

        ObrigatoriedadeLegal regra = regraResult.Value!;

        await repository.AdicionarAsync(regra, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint)
        {
            // Race entre ExisteRegraCodigoAtivoAsync e o INSERT (check-then-act):
            // a constraint UNIQUE parcial sobre regra_codigo/hash dispara 23505,
            // viramos 409 ProblemDetails consistente com o caminho não-race.
            // Filtro do `when` garante que outras exceções não-23505 propagam
            // intactas (não engolimos falhas inesperadas).
            if (UniqueConstraintViolation.IsRegraCodigoConflict(constraint))
            {
                return Result<Guid>.Failure(new DomainError(
                    "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                    $"Já existe regra ativa com RegraCodigo '{command.RegraCodigo}'."));
            }
            if (UniqueConstraintViolation.IsHashConflict(constraint))
            {
                return Result<Guid>.Failure(new DomainError(
                    "ObrigatoriedadeLegal.HashColisao",
                    "Já existe regra ativa com o mesmo conteúdo canônico."));
            }
            throw;
        }

        return Result<Guid>.Success(regra.Id);
    }

    internal static async Task<bool> TipoProcessoEhUniversalOuAtivoAsync(
        string codigo,
        ITipoProcessoReader tipoProcessoReader,
        CancellationToken cancellationToken) =>
        string.Equals(codigo?.Trim(), ObrigatoriedadeLegal.TipoProcessoUniversal, StringComparison.Ordinal)
        || await tipoProcessoReader.ObterAtivoPorCodigoAsync(codigo ?? string.Empty, cancellationToken).ConfigureAwait(false) is not null;

    internal static DomainError TipoProcessoNaoEncontradoOuInativo(string codigo) => new(
        "ObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo",
        $"Tipo de processo seletivo '{codigo?.Trim()}' não encontrado ou não está ativo.");

    internal static async Task<bool> TipoEtapaEhAtivoAsync(
        string codigo,
        ITipoEtapaReader tipoEtapaReader,
        CancellationToken cancellationToken) =>
        await tipoEtapaReader.ObterAtivoPorCodigoAsync(codigo ?? string.Empty, cancellationToken).ConfigureAwait(false) is not null;

    internal static DomainError TipoEtapaNaoEncontradoOuInativo(string codigo) => new(
        "ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo",
        $"Tipo de etapa '{codigo?.Trim()}' não encontrado ou não está ativo.");
}
