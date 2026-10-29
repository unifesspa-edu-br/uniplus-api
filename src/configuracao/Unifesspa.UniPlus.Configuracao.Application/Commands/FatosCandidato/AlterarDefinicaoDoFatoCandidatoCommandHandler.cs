namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida a definição nova no agregado e a confere contra o que cita o fato (ADR-0136); só então
/// grava. A recusa descarta o fato rastreado, que a validação do agregado e a das citações usam
/// com a definição já aplicada.
/// </summary>
public static class AlterarDefinicaoDoFatoCandidatoCommandHandler
{
    public static async Task<Result> Handle(
        AlterarDefinicaoDoFatoCandidatoCommand command,
        IFatoCandidatoRepository repository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IModeloFormularioRepository modeloRepository,
        IModalidadeRepository modalidadeRepository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(modeloRepository);
        ArgumentNullException.ThrowIfNull(modalidadeRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(command.Definicao);

        DefinicaoDoFato definicao = Ler(command.Definicao);

        // A definição de um fato é conferida contra as regras padrão do catálogo inteiro: as escritas
        // dessas regras e esta são serializadas.
        await repository.TravarRegrasPadraoParaEscritaAsync(cancellationToken).ConfigureAwait(false);

        // O fato inexistente (404) é recusado antes de qualquer carga; os modelos e as modalidades,
        // que são a carga pesada, só são lidos depois de o próprio fato aceitar a definição.
        return await MutacaoDoFato.AplicarAsync(
            command.Id,
            async fato =>
            {
                IReadOnlyList<FatoCandidato> fatos = await repository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
                IReadOnlyList<PrecedenciaFase> precedencias = await precedenciaRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);
                CatalogoDeFatos antes = new(fatos, precedencias);

                Result alterar = fato.AlterarDefinicao(definicao, antes);
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

                // O agregado já recebeu a definição nova; sem descartar, o save do Wolverine a gravaria.
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result.ValidationFailure(citacoes);
            },
            repository,
            unitOfWork,
            cancellationToken).ConfigureAwait(false);
    }

    private static DefinicaoDoFato Ler(DefinicaoDoFatoInput entrada) =>
        new(
            TokensDoFato.Dominio(entrada.Dominio),
            TokensDoFato.Cardinalidade(entrada.Cardinalidade),
            TokensDoFato.Fonte(entrada.FonteValores),
            TokensDoFato.Formato(entrada.Formato),
            entrada.PontoResolucao,
            TokensDoFato.Escopo(entrada.Escopo),
            TokensDoFato.Classificacao(entrada.ClassificacaoProtecao) ?? ClassificacaoDeProtecao.Nenhuma,
            entrada.FinalidadeTratamento,
            TokensDoFato.HipoteseLegal(entrada.HipoteseLegal) ?? HipoteseLegalTratamento.Nenhuma);
}
