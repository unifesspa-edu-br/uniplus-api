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
/// próprio processo (PR #896) — e delega a montagem/validação ao domínio.
/// </summary>
/// <remarks>
/// <paramref name="clock"/> — Story #554, PR #900, issue #893 (ADR-0068 proposed): injetado
/// por convenção do módulo, mesmo sem uso direto de "agora" nesta task — a avaliação em
/// runtime da idade máxima de emissão (<c>IdadeMaximaEmissao</c>) é fora de escopo desta
/// Story; este handler não abre exceção isolada à convenção só por não usá-lo ainda.
/// </remarks>
public static class DefinirDocumentosExigidosCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirDocumentosExigidosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoDocumentoReader tipoDocumentoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

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
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos = null;
        IReadOnlyDictionary<string, string>? pontoResolucaoPorFato = null;
        if (existeGatilho)
        {
            (vocabularioFatos, pontoResolucaoPorFato) = await ResolverVocabularioFatosAsync(fatoCandidatoReader, cancellationToken)
                .ConfigureAwait(false);
        }

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

            // Gate de fase (Story #916): uma condição de gatilho não pode citar um fato cujo
            // PontoResolucao é uma fase posterior à fase em que o documento é exigido — não há
            // como o gatilho já ter sido resolvido para o candidato quando a exigência entra
            // em jogo.
            if (condicoesResult.Value!.Count > 0)
            {
                Result gateDeFaseResult = ValidarGateDeFase(
                    condicoesResult.Value!, input.ExigidoNaFaseId, processo, pontoResolucaoPorFato!);
                if (gateDeFaseResult.IsFailure)
                {
                    return Result<MutacaoAceita>.Failure(gateDeFaseResult.Error!);
                }
            }

            Result<IReadOnlyList<DocumentoExigidoBaseLegal>> basesLegaisResult = ResolverBasesLegais(input.BasesLegais);
            if (basesLegaisResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(basesLegaisResult.Error!);
            }

            IdadeMaximaEmissaoInput? idade = input.IdadeMaximaEmissao;
            Result<IdadeMaximaEmissao?> idadeMaximaEmissaoResult = IdadeMaximaEmissao.Criar(
                idade?.Valor,
                UnidadeIdadeCodigo.FromCodigo(idade?.Unidade),
                ReferenciaTipoIdadeEmissaoCodigo.FromCodigo(idade?.ReferenciaTipo),
                idade?.Data,
                idade?.ReferenciaFaseId);
            if (idadeMaximaEmissaoResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(idadeMaximaEmissaoResult.Error!);
            }

            Result<FormatosPermitidos> formatosPermitidosResult = ResolverFormatosPermitidos(input.FormatosPermitidos);
            if (formatosPermitidosResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(formatosPermitidosResult.Error!);
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
                basesLegaisResult.Value!,
                idadeMaximaEmissaoResult.Value,
                formatosPermitidosResult.Value!,
                input.TamanhoMaximoBytes);
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
    /// Gate de fase (Story #916): recusa uma condição de gatilho cujo fato só é conhecido
    /// (<c>PontoResolucao</c>) numa fase posterior à fase em que o documento é exigido — o
    /// gatilho nunca teria como ter sido resolvido para o candidato a essa altura do
    /// certame. A fase da PRÓPRIA exigência é localizada com <c>SingleOrDefault</c> (nunca
    /// <c>Single</c>, para não estourar exceção em vez de 500): quando não encontrada, este
    /// método não recusa de novo — <see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>
    /// já garante, eagerly, que toda fase referenciada pertence ao cronograma
    /// (<c>DocumentoExigido.FaseNaoPertenceAoProcesso</c>), e é essa checagem que decide o caso.
    /// </summary>
    private static Result ValidarGateDeFase(
        IReadOnlyList<CondicaoGatilho> condicoes,
        Guid exigidoNaFaseId,
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato)
    {
        FaseCronograma? faseDaExigencia = processo.CronogramaFases.SingleOrDefault(f => f.Id == exigidoNaFaseId);

        foreach (CondicaoGatilho condicao in condicoes)
        {
            if (!pontoResolucaoPorFato.TryGetValue(condicao.Fato, out string? pontoResolucao))
            {
                // O fato já passou por PredicadoDnfValidador (vocabulário fechado) antes de
                // chegar aqui — não deveria faltar no mapa construído junto do mesmo
                // vocabulário. Defensivo: nada a comparar, não bloqueia.
                continue;
            }

            FaseCronograma? faseDoPontoResolucao = processo.CronogramaFases
                .SingleOrDefault(f => string.Equals(f.Codigo, pontoResolucao, StringComparison.Ordinal));

            if (faseDoPontoResolucao is null)
            {
                return Result.Failure(new DomainError(
                    "DocumentoExigido.PontoResolucaoForaDoCronograma",
                    $"O fato '{condicao.Fato}' resolve na fase '{pontoResolucao}', que não pertence ao cronograma deste processo."));
            }

            if (faseDaExigencia is not null && faseDoPontoResolucao.Ordem > faseDaExigencia.Ordem)
            {
                return Result.Failure(new DomainError(
                    "DocumentoExigido.FatoResolvidoEmFasePosterior",
                    $"O fato '{condicao.Fato}' só é conhecido na fase '{pontoResolucao}' (ordem {faseDoPontoResolucao.Ordem}), posterior à fase em que o documento é exigido (ordem {faseDaExigencia.Ordem})."));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Resolve as bases legais de um item (Story #554, PR #898, issue #549) — só a forma de
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
    /// Domínio dinâmico (Story #554, PR #896): as modalidades/condições de atendimento
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

        HashSet<string> tiposDeficiencia = [.. (processo.OfertaAtendimento?.TiposDeficiencia ?? [])
            .Select(static t => t.TipoDeficienciaNome)];

        return new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["MODALIDADE"] = modalidades,
            ["CONDICAO_ATENDIMENTO"] = condicoesAtendimento,
            ["TIPO_DEFICIENCIA"] = tiposDeficiencia,
        };
    }

    /// <summary>
    /// Mapeia <see cref="FatoCandidatoView"/> para <see cref="DescritorFatoCandidato"/>,
    /// estendendo <c>DefinirCriteriosDesempateCommandHandler.ResolverVocabularioFatosAsync</c>
    /// (#846/#847) para incluir os fatos categóricos de escopo-processo (Story #554, PR #896) —
    /// antes deliberadamente fora do vocabulário fechado. Devolve, na mesma passada, a projeção
    /// <c>PontoResolucao</c> por fato (Story #916) que o gate de fase usa — vive como projeção
    /// própria deste handler, e não em <see cref="DescritorFatoCandidato"/> (VO mínimo
    /// compartilhado com <c>DefinirCriteriosDesempateCommandHandler</c>, que não precisa dela).
    /// </summary>
    private static async Task<(IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario, IReadOnlyDictionary<string, string> PontoResolucaoPorFato)> ResolverVocabularioFatosAsync(
        IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, DescritorFatoCandidato> vocabulario = [];
        Dictionary<string, string> pontoResolucaoPorFato = new(StringComparer.Ordinal);
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
                pontoResolucaoPorFato[fato.Codigo] = fato.PontoResolucao;
            }
        }

        return (vocabulario, pontoResolucaoPorFato);
    }

    /// <summary>
    /// Interpreta o valor JSON polimórfico de <c>FormatosPermitidos</c> (Story #918): o
    /// mesmo tratamento de <see cref="CondicaoDnf.Valor"/> (resolvido por
    /// <see cref="JsonElement.ValueKind"/>, não um DTO discriminado). <see langword="null"/>
    /// estrutural (propriedade ausente do JSON, ou <c>null</c> explícito) é distinto de
    /// "veio, mas em forma inválida" — o primeiro produz <c>FormatosPermitidos.Obrigatorio</c>
    /// aqui mesmo, sem chegar a <see cref="Domain.ValueObjects.FormatosPermitidos.Criar"/>
    /// (que não tem como diferenciar "ausente" de "false" ao receber só um bool); o
    /// segundo produz <c>FormatosPermitidos.FormaInvalida</c>. Cada item do array aceita
    /// tanto um escalar de texto (<c>"PDF"</c>, sem teto por formato — cobre o cenário BDD
    /// da lista simples) quanto um objeto <c>{formato, tamanhoMaximoBytesMax}</c> (cobre o
    /// cenário BDD do teto por formato) — a forma mais direta que ainda cobre os dois
    /// cenários da issue, sem introduzir um terceiro shape de wire.
    /// </summary>
    private static Result<FormatosPermitidos> ResolverFormatosPermitidos(JsonElement? valor)
    {
        if (valor is not { } elemento)
        {
            return Result<FormatosPermitidos>.Failure(new DomainError(
                "FormatosPermitidos.Obrigatorio",
                "FormatosPermitidos é obrigatório: declare QUALQUER ou uma lista com ao menos um formato."));
        }

        if (elemento.ValueKind == JsonValueKind.String)
        {
            return string.Equals(elemento.GetString(), "QUALQUER", StringComparison.Ordinal)
                ? FormatosPermitidos.Criar(qualquer: true, entradas: null)
                : Result<FormatosPermitidos>.Failure(new DomainError(
                    "FormatosPermitidos.FormaInvalida",
                    "FormatosPermitidos deve ser o token QUALQUER ou um array de formatos."));
        }

        if (elemento.ValueKind != JsonValueKind.Array)
        {
            return Result<FormatosPermitidos>.Failure(new DomainError(
                "FormatosPermitidos.FormaInvalida",
                "FormatosPermitidos deve ser o token QUALQUER ou um array de formatos."));
        }

        List<(string Formato, int? TamanhoMaximoBytesMax)> entradas = [];
        foreach (JsonElement item in elemento.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    entradas.Add((item.GetString() ?? string.Empty, null));
                    break;

                case JsonValueKind.Object:
                    string? formato = item.TryGetProperty("formato", out JsonElement formatoElemento)
                        && formatoElemento.ValueKind == JsonValueKind.String
                            ? formatoElemento.GetString()
                            : null;
                    int? tamanhoMaximoBytesMax = item.TryGetProperty("tamanhoMaximoBytesMax", out JsonElement tamanhoElemento)
                        && tamanhoElemento.ValueKind == JsonValueKind.Number
                            ? tamanhoElemento.GetInt32()
                            : null;

                    if (formato is null)
                    {
                        return Result<FormatosPermitidos>.Failure(new DomainError(
                            "FormatosPermitidos.FormaInvalida",
                            "Cada item da lista de formatos permitidos deve declarar 'formato'."));
                    }

                    entradas.Add((formato, tamanhoMaximoBytesMax));
                    break;

                default:
                    return Result<FormatosPermitidos>.Failure(new DomainError(
                        "FormatosPermitidos.FormaInvalida",
                        "Cada item da lista de formatos permitidos deve ser um texto ou um objeto {formato, tamanhoMaximoBytesMax}."));
            }
        }

        return FormatosPermitidos.Criar(qualquer: false, entradas);
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
