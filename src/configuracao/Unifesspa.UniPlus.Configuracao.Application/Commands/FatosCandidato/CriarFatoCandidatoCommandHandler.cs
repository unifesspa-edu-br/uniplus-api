namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida o fato por inteiro primeiro (sem I/O), com as violações acumuladas (ADR-0125), e só então
/// confere a unicidade do código entre todos os fatos, desativados inclusive.
/// </summary>
public static class CriarFatoCandidatoCommandHandler
{
    private const string IndiceUnicoDoCodigo = "ux_rol_de_fatos_candidato_codigo";

    public static async Task<Result<Guid>> Handle(
        CriarFatoCandidatoCommand command,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Token desconhecido vira o sentinela do enum, que o agregado recusa com o erro do campo.
        _ = DominiosFato.TryAnalisar(command.Dominio, out DominioFato dominio);
        _ = CardinalidadesFato.TryAnalisar(command.Cardinalidade, out CardinalidadeFato cardinalidade);
        _ = EscoposFato.TryAnalisar(command.Escopo, out EscopoFato escopo);
        _ = ClassificacoesProtecaoDado.TryAnalisar(command.ClassificacaoProtecao, out ClassificacaoProtecaoDado classificacao);
        _ = HipotesesLegaisTratamento.TryAnalisar(command.HipoteseLegal, out HipoteseLegalTratamento hipotese);
        FonteValoresFato? fonte = command.FonteValores is null ? null
            : FontesValoresFato.TryAnalisar(command.FonteValores, out FonteValoresFato f) ? f : FonteValoresFato.Nenhuma;
        FormatoTexto? formato = command.Formato is null ? null
            : FormatosTexto.TryAnalisar(command.Formato, out FormatoTexto fmt) ? fmt : FormatoTexto.Nenhum;

        Result<FatoCandidato> criar = FatoCandidato.CriarDoAdministrador(
            command.Codigo, command.Nome, command.Descricao, dominio, cardinalidade, fonte, formato,
            command.PontoResolucao, escopo, classificacao, command.FinalidadeTratamento, hipotese);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        FatoCandidato fato = criar.Value!;
        if (await repository.CodigoExisteAsync(fato.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        await repository.AdicionarAsync(fato, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflito(exception, IndiceUnicoDoCodigo))
        {
            // Sem descartar, a inserção continua rastreada e o SaveChangesAsync do outbox a repete
            // fora deste catch, e o 409 vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        return Result<Guid>.Success(fato.Id);
    }

    private static DomainError CodigoJaExiste() => new(
        FatoCandidatoErrorCodes.CodigoJaExiste,
        "Já existe um fato com o código informado; o código de um fato nunca é reutilizado.");
}
