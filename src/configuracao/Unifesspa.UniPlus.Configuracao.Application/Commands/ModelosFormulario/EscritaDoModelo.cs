namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que a criação e a edição do modelo têm em comum: ler o conteúdo, conferi-lo contra o catálogo
/// de fatos, de termos e de tipos de processo, e gravar, traduzindo as corridas de índice único e
/// de concorrência otimista (xmin) em recusa.
/// </summary>
internal static class EscritaDoModelo
{
    private const string IndiceUnicoDoCodigo = "ix_modelos_formulario_codigo";

    /// <summary>
    /// O conteúdo lido e as recusas da leitura e do catálogo, acumuladas (ADR-0125). O tipo de
    /// processo é conferido só quando muda: desativar o tipo recusa vínculo novo, e o modelo que já
    /// o usava continua com ele.
    /// </summary>
    public static async Task<(ConteudoLido Conteudo, CatalogoDoModelo Catalogo, List<FieldError> Erros)> LerEConferirAsync(
        ConteudoDoModeloInput? entrada,
        string? tipoProcessoCodigo,
        string? tipoProcessoAnterior,
        VinculosDoModelo existentes,
        IFatoCandidatoRepository fatoRepository,
        ITermoConsentimentoReader termoReader,
        ITipoProcessoReader tipoProcessoReader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        CatalogoDoModelo catalogo = CatalogoDoModelo.De(fatos);
        ConteudoLido conteudo = ConteudoLido.Ler(entrada, catalogo.Formatos);

        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoReader
            .ListarVersoesAsync([.. conteudo.Termos.Select(static t => t.Termo.VersaoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        List<FieldError> erros = [.. ModeloFormulario.NoConteudo(
            conteudo.Erros.Concat(ConferenciaDoModelo.Conferir(conteudo, catalogo, versoes.ToDictionary(static v => v.VersaoId), existentes)))];

        // O texto que o banco não grava não é consultado: a recusa dele é a do modelo. O código é
        // comparado aparado, a forma em que o modelo e o cadastro de tipo de processo o gravam.
        string? tipo = string.IsNullOrWhiteSpace(tipoProcessoCodigo) || !ModeloFormulario.EhGravavel(tipoProcessoCodigo)
            ? null
            : tipoProcessoCodigo.Trim();
        if (tipo is not null
            && !string.Equals(tipo, tipoProcessoAnterior, StringComparison.Ordinal)
            && await tipoProcessoReader.ObterAtivoPorCodigoAsync(tipo, cancellationToken).ConfigureAwait(false) is null)
        {
            erros.Add(new("tipoProcessoCodigo", new DomainError(
                ModeloFormularioErrorCodes.TipoProcessoInexistente, $"O tipo de processo '{tipo}' não existe ou está desativado.")));
        }

        return (conteudo, catalogo, erros);
    }

    /// <summary>Grava o modelo novo: o código é único entre todos os modelos, desativados inclusive.</summary>
    public static async Task<Result<Guid>> GravarNovoAsync(
        ModeloFormulario modelo, IModeloFormularioRepository repository, IConfiguracaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteAsync(modelo.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        await repository.AdicionarAsync(modelo, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflito(exception, IndiceUnicoDoCodigo))
        {
            // Sem descartar, a inserção continua rastreada e o SaveChangesAsync do outbox a repete
            // fora deste catch, e o 409 vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        return Result<Guid>.Success(modelo.Id);
    }

    /// <summary>Carrega o modelo, aplica a mutação e grava, traduzindo a corrida de concorrência em conflito.</summary>
    public static async Task<Result> MutarAsync(
        Guid id,
        Func<ModeloFormulario, Task<Result>> mutacao,
        IModeloFormularioRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ModeloFormulario? modelo = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (modelo is null)
        {
            return Result.Failure(new DomainError(ModeloFormularioErrorCodes.NaoEncontrado, "Modelo de formulário não encontrado."));
        }

        Result resultado = await mutacao(modelo).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            // A edição recusada pelo catálogo pode já ter mudado o modelo rastreado; sem descartar,
            // o SaveChangesAsync do outbox o gravaria mesmo com a recusa.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return resultado;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Os endpoints têm Idempotency-Key (ADR-0119): captura local e descarte, para o
            // SaveChangesAsync do outbox não reencontrar a entidade modificada.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                ModeloFormularioErrorCodes.ConflitoDeConcorrencia, "Este modelo de formulário foi alterado concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }

    private static DomainError CodigoJaExiste() => new(
        ModeloFormularioErrorCodes.CodigoJaExiste, "Já existe um modelo de formulário com o código informado.");
}
