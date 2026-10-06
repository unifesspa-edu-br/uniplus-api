namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
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
    /// O conteúdo lido e as recusas da leitura e do catálogo, acumuladas (ADR-0125). O modelo de
    /// inscrição coleta o conjunto básico na seção reservada, como o formulário do processo: o item
    /// básico omitido entra como está gravado no modelo, e o enviado tem de ser igual a ele. O tipo
    /// de processo é conferido só quando muda: desativar o tipo recusa vínculo novo, e o modelo que
    /// já o usava continua com ele.
    /// </summary>
    public static async Task<(ConteudoLido Conteudo, CatalogoDoModelo Catalogo, List<FieldError> Erros)> LerEConferirAsync(
        ConteudoDoModeloInput? entrada,
        FinalidadeFormulario finalidade,
        ConteudoDoModeloInput? gravado,
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
        (ConteudoDoModeloInput? mesclada, List<FieldError> daSecao) = ComASecaoDosDadosBasicos(entrada, finalidade, gravado);
        ConteudoLido conteudo = ConteudoLido.Ler(mesclada, catalogo.Formatos);

        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoReader
            .ListarVersoesAsync([.. conteudo.Termos.Select(static t => t.Termo.VersaoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        List<FieldError> erros = [.. ModeloFormulario.NoConteudo(
            daSecao.Concat(conteudo.Erros).Concat(ConferenciaDoModelo.Conferir(conteudo, catalogo, versoes.ToDictionary(static v => v.VersaoId), existentes)))];

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

    /// <summary>
    /// O conteúdo do modelo como o formulário do processo o terá depois de aplicado: no de inscrição, com o
    /// conjunto básico na seção reservada, mesclado como a gravação e a aplicação o mesclam. O modelo
    /// gravado sem o básico — o da semente, montado direto pelo domínio — o ganha ao ser aplicado, e a
    /// simulação do modelo tem de mostrar o que o candidato verá.
    /// </summary>
    public static ConteudoDoModelo ComoAplicado(ModeloFormulario modelo, IReadOnlyList<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        if (modelo.Finalidade != FinalidadeFormulario.Inscricao)
        {
            return modelo.Conteudo;
        }

        // O gravado é a própria referência do básico: o item básico que ele já traz fica como está, e a
        // mescla não tem com o que divergir.
        ConteudoDoModeloInput gravado = modelo.ToView().Conteudo;
        (ConteudoDoModeloInput? mesclado, _) = ComASecaoDosDadosBasicos(gravado, modelo.Finalidade, gravado);
        return ConteudoLido.Ler(mesclado, CatalogoDoModelo.De(fatos).Formatos).ParaConteudo();
    }

    private static (ConteudoDoModeloInput? Entrada, List<FieldError> Erros) ComASecaoDosDadosBasicos(
        ConteudoDoModeloInput? entrada, FinalidadeFormulario finalidade, ConteudoDoModeloInput? gravado)
    {
        if (entrada is null || finalidade != FinalidadeFormulario.Inscricao)
        {
            return (entrada, []);
        }

        (IReadOnlyList<FatoColetadoInput> itens, IReadOnlyList<GrupoColetadoInput> grupos, List<FieldError> erros) =
            ConjuntoBasicoDaInscricao.MesclarItens(entrada.Itens ?? [], entrada.Grupos ?? [], ConjuntoBasicoDaInscricao.Referencia(gravado?.Itens ?? []));
        (IReadOnlyList<EtapaFormularioInput> etapas, List<FieldError> daSecao) =
            ConjuntoBasicoDaInscricao.MesclarEtapas(entrada.Etapas ?? [], ConjuntoBasicoDaInscricao.SecaoDe(gravado?.Etapas ?? []));
        return (entrada with { Itens = itens, Grupos = grupos, Etapas = etapas }, [.. erros, .. daSecao]);
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
