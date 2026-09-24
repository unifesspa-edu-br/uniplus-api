namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirClassificacaoCommand"/> (Story #775), em duas passadas. A
/// primeira confirma o número de opções de alocação
/// (<see cref="ConfiguracaoClassificacao.ValidarNOpcoesAlocacao"/>) sem tocar o catálogo,
/// acumulando (ADR-0125). Só então a segunda resolve cada regra no catálogo
/// <c>rol_de_regras</c> (<see cref="IRegraCatalogoReader"/>, Story #772), monta os args
/// tipados de cada regra de eliminação conforme o código, repassa <c>BaseadoEmEnem</c> e
/// congela as referências — a invariante ENEM×eliminação e a coerência de arredondamento são
/// validadas por <see cref="ConfiguracaoClassificacao.Criar"/>, que depende da regra de
/// cálculo já resolvida (é ela que distingue classificação local de importada, INV-B8); a que
/// depende de OUTRA dimensão do agregado (INV-B4) é garantida pela raiz
/// (<see cref="ProcessoSeletivo.DefinirClassificacao"/>).
/// </summary>
/// <remarks>
/// Na classificação baseada em ENEM com cálculo local, a resolução de Pesos por Área
/// declarada é resolvida no cadastro da Configuração (<see cref="IPesoAreaEnemReader"/>) e
/// congelada por cópia, grupo a grupo. Quem diz se a resolução está completa é a
/// Configuração, a única que conhece os grupos de área. O reader cross-módulo é resolvido
/// por service location (<c>SelecaoCodegenRegistration</c>, ADR-0098), o mesmo arranjo de
/// <see cref="DefinirBonusRegionalCommandHandler"/>, sem desligar a transação ambiente.
/// </remarks>
public static class DefinirClassificacaoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirClassificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IPesoAreaEnemReader pesoAreaEnemReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(pesoAreaEnemReader);
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

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio
        // que este handler avalia (existência de cadastros, coerência de referências): ela
        // as precede na ordem da ADR-0110 D9. Um cliente com If-Match defasado tem de saber
        // disso antes de sair caçando um cadastro que ele não errou.
        //
        // O que ela NÃO precede é a validação de SCHEMA do payload: o FluentValidation roda
        // como middleware do Wolverine, antes deste handler, e um command malformado morre
        // ali com 422 sem que o guard chegue a rodar. É desvio consciente da D9 — corrigi-lo
        // exigiria carregar o agregado no middleware, o que é pior. O custo é uma rodada
        // extra para quem erra as DUAS coisas ao mesmo tempo; nenhum estado é corrompido.
        //
        // O mesmo guard continua dentro do Definir* do domínio: esta antecipação dá a ordem,
        // não a garantia.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // Acumula (ADR-0125) o número de opções de alocação — a única checagem de
        // ConfiguracaoClassificacao.Criar que não depende de a regra de cálculo já ter sido
        // resolvida no catálogo. Mesmo padrão de DefinirCriteriosDesempateCommandHandler
        // (PR #1216): roda antes de qualquer I/O, ANTES de resolver o rol_de_regras.
        List<FieldError> formaErros = ConfiguracaoClassificacao.ValidarNOpcoesAlocacao(command.NOpcoesAlocacao);
        if (formaErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(formaErros);
        }

        Result<ReferenciaRegra> regraCalculoResult = await ResolverRegraAsync(
            command.RegraCalculoCodigo, command.RegraCalculoVersao, TipoRegra.RegraCalculo, regraCatalogoReader, cancellationToken)
            .ConfigureAwait(false);
        if (regraCalculoResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(regraCalculoResult.Error!);
        }

        ReferenciaRegra? regraArredondamento = null;
        if (command.RegraArredondamentoCodigo is not null)
        {
            if (command.RegraArredondamentoVersao is null)
            {
                return Result<MutacaoAceita>.Failure(new DomainError(
                    "ConfiguracaoClassificacao.RegraArredondamentoVersaoObrigatoria",
                    "Versão da regra de arredondamento é obrigatória quando o código é informado."));
            }

            Result<ReferenciaRegra> regraArredondamentoResult = await ResolverRegraAsync(
                command.RegraArredondamentoCodigo, command.RegraArredondamentoVersao, TipoRegra.RegraArredondamento, regraCatalogoReader, cancellationToken)
                .ConfigureAwait(false);
            if (regraArredondamentoResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(regraArredondamentoResult.Error!);
            }

            regraArredondamento = regraArredondamentoResult.Value!;
        }

        Result<ReferenciaRegra> regraOrdemAlocacaoResult = await ResolverRegraAsync(
            command.RegraOrdemAlocacaoCodigo, command.RegraOrdemAlocacaoVersao, TipoRegra.RegraOrdemAlocacao, regraCatalogoReader, cancellationToken)
            .ConfigureAwait(false);
        if (regraOrdemAlocacaoResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(regraOrdemAlocacaoResult.Error!);
        }

        List<RegraEliminacao> regrasEliminacao = [];
        foreach (RegraEliminacaoInput input in command.RegrasEliminacao)
        {
            Result<RegraEliminacao> resultado = await ResolverEliminacaoAsync(input, regraCatalogoReader, cancellationToken)
                .ConfigureAwait(false);
            if (resultado.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(resultado.Error!);
            }

            regrasEliminacao.Add(resultado.Value!);
        }

        // Só a combinação que aceita a resolução consulta o cadastro, e só com a resolução em
        // forma válida: o texto vira parâmetro da busca, e o Postgres recusaria o caractere nulo
        // com erro de banco. Nas demais combinações, quem responde é ConfiguracaoClassificacao.Criar
        // (resolução indevida ou obrigatória), sem uma leitura que não mudaria a resposta.
        string? resolucaoPesoAreaEnem = command.ResolucaoPesoAreaEnem;
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadroPesoAreaEnem = [];
        IReadOnlyList<FieldError> errosDaResolucao = [];
        if (ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(regraCalculoResult.Value!, command.BaseadoEmEnem)
            && !string.IsNullOrWhiteSpace(command.ResolucaoPesoAreaEnem))
        {
            errosDaResolucao = ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(command.ResolucaoPesoAreaEnem);
            if (errosDaResolucao.Count == 0)
            {
                Result<(string Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Quadro)> congelada =
                    await CongelarQuadroPesoAreaEnemAsync(command.ResolucaoPesoAreaEnem, pesoAreaEnemReader, cancellationToken)
                        .ConfigureAwait(false);
                if (congelada.IsFailure)
                {
                    errosDaResolucao = congelada.Errors;
                }
                else
                {
                    // A forma canônica é a que o cadastro devolveu, não o texto do comando.
                    (resolucaoPesoAreaEnem, quadroPesoAreaEnem) = congelada.Value;
                }
            }
        }

        Result<ConfiguracaoClassificacao> configuracaoResult = ConfiguracaoClassificacao.Criar(
            regraCalculoResult.Value!,
            regraArredondamento,
            command.CasasArredondamento,
            regraOrdemAlocacaoResult.Value!,
            command.NOpcoesAlocacao,
            regrasEliminacao,
            command.BaseadoEmEnem,
            resolucaoPesoAreaEnem,
            quadroPesoAreaEnem);

        // A recusa da resolução — forma inválida ou cadastro que não a resolve — sai junto com
        // as do domínio (ADR-0125), sem as que são só consequência dela.
        List<FieldError> erros = [.. errosDaResolucao];
        if (configuracaoResult.IsFailure)
        {
            erros.AddRange(errosDaResolucao.Count == 0
                ? configuracaoResult.Errors
                : ConfiguracaoClassificacao.SemConsequenciasDaResolucaoRecusada(configuracaoResult.Errors));
        }

        if (erros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        Result result = processo.DefinirClassificacao(configuracaoResult.Value!, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // Agregado tracked: persistência por change detection (ValueGeneratedNever
        // nos filhos) — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    /// <summary>
    /// Resolve a resolução no cadastro de Pesos por Área e copia cada linha por valor. A
    /// resolução sem linha vigente e a resolução sem algum grupo são recusadas no campo da
    /// resolução; a segunda nomeia os grupos que faltam, que é o que o operador precisa
    /// cadastrar.
    /// </summary>
    private static async Task<Result<(string Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Quadro)>> CongelarQuadroPesoAreaEnemAsync(
        string resolucaoInformada,
        IPesoAreaEnemReader pesoAreaEnemReader,
        CancellationToken cancellationToken)
    {
        ResolucaoPesoAreaEnemView? resolucao = await pesoAreaEnemReader
            .ObterPorResolucaoAsync(resolucaoInformada, cancellationToken)
            .ConfigureAwait(false);
        if (resolucao is null)
        {
            return Result<(string Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Quadro)>.ValidationFailure(
            [
                new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError(
                    "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada",
                    "A resolução de Pesos por Área informada não tem pesos cadastrados.")),
            ]);
        }

        if (!resolucao.Completa)
        {
            return Result<(string Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Quadro)>.ValidationFailure(
            [
                new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError(
                    "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIncompleta",
                    $"A resolução de Pesos por Área {resolucao.Resolucao} não tem pesos cadastrados para: "
                    + $"{string.Join(", ", resolucao.GruposAusentes.Select(static grupo => grupo.Rotulo))}.")),
            ]);
        }

        List<GrupoPesoAreaEnemCongelado> quadro = [];
        List<FieldError> erros = [];
        foreach (PesoAreaEnemView linha in resolucao.Linhas)
        {
            Result<GrupoPesoAreaEnemCongelado> grupo = GrupoPesoAreaEnemCongelado.Criar(
                linha.GrupoCurso.Codigo,
                linha.GrupoCurso.Rotulo,
                linha.BaseLegal,
                linha.Areas.Select(static area => ((string?)area.Codigo, (string?)area.Rotulo, area.Peso, area.Corte)));
            if (grupo.IsFailure)
            {
                erros.AddRange(grupo.Errors.Select(static erro => new FieldError(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, erro.Error)));
                continue;
            }

            quadro.Add(grupo.Value!);
        }

        return erros.Count > 0
            ? Result<(string Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Quadro)>.ValidationFailure(erros)
            : Result<(string Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Quadro)>.Success((resolucao.Resolucao, quadro));
    }

    private static async Task<Result<ReferenciaRegra>> ResolverRegraAsync(
        string codigo,
        string versao,
        TipoRegra tipoEsperado,
        IRegraCatalogoReader regraCatalogoReader,
        CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader.ObterAsync(codigo, versao, cancellationToken).ConfigureAwait(false);
        if (regra is null)
        {
            return Result<ReferenciaRegra>.Failure(new DomainError(
                "ConfiguracaoClassificacao.RegraNaoEncontrada",
                $"Regra {codigo}/{versao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != tipoEsperado)
        {
            return Result<ReferenciaRegra>.Failure(new DomainError(
                "ConfiguracaoClassificacao.RegraTipoInvalido",
                $"A regra {codigo}/{versao} não é do tipo esperado."));
        }

        return ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
    }

    private static async Task<Result<RegraEliminacao>> ResolverEliminacaoAsync(
        RegraEliminacaoInput input,
        IRegraCatalogoReader regraCatalogoReader,
        CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(input.RegraCodigo, input.RegraVersao, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.RegraNaoEncontrada",
                $"Regra de eliminação {input.RegraCodigo}/{input.RegraVersao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != TipoRegra.RegraEliminacao)
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.RegraTipoInvalido",
                $"A regra {input.RegraCodigo}/{input.RegraVersao} não é do tipo regra_eliminacao."));
        }

        Result<ArgsRegraEliminacao> argsResult = MontarArgs(input);
        if (argsResult.IsFailure)
        {
            return Result<RegraEliminacao>.Failure(argsResult.Error!);
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return Result<RegraEliminacao>.Failure(referenciaRegraResult.Error!);
        }

        return RegraEliminacao.Criar(referenciaRegraResult.Value!, argsResult.Value!);
    }

    private static Result<ArgsRegraEliminacao> MontarArgs(RegraEliminacaoInput input) =>
        input.RegraCodigo switch
        {
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa => input.EtapaRef is { } etapaRef && input.NotaMinima is { } notaMinima && input.Minimo is null
                ? Result<ArgsRegraEliminacao>.Success(new ArgsElimNotaMinimaEtapa(etapaRef, notaMinima))
                : Result<ArgsRegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.EtapaRefENotaMinimaObrigatorios",
                    $"EtapaRef e NotaMinima são obrigatórios (e Minimo não se aplica) para a regra {RegraEliminacaoCodigo.ElimNotaMinimaEtapa}.")),

            RegraEliminacaoCodigo.ElimCorteRedacao => input.Minimo is { } minimo && input.EtapaRef is null && input.NotaMinima is null
                ? Result<ArgsRegraEliminacao>.Success(new ArgsElimCorteRedacao(minimo))
                : Result<ArgsRegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.MinimoObrigatorio",
                    $"Minimo é obrigatório (e EtapaRef/NotaMinima não se aplicam) para a regra {RegraEliminacaoCodigo.ElimCorteRedacao}.")),

            RegraEliminacaoCodigo.ElimZeroEmArea => input.EtapaRef is null && input.NotaMinima is null && input.Minimo is null
                ? Result<ArgsRegraEliminacao>.Success(new ArgsElimZeroEmArea())
                : Result<ArgsRegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.ArgsIncompativeisComRegra",
                    $"A regra {RegraEliminacaoCodigo.ElimZeroEmArea} não aceita args (EtapaRef/NotaMinima/Minimo).")),

            _ => Result<ArgsRegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.RegraTipoInvalido",
                $"Código de regra de eliminação desconhecido: {input.RegraCodigo}.")),
        };
}
