namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Confere a forma de todos os termos antes de qualquer leitura externa e acumula as recusas por
/// termo (ADR-0125). Depois resolve as versões no catálogo de termos e valida as condições contra
/// os fatos que o processo coleta ou deriva, os mesmos com que o formulário as avalia.
/// </summary>
public static class DefinirTermosDoFormularioCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirTermosDoFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        ITermoConsentimentoReader termoConsentimentoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(termoConsentimentoReader);
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

        // A precondição precede a resolução de leituras externas (ADR-0110 D9).
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        if (command.Termos is not { } entradas)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                TermoExigidoFormularioErrorCodes.EntradaMalformada, "A lista de termos é obrigatória; a lista vazia remove os termos."));
        }

        // Forma, leitura do catálogo e unicidade acumulam no mesmo errors[], sem retorno entre elas
        // (ADR-0125); o termo de forma inválida só não segue para a conferência contra o catálogo.
        List<FieldError> erros = [];
        List<(PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade)?> condicoes = [];
        for (int i = 0; i < entradas.Count; i++)
        {
            int recusasAntes = erros.Count;
            (PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade) forma = ConferirForma(entradas[i], $"termos[{i}]", erros);
            condicoes.Add(erros.Count == recusasAntes ? (forma.Exibicao, forma.Obrigatoriedade!) : null);
        }

        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoConsentimentoReader
            .ListarVersoesAsync([.. entradas.OfType<TermoExigidoInput>().Select(static t => t.VersaoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, VersaoTermoConsentimentoView> versaoPorId = versoes.ToDictionary(static v => v.VersaoId);

        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, DescritorFatoCandidato> vocabulario = VocabularioDeFatos.Descritores(catalogo);
        Dictionary<string, DominioDeValores> dominiosDinamicos = VocabularioDeFatos.DominiosDinamicos(processo, catalogo);

        // O formulário avalia a condição do termo com as respostas e os derivados do candidato:
        // o universo são os fatos coletados e os derivados por regra do processo.
        HashSet<string> universo = new(
            processo.FatosColetados.Select(static f => f.FatoCodigo).Concat(processo.RegrasDerivacao.Select(static r => r.CodigoFato)),
            StringComparer.Ordinal);

        List<TermoExigidoFormulario> termos = [];
        for (int i = 0; i < entradas.Count; i++)
        {
            if (condicoes[i] is not { } condicao)
            {
                continue;
            }

            TermoExigidoInput entrada = entradas[i];
            string campo = $"termos[{i}]";

            // As condições não dependem da versão: conferidas antes dela, as recusas do termo saem
            // juntas.
            (PredicadoDnf? exibicao, Obrigatoriedade obrigatoriedade) = condicao;
            if (ValidarPredicado(exibicao, vocabulario, universo, dominiosDinamicos) is { } erroExibicao)
            {
                erros.Add(new($"{campo}.exibicao", erroExibicao));
            }

            if (ValidarPredicado(obrigatoriedade.Predicado, vocabulario, universo, dominiosDinamicos) is { } erroObrigatoriedade)
            {
                erros.Add(new($"{campo}.predicadoObrigatoriedade", erroObrigatoriedade));
            }

            if (!versaoPorId.TryGetValue(entrada.VersaoId, out VersaoTermoConsentimentoView? versao) || versao.TermoId != entrada.TermoId)
            {
                erros.Add(new($"{campo}.versaoId", new DomainError(
                    TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada,
                    "A versão informada não existe no catálogo de termos, ou pertence a outro termo.")));
                continue;
            }

            Result<TermoExigidoFormulario> termo = TermoExigidoFormulario.Criar(
                entrada.Codigo,
                entrada.Ordem,
                new VersaoTermoEscolhida(versao.TermoId, versao.VersaoId, versao.Nome, versao.Texto, versao.BaseLegal, versao.FormaAceite, versao.Hash),
                exibicao,
                obrigatoriedade);
            if (termo.IsSuccess)
            {
                termos.Add(termo.Value!);
            }
            else
            {
                erros.AddRange(termo.Errors.Select(e => new FieldError($"{campo}.{e.Field}", e.Error)));
            }
        }

        erros.AddRange(TermoExigidoFormulario.ConferirUnicidade(
            [.. entradas.Select(static t => t is null ? null : ((string?, int)?)(t.Codigo, t.Ordem))]));
        if (erros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            catalogo.ToDictionary(static f => f.Codigo, StringComparer.Ordinal),
            processo.Vinculos(),
            VinculosDeFatos.De([], termos.SelectMany(static t => t.Condicoes).Select(static c => (c.Fato, c.Valor))));
        if (vinculoNovo.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(vinculoNovo.Error!);
        }

        Result definir = processo.DefinirTermosDoFormulario(termos, command.Precondicao);
        if (definir.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definir.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    /// <summary>
    /// A forma do termo sem leitura externa: código, ordem, exibição e obrigatoriedade coerente — só
    /// <c>QUANDO</c> tem predicado, e ele o tem sempre.
    /// </summary>
    private static (PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade) ConferirForma(
        TermoExigidoInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(new(campo, new DomainError(TermoExigidoFormularioErrorCodes.EntradaMalformada, "O termo veio nulo.")));
            return (null, null);
        }

        erros.AddRange(TermoExigidoFormulario.ValidarFormaBasica(entrada.Codigo, entrada.Ordem)
            .Select(e => new FieldError($"{campo}.{e.Field}", e.Error)));

        Result<PredicadoDnf?> exibicao = Montar(entrada.Exibicao);
        if (exibicao.IsFailure)
        {
            erros.Add(new($"{campo}.exibicao", exibicao.Error!));
        }

        Result<PredicadoDnf?> predicado = Montar(entrada.PredicadoObrigatoriedade);
        if (predicado.IsFailure)
        {
            erros.Add(new($"{campo}.predicadoObrigatoriedade", predicado.Error!));
            return (exibicao.Value, null);
        }

        Obrigatoriedade? obrigatoriedade = (PredicadoDnfJson.TipoDoToken(entrada.Obrigatoriedade), predicado.Value) switch
        {
            (TipoObrigatoriedade.Sempre, null) => Obrigatoriedade.Sempre,
            (TipoObrigatoriedade.Nunca, null) => Obrigatoriedade.Nunca,
            (TipoObrigatoriedade.Quando, { } quando) => Obrigatoriedade.Quando(quando),
            _ => null,
        };
        if (obrigatoriedade is null)
        {
            erros.Add(new($"{campo}.obrigatoriedade", new DomainError(
                TermoExigidoFormularioErrorCodes.ObrigatoriedadeInvalida,
                "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.")));
        }

        return (exibicao.Value, obrigatoriedade);
    }

    /// <summary>O predicado da entrada; nulo ou sem cláusula é ausência de condição.</summary>
    private static Result<PredicadoDnf?> Montar(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? clausulas)
    {
        if (clausulas is null || clausulas.Count == 0)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            if (clausulas[c] is not { Count: > 0 } condicoes)
            {
                return Result<PredicadoDnf?>.Failure(new DomainError("ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição."));
            }

            foreach (CondicaoPrecondicaoInput? condicao in condicoes)
            {
                if (condicao is null)
                {
                    return Result<PredicadoDnf?>.Failure(new DomainError(
                        CondicaoPrecondicaoFatoErrorCodes.ClausulaInvalida, "O predicado contém uma condição nula."));
                }

                Result<CondicaoDnf> criada = CondicaoDnf.Criar(condicao.Fato, OperadorCodigo.FromCodigo(condicao.Operador), condicao.Valor);
                if (criada.IsFailure)
                {
                    return Result<PredicadoDnf?>.Failure(criada.Error!);
                }

                linhas.Add((c, criada.Value!));
            }
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.Failure(predicado.Error!);
    }

    private static DomainError? ValidarPredicado(
        PredicadoDnf? predicado,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlySet<string> universo,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        predicado is null ? null : PredicadoDnfValidador.Validar(predicado, vocabulario, universo, dominiosDinamicos).Error;
}
