namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Handler do <see cref="DefinirRegrasDerivacaoCommand"/> (Story #985): substitui integralmente as
/// regras de derivação de um processo em rascunho, em duas passadas. A primeira confirma a
/// <b>forma básica</b> de TODAS as configurações e regras (<see cref="ConfiguracaoDerivacaoFato.ValidarFormaBasica"/>/
/// <see cref="RegraDerivacaoConfigurada.ValidarFormaBasica"/>) sem tocar o vocabulário
/// cross-módulo, acumulando (ADR-0125) através de toda a lista. Só então a segunda resolve, na
/// Application (que tem acesso ao vocabulário cross-módulo, ADR-0056), a semântica que o domínio
/// não alcança: o fato alvo é <b>derivado com binding de regra de derivação</b>; cada condição
/// <c>quando</c> valida operador × domínio × valor do fato citado, contra a oferta do próprio
/// processo para os domínios dinâmicos; todo fato citado está disponível na configuração final
/// (coletado ou derivado no processo); e o código contribuído pertence ao domínio do fato — para
/// <c>MODALIDADE</c>, ao conjunto de modalidades ofertadas — parando na primeira configuração/
/// regra que falhar, granularidade nunca coberta pelo FluentValidation. O guard de rascunho é do
/// agregado (<see cref="ProcessoSeletivo.DefinirRegrasDerivacao"/>).
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

        // Precondição / bloqueio de mutação pós-publicação sem sessão ANTES da resolução do
        // vocabulário cross-módulo. O mesmo guard continua dentro de DefinirRegrasDerivacao.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // Acumula (ADR-0125) a forma básica de TODAS as configurações e regras numa primeira
        // passada, ANTES de resolver o vocabulário cross-módulo — mesmo padrão de
        // DefinirFatosColetadosCommandHandler (PR #1214): rodar depois da resolução semântica
        // trocaria um código/contribuição vazios por um erro semântico menos específico, e uma
        // violação de forma podia ser mascarada pelo erro semântico de outra configuração/regra
        // do mesmo payload.
        List<FieldError> formaErros = [];
        for (int indiceConfig = 0; indiceConfig < command.Configuracoes.Count; indiceConfig++)
        {
            ConfiguracaoDerivacaoInput configInput = command.Configuracoes[indiceConfig];
            List<FieldError> configErros = ConfiguracaoDerivacaoFato.ValidarFormaBasica(
                configInput.CodigoFato, configInput.Regras.Count, configInput.Regras.Select(static r => r.Ordem));
            formaErros.AddRange(configErros.Select(erro => erro with { Field = $"configuracoes[{indiceConfig}].{erro.Field}" }));

            for (int indiceRegra = 0; indiceRegra < configInput.Regras.Count; indiceRegra++)
            {
                RegraDerivacaoInput regraInput = configInput.Regras[indiceRegra];
                List<FieldError> regraErros = RegraDerivacaoConfigurada.ValidarFormaBasica(regraInput.Ordem, regraInput.Contribui);
                formaErros.AddRange(regraErros.Select(erro =>
                    erro with { Field = $"configuracoes[{indiceConfig}].regras[{indiceRegra}].{erro.Field}" }));
            }
        }

        if (formaErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(formaErros);
        }

        (IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
            IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario) =
            await ResolverVocabularioAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false);

        Dictionary<string, DominioDeValores> dominiosDinamicos =
            VocabularioDeFatos.DominiosDinamicos(processo, catalogo.Values);

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
                ResolverConfiguracao(configInput, catalogo, vocabulario, universo, dominiosDinamicos);
            if (configResult.IsFailure)
            {
                return Result<MutacaoAceita>.ValidationFailure(configResult.Errors);
            }

            configuracoes.Add(configResult.Value!);
        }

        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            catalogo,
            processo.Vinculos(),
            VinculosDeFatos.De(
                configuracoes.Select(static c => c.CodigoFato),
                configuracoes.SelectMany(static c => c.Regras).SelectMany(static r => r.Condicoes).Select(static c => (c.Fato, c.Valor)),
                configuracoes.SelectMany(static c => c.Regras.Select(r => (c.CodigoFato, r.Contribui)))));
        if (vinculoNovo.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(vinculoNovo.Error!);
        }

        Result result = processo.DefinirRegrasDerivacao(configuracoes, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // Rascunho puro: 204 sem ETag. Sob sessão de retificação: 204 com o ETag da nova revisão.
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    private static Result<ConfiguracaoDerivacaoFato> ResolverConfiguracao(
        ConfiguracaoDerivacaoInput configInput,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlySet<string> universo,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        // Alvo: o fato tem de existir e ser derivado com o binding da própria regra de derivação
        // (REGRA_DERIVACAO:{codigo}). Um fato declarado, ou derivado por outro mecanismo, não tem
        // regras configuráveis.
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
                $"O fato '{configInput.CodigoFato}' não é um alvo de derivação — só um fato categórico derivado com "
                + "binding de regra de derivação pode ter regras configuradas."));
        }

        List<RegraDerivacaoConfigurada> regras = [];
        foreach (RegraDerivacaoInput regraInput in configInput.Regras)
        {
            Result<RegraDerivacaoConfigurada> regraResult =
                ResolverRegra(regraInput, vocabulario, universo, dominiosDinamicos);
            if (regraResult.IsFailure)
            {
                return Result<ConfiguracaoDerivacaoFato>.ValidationFailure(regraResult.Errors);
            }

            regras.Add(regraResult.Value!);
        }

        Result<ConfiguracaoDerivacaoFato> configResult = ConfiguracaoDerivacaoFato.Criar(configInput.CodigoFato, regras);
        if (configResult.IsFailure)
        {
            return configResult;
        }

        // Domínio de contribuição: todo código contribuído pertence aos valores que o fato tem no
        // processo, pela fonte (ADR-0136) — modalidades ofertadas, opções declaradas, municípios do
        // bônus ou todos os valores do catálogo. O valor desativado do catálogo pertence ao domínio
        // e é recusado adiante, como vínculo novo. A fonte que o servidor não enumera (Geo) não tem
        // conferência aqui.
        if (VocabularioDeFatos.DominioDeContribuicao(view, dominiosDinamicos) is { } dominio)
        {
            Result<RegrasDerivacaoFato> dominioResult = configResult.Value!.ParaRegrasDerivacao(dominio);
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
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
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

            TipoDominioFato? tipoDominio = VocabularioDeFatos.Classificar(fato);

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

    private const string DominioCategorico = "CATEGORICO";

    /// <summary>
    /// A regra do processo contribui código, então o alvo é categórico: o derivado booleano ainda
    /// não tem regra configurável no processo, e aceitá-lo gravaria contribuições que o motor
    /// emitiria como lista num fato declarado booleano.
    /// </summary>
    public static bool EhAlvoDeDerivacao(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        return string.Equals(fato.Origem, OrigemDerivado, StringComparison.Ordinal)
            && string.Equals(fato.Dominio, DominioCategorico, StringComparison.Ordinal)
            && string.Equals(fato.Binding, PrefixoBindingRegraDerivacao + fato.Codigo, StringComparison.Ordinal);
    }
}
