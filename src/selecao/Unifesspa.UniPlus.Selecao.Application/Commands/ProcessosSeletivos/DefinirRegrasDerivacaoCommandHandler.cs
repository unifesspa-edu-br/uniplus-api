namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirRegrasDerivacaoCommand"/> (Story #985): substitui integralmente as
/// regras de derivação de um processo em rascunho. Resolve na Application (que tem acesso ao
/// vocabulário cross-módulo, ADR-0056) a semântica que o domínio não alcança: o fato alvo é
/// <b>derivado com binding de regra de derivação</b>; cada condição <c>quando</c> valida operador ×
/// domínio × valor do fato citado, contra a oferta do próprio processo para os domínios dinâmicos;
/// todo fato citado está disponível na configuração final (coletado ou derivado no processo); e o
/// código contribuído pertence ao domínio do fato — para <c>MODALIDADE</c>, ao conjunto de
/// modalidades ofertadas. A validação estrutural (código de fato único) e o guard de rascunho são
/// do agregado (<see cref="ProcessoSeletivo.DefinirRegrasDerivacao"/>).
/// </summary>
public static class DefinirRegrasDerivacaoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirRegrasDerivacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
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

        // Guard de rascunho ANTES da resolução do vocabulário cross-módulo.
        if (processo.EdicaoDeRegrasDerivacaoBloqueada() is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        (IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
            IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario) =
            await ResolverVocabularioAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false);

        Dictionary<string, IReadOnlySet<string>> dominiosDinamicos = ResolverDominiosDinamicos(processo);
        IReadOnlyCollection<string> modalidadesOfertadas =
            [.. dominiosDinamicos[RegrasDerivacaoModalidadeLei12711.CodigoFato]];

        // Universo dos fatos disponíveis na configuração final: os coletados pelo processo mais os
        // derivados definidos neste mesmo comando (a substituição é integral). Uma condição só pode
        // citar um fato deste universo — coerente com a recusa de fato citado inexistente do publish.
        HashSet<string> universo = new(StringComparer.Ordinal);
        foreach (FatoColetado fato in processo.FatosColetados)
        {
            universo.Add(fato.FatoCodigo);
        }

        foreach (ConfiguracaoDerivacaoInput configInput in command.Configuracoes)
        {
            universo.Add(configInput.CodigoFato);
        }

        List<ConfiguracaoDerivacaoFato> configuracoes = [];
        foreach (ConfiguracaoDerivacaoInput configInput in command.Configuracoes)
        {
            Result<ConfiguracaoDerivacaoFato> configResult =
                ResolverConfiguracao(configInput, catalogo, vocabulario, universo, dominiosDinamicos, modalidadesOfertadas);
            if (configResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(configResult.Error!);
            }

            configuracoes.Add(configResult.Value!);
        }

        Result result = processo.DefinirRegrasDerivacao(configuracoes);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    private static Result<ConfiguracaoDerivacaoFato> ResolverConfiguracao(
        ConfiguracaoDerivacaoInput configInput,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlySet<string> universo,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dominiosDinamicos,
        IReadOnlyCollection<string> modalidadesOfertadas)
    {
        // Alvo: o fato tem de existir e ser derivado com o binding da própria regra de derivação
        // (REGRA_DERIVACAO:{codigo}). Um fato declarado, ou derivado por outro mecanismo, não tem
        // regras configuráveis. No vocabulário atual, MODALIDADE é o único alvo válido.
        if (!catalogo.TryGetValue(configInput.CodigoFato, out FatoCandidatoView? view))
        {
            return Result<ConfiguracaoDerivacaoFato>.Failure(new DomainError(
                DerivabilidadeDeFato.FatoDesconhecido,
                $"O fato '{configInput.CodigoFato}' não pertence ao vocabulário de fatos do candidato."));
        }

        if (!DerivabilidadeDeFato.EhAlvoDeDerivacao(view))
        {
            return Result<ConfiguracaoDerivacaoFato>.Failure(new DomainError(
                DerivabilidadeDeFato.FatoNaoDerivavel,
                $"O fato '{configInput.CodigoFato}' não é um alvo de derivação — só um fato derivado com binding de "
                + "regra de derivação pode ter regras configuradas."));
        }

        List<RegraDerivacaoConfigurada> regras = [];
        foreach (RegraDerivacaoInput regraInput in configInput.Regras)
        {
            Result<RegraDerivacaoConfigurada> regraResult =
                ResolverRegra(regraInput, vocabulario, universo, dominiosDinamicos);
            if (regraResult.IsFailure)
            {
                return Result<ConfiguracaoDerivacaoFato>.Failure(regraResult.Error!);
            }

            regras.Add(regraResult.Value!);
        }

        Result<ConfiguracaoDerivacaoFato> configResult = ConfiguracaoDerivacaoFato.Criar(configInput.CodigoFato, regras);
        if (configResult.IsFailure)
        {
            return configResult;
        }

        // Domínio de contribuição: só MODALIDADE tem domínio validável nesta Story — cada código
        // contribuído tem de ser uma das modalidades ofertadas pelo processo (mesmo domínio e mesmo
        // erro do gate de publicação). Um segundo fato derivado com binding de regra, quando existir
        // no seed, estende esta validação; até lá, a estrutura das regras já foi conferida.
        if (string.Equals(configInput.CodigoFato, RegrasDerivacaoModalidadeLei12711.CodigoFato, StringComparison.Ordinal))
        {
            Result<RegrasDerivacaoFato> dominioResult = configResult.Value!.ParaRegrasDerivacao(modalidadesOfertadas);
            if (dominioResult.IsFailure)
            {
                return Result<ConfiguracaoDerivacaoFato>.Failure(dominioResult.Error!);
            }
        }

        return configResult;
    }

    /// <summary>
    /// Monta e valida uma regra. A regra <b>âncora</b> (sem condições, sempre verdadeira) tem
    /// <c>Quando</c> nulo. Cada condição valida a forma (<see cref="CondicaoDnf.Criar"/>) e, no
    /// conjunto, a semântica do predicado (<see cref="PredicadoDnfValidador"/>) — fato citado no
    /// vocabulário, disponível na configuração final, operador × domínio, valor × domínio.
    /// </summary>
    private static Result<RegraDerivacaoConfigurada> ResolverRegra(
        RegraDerivacaoInput regraInput,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlySet<string> universo,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dominiosDinamicos)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        List<CondicaoRegraDerivacao> condicoes = [];
        IReadOnlyList<IReadOnlyList<CondicaoDerivacaoInput>> quando = regraInput.Quando ?? [];
        for (int clausula = 0; clausula < quando.Count; clausula++)
        {
            foreach (CondicaoDerivacaoInput condicaoInput in quando[clausula])
            {
                // Defesa em profundidade: uma condição nula (JSON `[[null]]`) já é barrada pelo
                // validator (400), mas nunca deve virar NullReferenceException/500 aqui.
                if (condicaoInput is null)
                {
                    return Result<RegraDerivacaoConfigurada>.Failure(new DomainError(
                        CondicaoRegraDerivacaoErrorCodes.ClausulaInvalida,
                        "O predicado da regra contém uma condição nula."));
                }

                Operador operador = OperadorCodigo.FromCodigo(condicaoInput.Operador);

                Result<CondicaoRegraDerivacao> condicaoResult =
                    CondicaoRegraDerivacao.Criar(clausula, condicaoInput.Fato, operador, condicaoInput.Valor);
                if (condicaoResult.IsFailure)
                {
                    return Result<RegraDerivacaoConfigurada>.Failure(condicaoResult.Error!);
                }

                condicoes.Add(condicaoResult.Value!);
                linhas.Add((clausula, CondicaoDnf.Criar(condicaoInput.Fato, operador, condicaoInput.Valor).Value!));
            }
        }

        if (linhas.Count > 0)
        {
            Result<PredicadoDnf> predicadoResult = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
            if (predicadoResult.IsFailure)
            {
                return Result<RegraDerivacaoConfigurada>.Failure(predicadoResult.Error!);
            }

            Result validacao = PredicadoDnfValidador.Validar(predicadoResult.Value!, vocabulario, universo, dominiosDinamicos);
            if (validacao.IsFailure)
            {
                return Result<RegraDerivacaoConfigurada>.Failure(validacao.Error!);
            }
        }

        return RegraDerivacaoConfigurada.Criar(regraInput.Ordem, regraInput.Contribui, condicoes);
    }

    private static async Task<(
        IReadOnlyDictionary<string, FatoCandidatoView> Catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario)> ResolverVocabularioAsync(
        IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal);
        Dictionary<string, DescritorFatoCandidato> vocabulario = new(StringComparer.Ordinal);
        foreach (FatoCandidatoView fato in fatos)
        {
            catalogo[fato.Codigo] = fato;

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

        return (catalogo, vocabulario);
    }

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
            [RegrasDerivacaoModalidadeLei12711.CodigoFato] = modalidades,
            ["CONDICAO_ATENDIMENTO"] = condicoesAtendimento,
            ["TIPO_DEFICIENCIA"] = tiposDeficiencia,
        };
    }
}

/// <summary>
/// Política de derivabilidade de um fato (Story #985). Só pode ter regras de derivação configuradas
/// o fato de <c>Origem = DERIVADO</c> cujo binding é o da própria regra de derivação
/// (<c>REGRA_DERIVACAO:{codigo}</c>). Um fato declarado, ou derivado por outro mecanismo (ex.:
/// computado de atributo do candidato), não é alvo de configuração de regras.
/// </summary>
internal static class DerivabilidadeDeFato
{
    public const string FatoDesconhecido = "ConfiguracaoDerivacaoFato.FatoDesconhecido";
    public const string FatoNaoDerivavel = "ConfiguracaoDerivacaoFato.FatoNaoDerivavel";

    private const string OrigemDerivado = "DERIVADO";
    private const string PrefixoBindingRegraDerivacao = "REGRA_DERIVACAO:";

    public static bool EhAlvoDeDerivacao(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        return string.Equals(fato.Origem, OrigemDerivado, StringComparison.Ordinal)
            && string.Equals(fato.Binding, PrefixoBindingRegraDerivacao + fato.Codigo, StringComparison.Ordinal);
    }
}
