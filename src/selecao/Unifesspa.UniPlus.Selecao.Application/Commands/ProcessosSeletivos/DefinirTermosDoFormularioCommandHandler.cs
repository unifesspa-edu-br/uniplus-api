namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// Confere a forma de todos os termos antes de qualquer leitura externa e acumula as recusas por
/// termo (ADR-0125). Depois resolve as versões no catálogo de termos e valida as condições contra
/// os fatos que o processo coleta ou deriva, os mesmos com que o formulário as avalia.
/// </summary>
public static class DefinirTermosDoFormularioCommandHandler
{
    public static async Task<Result> Handle(
        DefinirTermosDoFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        ITermoConsentimentoReader termoConsentimentoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(termoConsentimentoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return processo is null
            ? ProcessoNaoEncontrado(command.ProcessoSeletivoId)
            : await DefinirAsync(command, processo, fatoCandidatoReader, termoConsentimentoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result ProcessoNaoEncontrado(Guid processoSeletivoId) =>
        Result.Failure(new DomainError("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {processoSeletivoId} não encontrado."));

    private static async Task<Result> DefinirAsync(
        DefinirTermosDoFormularioCommand command,
        ProcessoSeletivo processo,
        IFatoCandidatoReader fatoCandidatoReader,
        ITermoConsentimentoReader termoConsentimentoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (RecusaAntesDoCatalogo(command, processo) is { } recusa)
        {
            return recusa;
        }

        TermosResolvidosNoCatalogo resolvidos =
            await ResolverNoCatalogoAsync(command.Termos, processo, fatoCandidatoReader, termoConsentimentoReader, cancellationToken).ConfigureAwait(false);
        return resolvidos.Erros.Count > 0
            ? Result.ValidationFailure(resolvidos.Erros)
            : await GravarAsync(command, processo, resolvidos, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fora do rascunho a recusa vem antes de consultar cadastros; a lista de termos é obrigatória, e a vazia os remove.</summary>
    private static Result? RecusaAntesDoCatalogo(DefinirTermosDoFormularioCommand command, ProcessoSeletivo processo)
    {
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        return command.Termos is null
            ? Result.Failure(new DomainError(
                TermoExigidoFormularioErrorCodes.EntradaMalformada, "A lista de termos é obrigatória; a lista vazia remove os termos."))
            : null;
    }

    /// <summary>
    /// A forma vem antes das leituras externas; as recusas da forma, do catálogo e da unicidade acumulam no mesmo
    /// <c>errors[]</c> (ADR-0125).
    /// </summary>
    private static async Task<TermosResolvidosNoCatalogo> ResolverNoCatalogoAsync(
        IReadOnlyList<TermoExigidoInput?> entradas,
        ProcessoSeletivo processo,
        IFatoCandidatoReader fatoCandidatoReader,
        ITermoConsentimentoReader termoConsentimentoReader,
        CancellationToken cancellationToken)
    {
        TermosLidos lidos = EscritaDosTermos.Ler(entradas);
        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoConsentimentoReader
            .ListarVersoesAsync([.. entradas.OfType<TermoExigidoInput>().Select(static t => t.Termo?.VersaoId).OfType<Guid>().Distinct()], cancellationToken)
            .ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false));
        (List<TermoExigidoFormulario> termos, List<FieldError> erros) =
            EscritaDosTermos.Resolver(lidos, contexto, versoes.ToDictionary(static v => v.VersaoId), UniversoDeFatos(processo));
        return new TermosResolvidosNoCatalogo(contexto, termos, erros);
    }

    /// <summary>
    /// O formulário avalia a condição do termo com as respostas e os derivados do candidato: o universo são os fatos
    /// coletados, os derivados por regra do processo e os derivados do sistema cujas dependências o processo coleta.
    /// </summary>
    private static HashSet<string> UniversoDeFatos(ProcessoSeletivo processo)
    {
        HashSet<string> coletados = new(processo.FatosColetados.Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        return new(
            coletados.Concat(processo.DerivacoesEfetivas().Select(static r => r.CodigoFato)).Concat(VocabularioDeFatos.DerivadosDoSistemaResolvidos(coletados)),
            StringComparer.Ordinal);
    }

    private static async Task<Result> GravarAsync(
        DefinirTermosDoFormularioCommand command,
        ProcessoSeletivo processo,
        TermosResolvidosNoCatalogo resolvidos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result vinculoNovo = ConferirVinculoNovo(processo, resolvidos);
        if (vinculoNovo.IsFailure)
        {
            return Result.Failure(vinculoNovo.Error!);
        }

        Result definido = Definir(command, processo, resolvidos.Termos);
        if (definido.IsFailure)
        {
            return Result.ValidationFailure(definido.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static Result Definir(DefinirTermosDoFormularioCommand command, ProcessoSeletivo processo, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        Result<CodigoDoFormulario> codigo = RegrasDosFormularios.CodigoDoCaminho(command.FormularioCodigo);
        return codigo.IsFailure ? Result.Failure(codigo.Error!) : processo.DefinirTermosDoFormulario(codigo.Value!, termos);
    }

    private static Result ConferirVinculoNovo(ProcessoSeletivo processo, TermosResolvidosNoCatalogo resolvidos) =>
        ConferenciaDeVinculoNovo.Conferir(
            resolvidos.Contexto.Fatos,
            ReferenciasVivas.Vinculos(processo.Declaracao()),
            VinculosDeFatos.De([], resolvidos.Termos.SelectMany(static t => t.Condicoes).Select(static c => (c.Fato, c.Valor))));

    /// <summary>Os termos resolvidos contra o catálogo, o contexto que os conferiu e as recusas de toda a leitura.</summary>
    private sealed record TermosResolvidosNoCatalogo(ContextoDoCatalogo Contexto, List<TermoExigidoFormulario> Termos, List<FieldError> Erros);
}
