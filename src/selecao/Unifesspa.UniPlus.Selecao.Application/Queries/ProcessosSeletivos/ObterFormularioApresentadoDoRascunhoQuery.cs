namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

/// <summary>
/// O formulário do código informado como a configuração viva o define agora — o rascunho —, no mesmo formato que o certame divulgado serve (ADR-0139). É o que a
/// simulação do administrador interpreta antes de publicar; nada é gravado.
/// </summary>
public sealed record ObterFormularioApresentadoDoRascunhoQuery(
    Guid ProcessoSeletivoId, string FormularioCodigo) : IQuery<Result<FormularioApresentadoDoProcessoDto>>;

/// <summary>
/// Projeta a configuração viva pela mesma projeção do certame divulgado, com o catálogo vivo: a
/// definição avaliável da pré-visualização, os valores que o catálogo oferece hoje e a data de
/// referência dos fatos que o rascunho já resolve. A comprovação documental sai da publicação.
/// </summary>
public static class ObterFormularioApresentadoDoRascunhoQueryHandler
{
    public static async Task<Result<FormularioApresentadoDoProcessoDto>> Handle(
        ObterFormularioApresentadoDoRascunhoQuery query,
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
        // Provisório até a retirada da finalidade: o código desconhecido é o mesmo não encontrado do formulário ausente.
        return processo is null || EstruturaFormulario.FinalidadeDoCodigo(query.FormularioCodigo) is not { } finalidade
            ? NaoEncontrado(query.ProcessoSeletivoId)
            : await ProjetarAsync(processo, finalidade, fatoCandidatoReader, resolvedorFuso, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<FormularioApresentadoDoProcessoDto>> ProjetarAsync(
        ProcessoSeletivo processo,
        FinalidadeFormulario finalidade,
        IFatoCandidatoReader fatoCandidatoReader,
        IResolvedorFusoInstitucional resolvedorFuso,
        CancellationToken cancellationToken)
    {
        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<FormularioApresentadoDoProcessoDto>.Failure(fuso.Error!);
        }

        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, catalogo);
        return avaliavel.IsFailure
            ? Result<FormularioApresentadoDoProcessoDto>.Failure(avaliavel.Error!)
            : Projetar(processo, finalidade, avaliavel.Value!, fuso.Value!);
    }

    private static Result<FormularioApresentadoDoProcessoDto> Projetar(
        ProcessoSeletivo processo, FinalidadeFormulario finalidade, DefinicaoAvaliavel avaliavel, TimeZoneInfo fuso) =>
        ProjecaoDoFormularioApresentado.Projetar(
            finalidade, avaliavel, processo.Formularios, processo.FatosColetados, processo.GruposColetados, processo.TermosExigidos,
            ReferenciaTemporalDosFatos.DataReferenciaFatosEmMontagem(processo.Declaracao(), fuso)) is { } formulario
            ? Result<FormularioApresentadoDoProcessoDto>.Success(new FormularioApresentadoDoProcessoDto(
                formulario, processo.Formularios.FirstOrDefault(f => f.Finalidade == finalidade)?.FaseId, null, null, null))
            : NaoEncontrado(processo.Id);

    private static Result<FormularioApresentadoDoProcessoDto> NaoEncontrado(Guid processoSeletivoId) =>
        Result<FormularioApresentadoDoProcessoDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            $"Processo Seletivo {processoSeletivoId} não encontrado, ou sem formulário da finalidade."));
}
