namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDocumentosExigidosCommand"/> (Story #554): resolve o
/// snapshot-copy de <c>TipoDocumento</c> (módulo Configuração, ADR-0056) e, quando algum
/// item declara gatilho, o vocabulário fechado de fatos do candidato
/// (<c>IFatoCandidatoReader</c>, #846) estendido pelo domínio dinâmico da oferta do
/// próprio processo (PR-b) — e delega a montagem/validação ao domínio.
/// </summary>
public static class DefinirDocumentosExigidosCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirDocumentosExigidosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoDocumentoReader tipoDocumentoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição precede a resolução de leituras externas (ADR-0110 D9) — mesma
        // nota dos demais Definir*.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // O vocabulário só é resolvido (I/O cross-módulo) quando algum item declara
        // gatilho — mesmo princípio de DefinirCriteriosDesempateCommandHandler.
        bool existeGatilho = command.Itens.Any(static i => i.Condicoes.Count > 0);
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos = existeGatilho
            ? await ResolverVocabularioFatosAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false)
            : null;
        IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos = existeGatilho
            ? ResolverDominiosDinamicos(processo)
            : null;

        List<DocumentoExigido> itens = [];
        foreach (ItemDocumentoExigidoInput input in command.Itens)
        {
            TipoDocumentoView? tipoDocumento = await tipoDocumentoReader
                .ObterPorIdAsync(input.TipoDocumentoId, cancellationToken)
                .ConfigureAwait(false);
            if (tipoDocumento is null)
            {
                return Result<MutacaoAceita>.Failure(new DomainError(
                    "DocumentoExigido.TipoDocumentoNaoEncontrado",
                    $"Tipo de documento {input.TipoDocumentoId} não encontrado ou não está mais vivo."));
            }

            Aplicabilidade aplicabilidade = input.Aplicabilidade switch
            {
                "GERAL" => Aplicabilidade.Geral,
                "CONDICIONAL" => Aplicabilidade.Condicional,
                _ => Aplicabilidade.Nenhuma,
            };

            Result<IReadOnlyList<CondicaoGatilho>> condicoesResult = ResolverCondicoes(
                input.Condicoes, vocabularioFatos, dominiosDinamicos);
            if (condicoesResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(condicoesResult.Error!);
            }

            Result<IReadOnlyList<DocumentoExigidoBaseLegal>> basesLegaisResult = ResolverBasesLegais(input.BasesLegais);
            if (basesLegaisResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(basesLegaisResult.Error!);
            }

            Result<DocumentoExigido> itemResult = DocumentoExigido.Criar(
                input.ExigidoNaFaseId,
                tipoDocumento.Id,
                tipoDocumento.Codigo,
                tipoDocumento.Nome,
                tipoDocumento.Categoria,
                aplicabilidade,
                input.Obrigatorio,
                input.ConsequenciaIndeferimento,
                input.GrupoSatisfacaoId,
                condicoesResult.Value!,
                basesLegaisResult.Value!);
            if (itemResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(itemResult.Error!);
            }

            itens.Add(itemResult.Value!);
        }

        Result definirResult = processo.DefinirDocumentosExigidos(itens, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    /// <summary>
    /// Resolve as condições de um item: monta <see cref="CondicaoDnf"/> por linha, agrupa
    /// em <see cref="PredicadoDnf"/> (mesma forma de <c>PredicadoDnf.CriarDeCondicoesAgrupadas</c>)
    /// para validar CA-02/CA-03 via <see cref="PredicadoDnfValidador"/> — fato no vocabulário
    /// fechado, operador × domínio, valor × domínio (estático OU dinâmico, contra a oferta
    /// do processo) — e só então converte para as entidades <see cref="CondicaoGatilho"/>
    /// que o domínio persiste.
    /// </summary>
    private static Result<IReadOnlyList<CondicaoGatilho>> ResolverCondicoes(
        IReadOnlyList<CondicaoGatilhoInput> inputs,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos)
    {
        if (inputs.Count == 0)
        {
            return Result<IReadOnlyList<CondicaoGatilho>>.Success([]);
        }

        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        foreach (CondicaoGatilhoInput input in inputs)
        {
            Operador operador = OperadorCodigo.FromCodigo(input.Operador);
            JsonElement valor = InterpretarValor(input.Valor);

            Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(input.Fato, operador, valor);
            if (condicaoResult.IsFailure)
            {
                return Result<IReadOnlyList<CondicaoGatilho>>.Failure(condicaoResult.Error!);
            }

            linhas.Add((input.Clausula, condicaoResult.Value!));
        }

        Result<PredicadoDnf> predicadoResult = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
        if (predicadoResult.IsFailure)
        {
            return Result<IReadOnlyList<CondicaoGatilho>>.Failure(predicadoResult.Error!);
        }

        Result validacaoResult = PredicadoDnfValidador.Validar(
            predicadoResult.Value!, vocabularioFatos ?? new Dictionary<string, DescritorFatoCandidato>(), null, dominiosDinamicos);
        if (validacaoResult.IsFailure)
        {
            return Result<IReadOnlyList<CondicaoGatilho>>.Failure(validacaoResult.Error!);
        }

        List<CondicaoGatilho> condicoes = [];
        foreach ((int clausula, CondicaoDnf condicao) in linhas)
        {
            Result<CondicaoGatilho> gatilhoResult = CondicaoGatilho.Criar(clausula, condicao.Fato, condicao.Operador, condicao.Valor);
            if (gatilhoResult.IsFailure)
            {
                return Result<IReadOnlyList<CondicaoGatilho>>.Failure(gatilhoResult.Error!);
            }

            condicoes.Add(gatilhoResult.Value!);
        }

        return Result<IReadOnlyList<CondicaoGatilho>>.Success(condicoes);
    }

    /// <summary>
    /// Resolve as bases legais de um item (Story #554, PR-c, issue #549) — só a forma de
    /// cada base é validada aqui (referência não vazia, abrangência/status no domínio
    /// fechado); o gate "≥1 RESOLVIDO por exigência que determina resultado" é da
    /// publicação (<c>Domain.Services.ValidadorBaseLegalExigencias</c>), nunca da escrita.
    /// </summary>
    private static Result<IReadOnlyList<DocumentoExigidoBaseLegal>> ResolverBasesLegais(IReadOnlyList<BaseLegalInput> inputs)
    {
        List<DocumentoExigidoBaseLegal> basesLegais = [];
        foreach (BaseLegalInput input in inputs)
        {
            TipoAbrangencia abrangencia = TipoAbrangenciaCodigo.FromCodigo(input.Abrangencia);
            StatusBaseLegal status = StatusBaseLegalCodigo.FromCodigo(input.Status);

            Result<DocumentoExigidoBaseLegal> baseLegalResult = DocumentoExigidoBaseLegal.Criar(
                input.Referencia, abrangencia, status, input.Observacao);
            if (baseLegalResult.IsFailure)
            {
                return Result<IReadOnlyList<DocumentoExigidoBaseLegal>>.Failure(baseLegalResult.Error!);
            }

            basesLegais.Add(baseLegalResult.Value!);
        }

        return Result<IReadOnlyList<DocumentoExigidoBaseLegal>>.Success(basesLegais);
    }

    /// <summary>
    /// Domínio dinâmico (Story #554, PR-b): as modalidades/condições de atendimento
    /// válidas para um gatilho são as que o PRÓPRIO PROCESSO oferece — nunca um catálogo
    /// global (CA-03, integridade referencial).
    /// </summary>
    private static Dictionary<string, IReadOnlySet<string>> ResolverDominiosDinamicos(ProcessoSeletivo processo)
    {
        HashSet<string> modalidades = [.. processo.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Select(static m => m.Codigo)];

        HashSet<string> condicoesAtendimento = [.. (processo.OfertaAtendimento?.Condicoes ?? [])
            .Select(static c => c.CondicaoCodigo)];

        return new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["MODALIDADE"] = modalidades,
            ["CONDICAO_ATENDIMENTO"] = condicoesAtendimento,
        };
    }

    /// <summary>
    /// Mapeia <see cref="FatoCandidatoView"/> para <see cref="DescritorFatoCandidato"/>,
    /// estendendo <c>DefinirCriteriosDesempateCommandHandler.ResolverVocabularioFatosAsync</c>
    /// (#846/#847) para incluir os fatos categóricos de escopo-processo (Story #554, PR-b) —
    /// antes deliberadamente fora do vocabulário fechado.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, DescritorFatoCandidato>> ResolverVocabularioFatosAsync(
        IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, DescritorFatoCandidato> vocabulario = [];
        foreach (FatoCandidatoView fato in fatos)
        {
            TipoDominioFato? tipoDominio = fato switch
            {
                { Dominio: "BOOLEANO" } => TipoDominioFato.Booleano,
                { Dominio: "NUMERICO" } => TipoDominioFato.Numerico,
                { Dominio: "CATEGORICO", ValoresDominio.Count: > 0 } => TipoDominioFato.CategoricoEstatico,
                { Dominio: "CATEGORICO", ValoresDominio: null } => TipoDominioFato.CategoricoDinamico,
                _ => null,
            };

            if (tipoDominio is not { } tipo)
            {
                continue;
            }

            Result<DescritorFatoCandidato> descritorResult = DescritorFatoCandidato.Criar(fato.Codigo, tipo, fato.ValoresDominio);
            if (descritorResult.IsSuccess)
            {
                vocabulario[fato.Codigo] = descritorResult.Value!;
            }
        }

        return vocabulario;
    }

    /// <summary>
    /// Mesma interpretação de <c>DefinirCriteriosDesempateCommandHandler.InterpretarValor</c>:
    /// texto JSON válido (booleano, número, string entre aspas, array para EM) é
    /// interpretado como tal; texto que não é JSON válido é tratado como o escalar de
    /// string que representa.
    /// </summary>
    private static JsonElement InterpretarValor(string valor)
    {
        try
        {
            using JsonDocument documento = JsonDocument.Parse(valor);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(valor);
        }
    }
}
