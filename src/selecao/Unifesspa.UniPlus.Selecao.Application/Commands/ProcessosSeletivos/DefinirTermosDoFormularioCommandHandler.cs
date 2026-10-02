namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
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

        // A forma vem antes das leituras externas; as recusas da forma, do catálogo e da unicidade
        // acumulam no mesmo errors[] (ADR-0125).
        TermosLidos lidos = EscritaDosTermos.Ler(entradas);
        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoConsentimentoReader
            .ListarVersoesAsync([.. entradas.OfType<TermoExigidoInput>().Select(static t => t.VersaoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, catalogo);

        // O formulário avalia a condição do termo com as respostas e os derivados do candidato:
        // o universo são os fatos coletados, os derivados por regra do processo e os derivados do
        // sistema cujas dependências o processo coleta.
        HashSet<string> coletados = new(processo.FatosColetados.Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        HashSet<string> universo = new(
            coletados.Concat(processo.RegrasDerivacao.Select(static r => r.CodigoFato)).Concat(VocabularioDeFatos.DerivadosDoSistemaResolvidos(coletados)),
            StringComparer.Ordinal);
        (List<TermoExigidoFormulario> termos, List<FieldError> erros) =
            EscritaDosTermos.Resolver(lidos, contexto, versoes.ToDictionary(static v => v.VersaoId), universo);

        if (erros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            contexto.Fatos,
            processo.Vinculos(),
            VinculosDeFatos.De([], termos.SelectMany(static t => t.Condicoes).Select(static c => (c.Fato, c.Valor))));
        if (vinculoNovo.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(vinculoNovo.Error!);
        }

        Result definir = processo.DefinirTermosDoFormulario(command.Finalidade, termos, command.Precondicao);
        if (definir.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definir.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
