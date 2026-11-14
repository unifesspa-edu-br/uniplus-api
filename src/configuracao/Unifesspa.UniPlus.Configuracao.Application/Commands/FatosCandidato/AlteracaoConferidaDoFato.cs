namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A alteração de um fato que o agregado aceita é conferida contra o que cita o fato (ADR-0136) antes de
/// gravar: as regras padrão de outros derivados, os agregados, os modelos de formulário e as regras de
/// aplicabilidade das modalidades. A recusa descarta o fato rastreado, que a validação do agregado e a das
/// citações usam com a alteração já aplicada.
/// </summary>
internal static class AlteracaoConferidaDoFato
{
    public static async Task<Result> AplicarAsync(
        Guid id,
        Func<FatoCandidato, CatalogoDeFatos, Result> alteracao,
        IFatoCandidatoRepository repository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IModeloFormularioRepository modeloRepository,
        IModalidadeRepository modalidadeRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        // A alteração é conferida contra as regras padrão do catálogo inteiro: as escritas dessas regras e
        // esta são serializadas.
        await repository.TravarRegrasPadraoParaEscritaAsync(cancellationToken).ConfigureAwait(false);

        // O fato inexistente (404) é recusado antes de qualquer carga; os modelos e as modalidades,
        // que são a carga pesada, só são lidos depois de o próprio fato aceitar a alteração.
        return await MutacaoDoFato.AplicarAsync(
            id,
            async fato =>
            {
                IReadOnlyList<FatoCandidato> fatos = await repository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
                IReadOnlyList<PrecedenciaFase> precedencias = await precedenciaRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);
                CatalogoDeFatos antes = new(fatos, precedencias);

                Result alterar = alteracao(fato, antes);
                if (alterar.IsFailure)
                {
                    return alterar;
                }

                IReadOnlyList<ModeloFormulario> modelos = await modeloRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
                IReadOnlyList<Modalidade> modalidades = await modalidadeRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);

                CatalogoDeFatos depois = new(
                    [.. fatos.Where(f => f.Id != fato.Id), fato], precedencias);
                List<FieldError> citacoes = ConferenciaDasCitacoesDoFato.Conferir(fato.Codigo, antes, depois, modelos, modalidades);
                if (citacoes.Count == 0)
                {
                    return Result.Success();
                }

                // O agregado já recebeu a alteração; sem descartar, o save do Wolverine a gravaria.
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result.ValidationFailure(citacoes);
            },
            repository,
            unitOfWork,
            cancellationToken).ConfigureAwait(false);
    }
}
