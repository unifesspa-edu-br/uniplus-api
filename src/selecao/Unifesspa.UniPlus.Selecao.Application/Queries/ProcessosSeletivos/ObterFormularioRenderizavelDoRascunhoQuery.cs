namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário de uma finalidade como a configuração viva o define agora — o rascunho, ou a sessão
/// de retificação aberta —, no mesmo formato que o certame divulgado serve (ADR-0139). É o que a
/// simulação do administrador interpreta antes de publicar; nada é gravado.
/// </summary>
public sealed record ObterFormularioRenderizavelDoRascunhoQuery(
    Guid ProcessoSeletivoId, FinalidadeFormulario Finalidade) : IQuery<Result<FormularioRenderizavelDto>>;

/// <summary>
/// Projeta a configuração viva pela mesma projeção do certame divulgado, com o catálogo vivo: a
/// definição avaliável da pré-visualização, os valores que o catálogo oferece hoje e a data de
/// referência dos fatos que o rascunho já resolve. A comprovação documental sai da publicação.
/// </summary>
public static class ObterFormularioRenderizavelDoRascunhoQueryHandler
{
    public static async Task<Result<FormularioRenderizavelDto>> Handle(
        ObterFormularioRenderizavelDoRascunhoQuery query,
        IProcessoSeletivoRepository repository,
        IFatoCandidatoReader fatoCandidatoReader,
        IResolvedorFusoInstitucional resolvedorFuso,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);

        ProcessoSeletivo? processo = await repository.ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return NaoEncontrado(query.ProcessoSeletivoId);
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<FormularioRenderizavelDto>.Failure(fuso.Error!);
        }

        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, catalogo);
        if (avaliavel.IsFailure)
        {
            return Result<FormularioRenderizavelDto>.Failure(avaliavel.Error!);
        }

        return ProjecaoDoFormularioRenderizavel.Projetar(
                query.Finalidade, avaliavel.Value!, processo.Formularios, processo.FatosColetados, processo.GruposColetados, processo.TermosExigidos,
                processo.DataReferenciaFatosEmMontagem(fuso.Value!)) is { } formulario
            ? Result<FormularioRenderizavelDto>.Success(new FormularioRenderizavelDto(formulario, comprovacaoDocumental: null))
            : NaoEncontrado(query.ProcessoSeletivoId);
    }

    private static Result<FormularioRenderizavelDto> NaoEncontrado(Guid processoSeletivoId) =>
        Result<FormularioRenderizavelDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            $"Processo Seletivo {processoSeletivoId} não encontrado, ou sem formulário da finalidade."));
}
