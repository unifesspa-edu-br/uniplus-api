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
        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        Result definido = processo is null
            ? Result.Failure(ProcessoNaoEncontrado(command.ProcessoSeletivoId))
            : await DefinirAsync(command, processo, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        if (definido.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return definido;
    }

    // Provisório até a retirada da finalidade: o código do pedido ainda é traduzido para a finalidade que a conferência usa.
    private static FinalidadeFormulario FinalidadeDo(DefinirFormularioCommand command) =>
        FormularioDeclarado.FinalidadeProvisoriaDe(command.FormularioCodigo, command.Obrigatorio);

    internal static DomainError ProcessoNaoEncontrado(Guid id) =>
        new("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {id} não encontrado.");

    private static async Task<Result> DefinirAsync(
        DefinirFormularioCommand command, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        // Fora do rascunho a recusa vem antes de consultar cadastros.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        FormularioLido lido = await LerAsync(command, processo, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        return lido.Erros.Count > 0
            ? Result.ValidationFailure([.. lido.Erros, .. ConferirEstrutura(command, processo, lido)])
            : Gravar(command, processo, lido);
    }

    /// <summary>
    /// As páginas lidas pela forma, as exibições conferidas contra o catálogo — lido só quando preciso — e, na criação do
    /// formulário de inscrição, os itens do conjunto básico. O formulário de inscrição tem a seção reservada dos dados
    /// básicos, a primeira: o que nasce agora nasce também com os itens dela.
    /// </summary>
    private static async Task<FormularioLido> LerAsync(
        DefinirFormularioCommand command, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        bool inscricao = FinalidadeDo(command) == FinalidadeFormulario.Inscricao;
        (IReadOnlyList<PaginaDoFormularioInput> entradas, List<FieldError> daSecao) = inscricao
            ? ConjuntoBasicoDaInscricao.MesclarPaginas(command.Paginas ?? [], SecaoDadosBasicosDoProcesso.Secao(processo))
            : (command.Paginas ?? [], []);
        PaginasDoFormularioLidas lidas = EscritaDasPaginasDoFormulario.Ler(entradas);
        bool criaInscricao = inscricao && processo.FormularioDe(FinalidadeFormulario.Inscricao) is null;
        IReadOnlyList<FatoCandidatoView> catalogo = criaInscricao || lidas.Exibicoes.Any(static e => e is not null)
            ? await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false)
            : [];
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, catalogo);
        Result<ConsequenciaDoDescumprimento?> consequencia = LeituraDaConsequenciaDaFaltaDeEnvio.Ler(command.ConsequenciaDaFaltaDeEnvio, FinalidadeDo(command));
        List<FieldError> erros = [.. daSecao, .. lidas.Erros, .. EscritaDasPaginasDoFormulario.ConferirExibicoes(lidas, contexto), .. consequencia.Errors];
        List<FatoColetado>? itensDaCriacao = criaInscricao ? ResolverItensDaCriacao(contexto, erros) : null;
        return new FormularioLido(entradas.Count, [.. lidas.Paginas], itensDaCriacao, contexto, erros, consequencia.Value);
    }

    private static List<FatoColetado> ResolverItensDaCriacao(ContextoDoCatalogo contexto, List<FieldError> erros)
    {
        (List<FatoColetado> basicos, List<FieldError> errosDosBasicos) = EscritaDosItens.Resolver(EscritaDosItens.Ler(ConjuntoBasicoDaInscricao.Itens), contexto);
        erros.AddRange(errosDosBasicos);
        return basicos;
    }

    /// <summary>
    /// As recusas do formulário somam-se às das páginas (ADR-0125). A estrutura só é conferida com todas as páginas montadas,
    /// e a exibição já recusada pela semântica não é recusada de novo pela posição.
    /// </summary>
    private static IEnumerable<FieldError> ConferirEstrutura(DefinirFormularioCommand command, ProcessoSeletivo processo, FormularioLido lido)
    {
        HashSet<string> recusados = [.. lido.Erros.Select(static e => e.Field ?? string.Empty)];
        return RegrasDosFormularios
            .ConferirFormulario(
                processo.Declaracao(), FinalidadeDo(command), command.FaseId, command.Titulo, lido.Paginas, lido.PaginasCompletas, lido.ItensDaCriacao)
            .Where(recusa => recusa.Field is not { } campo || !campo.EndsWith(".exibicao", StringComparison.Ordinal) || !recusados.Contains(campo));
    }

    private static Result Gravar(DefinirFormularioCommand command, ProcessoSeletivo processo, FormularioLido lido)
    {
        Result vinculoNovo = ConferirVinculoNovo(processo, lido);
        if (vinculoNovo.IsFailure)
        {
            return Result.Failure(vinculoNovo.Error!);
        }

        Result definido = processo.DefinirFormulario(
            FinalidadeDo(command), command.FaseId, command.Titulo, lido.Paginas, lido.ItensDaCriacao, lido.ConsequenciaDaFaltaDeEnvio);
        return definido.IsFailure ? Result.ValidationFailure(definido.Errors) : Result.Success();
    }

    private static Result ConferirVinculoNovo(ProcessoSeletivo processo, FormularioLido lido) =>
        ConferenciaDeVinculoNovo.Conferir(
            lido.Contexto.Fatos,
            ReferenciasVivas.Vinculos(processo.Declaracao()),
            VinculosDeFatos.De(
                (lido.ItensDaCriacao ?? []).Select(static f => f.FatoCodigo),
                lido.Paginas.SelectMany(static e => e.Condicoes).Concat((lido.ItensDaCriacao ?? []).SelectMany(static f => f.Condicoes))
                    .Select(static c => (c.Fato, c.Valor))));

    /// <summary>
    /// As páginas montadas, os itens da criação do formulário de inscrição, o contexto do catálogo que as conferiu e as
    /// recusas da leitura; as páginas estão completas quando nenhuma entrada foi recusada.
    /// </summary>
    private sealed record FormularioLido(
        int QuantidadeEnviada, List<PaginaDoFormulario> Paginas, List<FatoColetado>? ItensDaCriacao, ContextoDoCatalogo Contexto, List<FieldError> Erros,
        ConsequenciaDoDescumprimento? ConsequenciaDaFaltaDeEnvio)
    {
        public bool PaginasCompletas => Paginas.Count == QuantidadeEnviada;
    }
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
            ? Result.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId))
            : Remover(processo, command.FormularioCodigo);
        if (removido.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return removido;
    }

    private static Result Remover(ProcessoSeletivo processo, string formularioCodigo)
    {
        Result<CodigoDoFormulario> codigo = CodigoDoFormulario.Criar(formularioCodigo);
        return codigo.IsFailure ? Result.Failure(codigo.Error!) : processo.RemoverFormulario(codigo.Value!);
    }
}
