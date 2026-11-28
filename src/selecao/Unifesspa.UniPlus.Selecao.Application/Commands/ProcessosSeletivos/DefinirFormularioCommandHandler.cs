namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// Monta as páginas com as recusas acumuladas por página (ADR-0125) e entrega ao processo, que confere
/// a estrutura do formulário, a fase, os itens já definidos e os fatos que a exibição de cada seção
/// cita. A semântica da exibição (fato do catálogo, operador e valor do domínio) é conferida aqui,
/// contra o catálogo.
/// </summary>
public static class DefinirFormularioCommandHandler
{
    public static async Task<Result> Handle(
        DefinirFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirESalvarAsync(command, obtido.Value!, fatoCandidatoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirESalvarAsync(
        DefinirFormularioCommand command, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definido = await DefinirAsync(command, processo, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        if (definido.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return definido;
    }

    private static async Task<Result> DefinirAsync(
        DefinirFormularioCommand command, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        Result<FormularioDeclarado> declarado = Declarar(command);
        return declarado.IsFailure
            ? Result.Failure(declarado.Error!)
            : await LerEGravarAsync(command, declarado.Value!, processo, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
    }

    private static Result<FormularioDeclarado> Declarar(DefinirFormularioCommand command)
    {
        Result<CodigoDoFormulario> codigo = CodigoDoFormulario.Criar(command.FormularioCodigo);
        return codigo.IsFailure
            ? Result<FormularioDeclarado>.Failure(codigo.Error!)
            : Result<FormularioDeclarado>.Success(new FormularioDeclarado(codigo.Value!, command.FaseId, command.Obrigatorio));
    }

    private static async Task<Result> LerEGravarAsync(
        DefinirFormularioCommand command, FormularioDeclarado declarado, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        FormularioLido lido = await LerAsync(command, declarado, processo, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        return lido.Erros.Count > 0 || lido.RecusasDoVinculoNovo.Count > 0
            ? Result.ValidationFailure([.. lido.Erros, .. ConferirEstrutura(command, declarado, processo, lido), .. lido.RecusasDoVinculoNovo])
            : Gravar(command, declarado, processo, lido);
    }

    /// <summary>
    /// As páginas lidas pela forma, o catálogo lido só quando preciso — para a exibição ou para os itens da criação — e as
    /// recusas conferidas contra ele. O formulário que guarda o conjunto básico da inscrição tem a seção reservada dos dados
    /// básicos, a primeira: o que nasce agora nasce também com os itens dela.
    /// </summary>
    private static async Task<FormularioLido> LerAsync(
        DefinirFormularioCommand command, FormularioDeclarado declarado, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        bool guardaOConjuntoBasico = ConjuntoBasicoNaInscricao.Guarda(processo.Declaracao(), declarado);
        (IReadOnlyList<PaginaDoFormularioInput> entradas, List<FieldError> daSecao) = guardaOConjuntoBasico
            ? ConjuntoBasicoDaInscricao.MesclarPaginas(command.Paginas ?? [], SecaoDadosBasicosDoProcesso.Secao(processo, declarado.Codigo))
            : (command.Paginas ?? [], []);
        PaginasDoFormularioLidas lidas = EscritaDasPaginasDoFormulario.Ler(entradas);
        bool criaInscricao = guardaOConjuntoBasico && processo.Declaracao().Coleta.FormularioDe(declarado.Codigo) is null;
        IReadOnlyList<FatoCandidatoView> catalogo = criaInscricao || lidas.Exibicoes.Any(static e => e is not null)
            ? await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false)
            : [];
        return LerContraOCatalogo(command, declarado, processo, new PaginasEmLeitura(lidas, daSecao, criaInscricao), ContextoDoCatalogo.De(processo, catalogo));
    }

    private static FormularioLido LerContraOCatalogo(
        DefinirFormularioCommand command, FormularioDeclarado declarado, ProcessoSeletivo processo, PaginasEmLeitura paginas, ContextoDoCatalogo contexto)
    {
        Result<ConsequenciaDoDescumprimento?> consequencia = LeituraDaConsequenciaDaFaltaDeEnvio.Ler(command.ConsequenciaDaFaltaDeEnvio, declarado.Codigo);
        List<FieldError> erros = [.. paginas.DaSecao, .. paginas.Lidas.Erros, .. EscritaDasPaginasDoFormulario.ConferirExibicoes(paginas.Lidas, contexto), .. consequencia.Errors];
        ItensLidos? lidosDaCriacao = paginas.CriaInscricao ? LeituraDosItensDoFormulario.Ler(ConjuntoBasicoDaInscricao.Itens, contexto) : null;
        List<FatoColetado>? itensDaCriacao = lidosDaCriacao is null ? null : ResolverItensDaCriacao(lidosDaCriacao, contexto, erros);
        return new FormularioLido(
            paginas.Lidas, itensDaCriacao, erros, consequencia.Value,
            RecusasDoVinculoNovo(processo, contexto, paginas.Lidas, lidosDaCriacao));
    }

    private static List<FatoColetado> ResolverItensDaCriacao(ItensLidos lidos, ContextoDoCatalogo contexto, List<FieldError> erros)
    {
        (List<FatoColetado> basicos, List<FieldError> errosDosBasicos) = EscritaDosItens.Resolver(lidos, contexto);
        erros.AddRange(errosDosBasicos);
        return basicos;
    }

    /// <summary>
    /// O vínculo novo ao catálogo, conferido junto das demais recusas: as exibições lidas de todas as páginas, inclusive as
    /// recusadas, e os dados e as condições dos itens lidos da criação do formulário.
    /// </summary>
    private static List<FieldError> RecusasDoVinculoNovo(ProcessoSeletivo processo, ContextoDoCatalogo contexto, PaginasDoFormularioLidas lidas, ItensLidos? criacao) =>
        [.. ConferenciaDeVinculoNovo.Conferir(
            contexto.Fatos,
            ReferenciasVivas.Vinculos(processo.Declaracao()),
            VinculosDeFatos.De(
                criacao?.CodigosEnviados ?? [],
                lidas.Exibicoes.SelectMany(static e => e?.Clausulas ?? []).SelectMany(static c => c.Condicoes).Concat(criacao?.CondicoesLidas ?? [])
                    .Select(static c => (c.Fato, c.Valor)))).Errors];

    /// <summary>
    /// As recusas do formulário somam-se às das páginas (ADR-0125). Cada página montada é conferida contra as outras na posição
    /// do pedido; o que conclui algo sobre o formulário inteiro só é conferido com todas as páginas montadas, e a exibição já
    /// recusada pela semântica não é recusada de novo pela posição.
    /// </summary>
    private static IEnumerable<FieldError> ConferirEstrutura(DefinirFormularioCommand command, FormularioDeclarado declarado, ProcessoSeletivo processo, FormularioLido lido)
    {
        HashSet<string> recusados = [.. lido.Erros.Select(static e => e.Field ?? string.Empty)];
        return RegrasDosFormularios
            .ConferirFormulario(
                processo.Declaracao(), declarado, command.Titulo, lido.Paginas.PorPosicao, lido.ItensDaCriacao)
            .Where(recusa => recusa.Field is not { } campo || !campo.EndsWith(".exibicao", StringComparison.Ordinal) || !recusados.Contains(campo));
    }

    private static Result Gravar(DefinirFormularioCommand command, FormularioDeclarado declarado, ProcessoSeletivo processo, FormularioLido lido)
    {
        Result definido = processo.DefinirFormulario(declarado, command.Titulo, lido.Paginas.Montadas, lido.ItensDaCriacao, lido.ConsequenciaDaFaltaDeEnvio);
        return definido.IsFailure ? Result.ValidationFailure(definido.Errors) : Result.Success();
    }

    /// <summary>As páginas lidas pela forma, as recusas da seção dos dados básicos e se o formulário nasce com os itens dela.</summary>
    private sealed record PaginasEmLeitura(PaginasDoFormularioLidas Lidas, List<FieldError> DaSecao, bool CriaInscricao);

    /// <summary>
    /// As páginas lidas por posição, os itens da criação do formulário de inscrição, as recusas da leitura e as do vínculo
    /// novo ao catálogo.
    /// </summary>
    private sealed record FormularioLido(
        PaginasDoFormularioLidas Paginas, List<FatoColetado>? ItensDaCriacao, List<FieldError> Erros,
        ConsequenciaDoDescumprimento? ConsequenciaDaFaltaDeEnvio, List<FieldError> RecusasDoVinculoNovo);
}

/// <summary>Remove o formulário do código; a regra de só em rascunho é do processo.</summary>
public static class RemoverFormularioCommandHandler
{
    public static async Task<Result> Handle(
        RemoverFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        Result removido = processo is null
            ? Result.Failure(AcessoAoProcesso.NaoEncontrado())
            : Remover(processo, command.FormularioCodigo);
        if (removido.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return removido;
    }

    private static Result Remover(ProcessoSeletivo processo, string formularioCodigo)
    {
        Result<CodigoDoFormulario> codigo = RegrasDosFormularios.CodigoDoCaminho(formularioCodigo);
        return codigo.IsFailure ? Result.Failure(codigo.Error!) : processo.RemoverFormulario(codigo.Value!);
    }
}
