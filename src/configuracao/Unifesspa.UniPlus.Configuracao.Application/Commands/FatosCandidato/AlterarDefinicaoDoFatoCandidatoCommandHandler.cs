namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida a definição nova no agregado e a confere contra o que cita o fato (ADR-0136); só então
/// grava (<see cref="AlteracaoConferidaDoFato"/>).
/// </summary>
public static class AlterarDefinicaoDoFatoCandidatoCommandHandler
{
    public static Task<Result> Handle(
        AlterarDefinicaoDoFatoCandidatoCommand command,
        IFatoCandidatoRepository repository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IModeloFormularioRepository modeloRepository,
        IModalidadeRepository modalidadeRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(modeloRepository);
        ArgumentNullException.ThrowIfNull(modalidadeRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        DefinicaoDoFato definicao = Ler(command.Definicao);

        return AlteracaoConferidaDoFato.AplicarAsync(
            command.Id,
            (fato, catalogo) => fato.AlterarDefinicao(definicao, catalogo),
            repository,
            precedenciaRepository,
            modeloRepository,
            modalidadeRepository,
            unitOfWork,
            cancellationToken);
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
