namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// Handler do <see cref="DefinirRegrasDerivacaoCommand"/>: substitui integralmente as
/// regras de derivação de um processo em rascunho. A leitura e a conferência das derivações
/// (<see cref="ResolucaoDasDerivacoes"/>) acumulam as recusas (ADR-0125) através de todas as configurações e
/// regras: a forma da configuração, das regras e das condições e, na Application, que tem acesso ao vocabulário
/// cross-módulo (ADR-0056), a semântica que o domínio não alcança — o fato alvo é <b>derivado com binding de
/// regra de derivação</b>; cada condição <c>quando</c> valida operador × domínio × valor do fato citado, contra a
/// oferta do próprio processo para os domínios dinâmicos; todo fato citado está disponível na configuração final
/// (coletado ou derivado no processo); e o código contribuído pertence ao domínio do fato. A modalidade do
/// candidato e a opção pelas cotas da lei vêm da oferta de vagas e são recusadas como forma. O guard de rascunho é
/// do agregado (<see cref="ProcessoSeletivo.DefinirRegrasDerivacao"/>).
/// </summary>
public static class DefinirRegrasDerivacaoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirRegrasDerivacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ObterParaEdicaoAsync(processoSeletivoRepository, command, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, fatoCandidatoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // O bloqueio de mutação fora do rascunho vem ANTES da resolução do vocabulário cross-módulo. O mesmo guard
    // continua dentro de DefinirRegrasDerivacao.
    private static async Task<Result<ProcessoSeletivo>> ObterParaEdicaoAsync(
        IProcessoSeletivoRepository repositorio, DefinirRegrasDerivacaoCommand command, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return processo.MutacaoBloqueada() is { } bloqueio
            ? Result<ProcessoSeletivo>.Failure(bloqueio)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirRegrasDerivacaoCommand command,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, fatos);
        Result<List<ConfiguracaoDerivacaoFato>> configuracoes = ResolucaoDasDerivacoes.Resolver(processo, command.Configuracoes, contexto);
        Result definido = configuracoes.IsFailure
            ? Result.ValidationFailure(configuracoes.Errors)
            : DefinirNoProcesso(processo, contexto.Fatos, configuracoes.Value!);
        if (definido.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return definido;
    }

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo, List<ConfiguracaoDerivacaoFato> configuracoes)
    {
        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            catalogo,
            ReferenciasVivas.Vinculos(processo.Declaracao()),
            VinculosDeFatos.De(
                configuracoes.Select(static c => c.CodigoFato),
                configuracoes.SelectMany(static c => c.Regras).SelectMany(static r => r.Condicoes).Select(static c => (c.Fato, c.Valor)),
                configuracoes.SelectMany(static c => c.CodigosContribuidos)));

        return vinculoNovo.IsFailure ? Result.Failure(vinculoNovo.Error!) : processo.DefinirRegrasDerivacao(configuracoes);
    }
}

/// <summary>
/// Política de derivabilidade de um fato. Só pode ter regras de derivação configuradas
/// o fato de <c>Origem = DERIVADO</c> cujo binding é o da própria regra de derivação
/// (<c>REGRA_DERIVACAO:{codigo}</c>). Um fato declarado, ou derivado por outro mecanismo (ex.:
/// computado de atributo do candidato), não é alvo de configuração de regras.
/// </summary>
internal static class DerivabilidadeDeFato
{
    public const string FatoDesconhecido = "ConfiguracaoDerivacaoFato.FatoDesconhecido";
    public const string FatoNaoDerivavel = "ConfiguracaoDerivacaoFato.FatoNaoDerivavel";
    public const string ContribuicaoIncoerenteComODominio = "ConfiguracaoDerivacaoFato.ContribuicaoIncoerenteComODominio";

    private const string DominioCategorico = "CATEGORICO";

    /// <summary>
    /// O derivado por regra é categórico, por união dos códigos contribuídos, ou booleano,
    /// verdadeiro quando alguma regra ativa (ADR-0136).
    /// </summary>
    public static bool EhAlvoDeDerivacao(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        return string.Equals(fato.Origem, DominioDoCatalogo.OrigemDerivada, StringComparison.Ordinal)
            && (string.Equals(fato.Dominio, DominioCategorico, StringComparison.Ordinal)
                || string.Equals(fato.Dominio, VocabularioDeFatos.DominioBooleano, StringComparison.Ordinal))
            && string.Equals(fato.Binding, VinculoDeFato.De(VinculoDeFato.RegraDeDerivacao, fato.Codigo), StringComparison.Ordinal);
    }
}
