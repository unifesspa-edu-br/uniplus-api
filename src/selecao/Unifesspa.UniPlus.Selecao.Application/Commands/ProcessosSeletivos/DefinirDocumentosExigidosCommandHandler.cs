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
/// Handler do <see cref="DefinirDocumentosExigidosCommand"/> (Story #554; árvore E/OU na
/// Story #920): resolve o snapshot-copy de <c>TipoDocumento</c> (módulo Configuração,
/// ADR-0056) e, quando alguma folha declara gatilho, o vocabulário fechado de fatos do
/// candidato (<c>IFatoCandidatoReader</c>, #846) estendido pelo domínio dinâmico da oferta do
/// próprio processo (PR #896) — monta a árvore recursivamente (folhas primeiro, bottom-up,
/// já que <see cref="NoExigencia.CriarGrupo"/> recebe os filhos prontos) e delega a
/// montagem/validação final ao domínio.
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

        // O vocabulário só é resolvido (I/O cross-módulo) quando alguma folha declara
        // gatilho — mesmo princípio de DefinirCriteriosDesempateCommandHandler.
        bool existeGatilho = ExisteGatilho(command.Raizes);
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

        // Folha primeiro, bottom-up: NoExigencia.CriarGrupo recebe os filhos já prontos. A
        // recursão em si é top-down (visita o nó antes dos filhos) — por isso dá para
        // propagar `tipoEntidadeAncestral` descendo, mesmo com a CONSTRUÇÃO do NoExigencia
        // sendo bottom-up.
        async Task<Result<NoExigencia>> ConstruirNoAsync(NoExigenciaInput input, int ordem, TipoEntidade? tipoEntidadeAncestral)
        {
            TipoEntidade? tipoEntidadeEfetivo = tipoEntidadeAncestral
                ?? (input.RepetePorEntidade is null ? null : TipoEntidadeCodigo.FromCodigo(input.RepetePorEntidade));

            if (string.Equals(input.Tipo, "FOLHA", StringComparison.Ordinal))
            {
                if (input.Documento is not { } documentoInput)
                {
                    return Result<NoExigencia>.Failure(new DomainError(
                        "NoExigencia.DocumentoObrigatorioEmFolha",
                        "Um nó do tipo FOLHA precisa declarar 'documento'."));
                }

                Result<DocumentoExigido> documentoResult = await ConstruirDocumentoExigidoAsync(
                        documentoInput, processo, tipoDocumentoReader, vocabularioFatos, pontoResolucaoPorFato,
                        dominiosDinamicos, tipoEntidadeEfetivo, cancellationToken)
                    .ConfigureAwait(false);
                if (documentoResult.IsFailure)
                {
                    return Result<NoExigencia>.Failure(documentoResult.Error!);
                }

                ChaveDistincao? chaveDistincao = input.ChaveDistincao is null
                    ? null
                    : ChaveDistincaoCodigo.FromCodigo(input.ChaveDistincao);

                return NoExigencia.CriarFolha(
                    documentoResult.Value!,
                    ordem,
                    input.QuantidadeMinima,
                    chaveDistincao,
                    input.DataReferencia,
                    input.OcorrenciasEsperadas,
                    tipoEntidadeEfetivo is null ? null : TipoEntidadeCodigo.FromCodigo(input.RepetePorEntidade));
            }

            TipoNo tipo = input.Tipo switch
            {
                "E" => TipoNo.GrupoE,
                "OU" => TipoNo.GrupoOu,
                _ => TipoNo.Nenhum,
            };

            if (tipo == TipoNo.Nenhum)
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "NoExigencia.TipoInvalido",
                    $"Tipo de nó '{input.Tipo}' inválido — esperado FOLHA, E ou OU."));
            }

            List<NoExigencia> filhos = [];
            int ordemFilho = 0;
            foreach (NoExigenciaInput filhoInput in input.Filhos ?? [])
            {
                Result<NoExigencia> filhoResult = await ConstruirNoAsync(filhoInput, ordemFilho, tipoEntidadeEfetivo).ConfigureAwait(false);
                if (filhoResult.IsFailure)
                {
                    return Result<NoExigencia>.Failure(filhoResult.Error!);
                }

                filhos.Add(filhoResult.Value!);
                ordemFilho++;
            }

            Result<IReadOnlyList<NoExigenciaBaseLegal>> basesLegaisResult =
                ResolverBasesLegaisDeGrupo(input.BasesLegais ?? []);
            if (basesLegaisResult.IsFailure)
            {
                return Result<NoExigencia>.Failure(basesLegaisResult.Error!);
            }

            TipoEntidade? repetePorEntidadeGrupo = input.RepetePorEntidade is null
                ? null
                : TipoEntidadeCodigo.FromCodigo(input.RepetePorEntidade);

            return NoExigencia.CriarGrupo(
                tipo, ordem, input.QuantidadeMinima, input.Consequencia, basesLegaisResult.Value!, filhos, repetePorEntidadeGrupo);
        }

        List<NoExigencia> raizes = [];
        int ordemRaiz = 0;
        foreach (NoExigenciaInput raizInput in command.Raizes)
        {
            Result<NoExigencia> raizResult = await ConstruirNoAsync(raizInput, ordemRaiz, tipoEntidadeAncestral: null).ConfigureAwait(false);
            if (raizResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(raizResult.Error!);
            }

            raizes.Add(raizResult.Value!);
            ordemRaiz++;
        }

        Result definirResult = processo.DefinirDocumentosExigidos(raizes, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    /// <summary>Resolve o vocabulário de gatilho recursivamente na floresta — só a presença de gatilho, ainda sem I/O.</summary>
    private static bool ExisteGatilho(IReadOnlyList<NoExigenciaInput> nos) =>
        nos.Any(no => (no.Documento?.Condicoes.Count ?? 0) > 0 || ExisteGatilho(no.Filhos ?? []));

    /// <summary>
    /// Constrói UMA folha da árvore — exatamente a mesma resolução por-item do modelo plano
    /// anterior (tipo de documento, aplicabilidade, gatilho, gate de fase, base legal,
    /// idade/formato/tamanho), sem o antigo <c>GrupoSatisfacaoId</c> (a posição na árvore o
    /// substitui).
    /// </summary>
    private static async Task<Result<DocumentoExigido>> ConstruirDocumentoExigidoAsync(
        ItemDocumentoExigidoInput input,
        ProcessoSeletivo processo,
        ITipoDocumentoReader tipoDocumentoReader,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos,
        IReadOnlyDictionary<string, string>? pontoResolucaoPorFato,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos,
        TipoEntidade? tipoEntidadeRepeticao,
        CancellationToken cancellationToken)
    {
        TipoDocumentoView? tipoDocumento = await tipoDocumentoReader
            .ObterPorIdAsync(input.TipoDocumentoId, cancellationToken)
            .ConfigureAwait(false);
        if (tipoDocumento is null)
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.TipoDocumentoNaoEncontrado",
                $"Tipo de documento {input.TipoDocumentoId} não encontrado ou não está mais vivo."));
        }

        Aplicabilidade aplicabilidade = input.Aplicabilidade switch
        {
            "GERAL" => Aplicabilidade.Geral,
            "CONDICIONAL" => Aplicabilidade.Condicional,
            _ => Aplicabilidade.Nenhuma,
        };

        // Story #922 — gatilho por atributo da entidade: uma folha DENTRO de (ou que É) uma
        // subárvore repetePorEntidade pode citar os fatos de escopo-entidade do tipo (ex.:
        // MAIOR_IDADE/SEM_RENDA para MEMBRO_NUCLEO_FAMILIAR) como se fossem fatos do
        // candidato — mesmo motor de PredicadoDnfValidador, vocabulário estendido. Sem isto,
        // esses gatilhos seriam recusados como PredicadoDnf.FatoDesconhecido: o vocabulário
        // global (IFatoCandidatoReader) não conhece atributos de entidade repetível.
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabularioEfetivo =
            MesclarVocabularioDeEntidade(vocabularioFatos, tipoEntidadeRepeticao);

        Result<IReadOnlyList<CondicaoGatilho>> condicoesResult = ResolverCondicoes(
            input.Condicoes, vocabularioEfetivo, dominiosDinamicos);
        if (condicoesResult.IsFailure)
        {
            return Result<DocumentoExigido>.Failure(condicoesResult.Error!);
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
                return Result<DocumentoExigido>.Failure(gateDeFaseResult.Error!);
            }
        }

        Result<IReadOnlyList<DocumentoExigidoBaseLegal>> basesLegaisResult = ResolverBasesLegais(input.BasesLegais);
        if (basesLegaisResult.IsFailure)
        {
            return Result<DocumentoExigido>.Failure(basesLegaisResult.Error!);
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
            return Result<DocumentoExigido>.Failure(idadeMaximaEmissaoResult.Error!);
        }

        Result<FormatosPermitidos> formatosPermitidosResult = ResolverFormatosPermitidos(input.FormatosPermitidos);
        if (formatosPermitidosResult.IsFailure)
        {
            return Result<DocumentoExigido>.Failure(formatosPermitidosResult.Error!);
        }

        return DocumentoExigido.Criar(
            input.ExigidoNaFaseId,
            tipoDocumento.Id,
            tipoDocumento.Codigo,
            tipoDocumento.Nome,
            tipoDocumento.Categoria,
            aplicabilidade,
            input.Obrigatorio,
            input.ConsequenciaIndeferimento,
            condicoesResult.Value!,
            basesLegaisResult.Value!,
            idadeMaximaEmissaoResult.Value,
            formatosPermitidosResult.Value!,
            input.TamanhoMaximoBytes);
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
    /// Resolve as bases legais de uma folha (Story #554, PR #898, issue #549) — só a forma de
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

    /// <summary>Mesma resolução de <see cref="ResolverBasesLegais"/>, para a base legal PRÓPRIA de um grupo <c>OU</c>/<c>N-de</c> (Story #920) — tipo de entidade diferente (<see cref="NoExigenciaBaseLegal"/>), mesma forma de wire (<see cref="BaseLegalInput"/>).</summary>
    private static Result<IReadOnlyList<NoExigenciaBaseLegal>> ResolverBasesLegaisDeGrupo(IReadOnlyList<BaseLegalInput> inputs)
    {
        List<NoExigenciaBaseLegal> basesLegais = [];
        foreach (BaseLegalInput input in inputs)
        {
            TipoAbrangencia abrangencia = TipoAbrangenciaCodigo.FromCodigo(input.Abrangencia);
            StatusBaseLegal status = StatusBaseLegalCodigo.FromCodigo(input.Status);

            Result<NoExigenciaBaseLegal> baseLegalResult = NoExigenciaBaseLegal.Criar(
                input.Referencia, abrangencia, status, input.Observacao);
            if (baseLegalResult.IsFailure)
            {
                return Result<IReadOnlyList<NoExigenciaBaseLegal>>.Failure(baseLegalResult.Error!);
            }

            basesLegais.Add(baseLegalResult.Value!);
        }

        return Result<IReadOnlyList<NoExigenciaBaseLegal>>.Success(basesLegais);
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

    // Story #922 — schema fechado de atributos por TipoEntidade (mesmo catálogo fechado do
    // domínio, Enums.TipoEntidade) — os NOMES dos fatos de escopo-entidade que uma folha
    // dentro de uma subárvore repetePorEntidade pode citar no gatilho. Ampliar exige nova
    // change, igual ao catálogo de TipoEntidade em si.
    private static readonly IReadOnlyDictionary<string, DescritorFatoCandidato> AtributosMembroNucleoFamiliar =
        new Dictionary<string, DescritorFatoCandidato>(StringComparer.Ordinal)
        {
            ["MAIOR_IDADE"] = DescritorFatoCandidato.Criar("MAIOR_IDADE", TipoDominioFato.Booleano, null).Value!,
            ["SEM_RENDA"] = DescritorFatoCandidato.Criar("SEM_RENDA", TipoDominioFato.Booleano, null).Value!,
            ["SOB_GUARDA"] = DescritorFatoCandidato.Criar("SOB_GUARDA", TipoDominioFato.Booleano, null).Value!,
        };

    /// <summary>
    /// Story #922 — estende o vocabulário fechado de fatos do candidato com os atributos de
    /// escopo-entidade do <paramref name="tipoEntidadeRepeticao"/>, quando a folha está dentro
    /// de (ou é) uma subárvore <c>repetePorEntidade</c>. <see cref="Enums.TipoEntidade.PessoaJuridicaVinculada"/>
    /// não tem atributos (repetição pura) — o vocabulário não muda nesse caso.
    /// </summary>
    private static IReadOnlyDictionary<string, DescritorFatoCandidato> MesclarVocabularioDeEntidade(
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos, TipoEntidade? tipoEntidadeRepeticao)
    {
        if (tipoEntidadeRepeticao != TipoEntidade.MembroNucleoFamiliar)
        {
            return vocabularioFatos ?? new Dictionary<string, DescritorFatoCandidato>();
        }

        Dictionary<string, DescritorFatoCandidato> mesclado = vocabularioFatos is null
            ? new Dictionary<string, DescritorFatoCandidato>(StringComparer.Ordinal)
            : new Dictionary<string, DescritorFatoCandidato>(vocabularioFatos, StringComparer.Ordinal);
        foreach (KeyValuePair<string, DescritorFatoCandidato> atributo in AtributosMembroNucleoFamiliar)
        {
            mesclado[atributo.Key] = atributo.Value;
        }

        return mesclado;
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

                    if (formato is null)
                    {
                        return Result<FormatosPermitidos>.Failure(new DomainError(
                            "FormatosPermitidos.FormaInvalida",
                            "Cada item da lista de formatos permitidos deve declarar 'formato'."));
                    }

                    Result<int?> tamanhoResult = ResolverTamanhoMaximoBytesMax(item);
                    if (tamanhoResult.IsFailure)
                    {
                        return Result<FormatosPermitidos>.Failure(tamanhoResult.Error!);
                    }

                    entradas.Add((formato, tamanhoResult.Value));
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
    /// <c>tamanhoMaximoBytesMax</c> ausente do objeto, ou presente como JSON <c>null</c>, é
    /// "sem teto por formato" — <see langword="null"/>, sucesso. Presente com qualquer outra
    /// forma (string, booleano, número fora do alcance de <see cref="int"/>) é malformado —
    /// falha nomeada, nunca convertido silenciosamente para <see langword="null"/> nem deixado
    /// estourar em <c>GetInt32()</c> (que lançaria antes de qualquer <see cref="Result"/> ser
    /// retornado, virando 500 em vez de 422).
    /// </summary>
    private static Result<int?> ResolverTamanhoMaximoBytesMax(JsonElement item)
    {
        if (!item.TryGetProperty("tamanhoMaximoBytesMax", out JsonElement tamanhoElemento)
            || tamanhoElemento.ValueKind == JsonValueKind.Null)
        {
            return Result<int?>.Success(null);
        }

        if (tamanhoElemento.ValueKind != JsonValueKind.Number || !tamanhoElemento.TryGetInt32(out int tamanho))
        {
            return Result<int?>.Failure(new DomainError(
                "FormatosPermitidos.FormaInvalida",
                "'tamanhoMaximoBytesMax', quando presente, deve ser um número inteiro."));
        }

        return Result<int?>.Success(tamanho);
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
