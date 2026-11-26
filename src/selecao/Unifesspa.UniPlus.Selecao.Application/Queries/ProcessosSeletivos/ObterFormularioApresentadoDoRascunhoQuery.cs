namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Cronograma;
using Domain.ProcessosSeletivos.Formularios;
using Domain.ValueObjects;

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
        // Código fora do formato não designa formulário nenhum: é o mesmo não encontrado do formulário ausente.
        return processo is null || CodigoDoFormulario.Criar(query.FormularioCodigo) is not { IsSuccess: true } codigo
            ? NaoEncontrado()
            : await ProjetarAsync(processo, codigo.Value!, fatoCandidatoReader, resolvedorFuso, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<FormularioApresentadoDoProcessoDto>> ProjetarAsync(
        ProcessoSeletivo processo,
        CodigoDoFormulario codigo,
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
            : Projetar(processo, codigo, avaliavel.Value!, fuso.Value!);
    }

    private static Result<FormularioApresentadoDoProcessoDto> Projetar(
        ProcessoSeletivo processo, CodigoDoFormulario codigo, DefinicaoAvaliavel avaliavel, TimeZoneInfo fuso)
    {
        ConfiguracaoDeclarada declaracao = processo.Declaracao();
        return ProjecaoDoFormularioApresentado.Projetar(
            codigo, avaliavel, declaracao.Coleta, CronogramaDoProcesso.Do(declaracao.Cronograma.CronogramaFases),
            ReferenciaTemporalDosFatos.DataReferenciaFatosEmMontagem(declaracao, fuso)) is { } formulario
            ? Result<FormularioApresentadoDoProcessoDto>.Success(DoRascunho(formulario, declaracao.Coleta.FormularioDe(codigo)!))
            : NaoEncontrado();
    }

    private static FormularioApresentadoDoProcessoDto DoRascunho(FormularioApresentado apresentado, FormularioProcesso formulario) =>
        new(apresentado, formulario.Codigo.Valor, formulario.FaseId, formulario.Obrigatorio, null, null, null);

    private static Result<FormularioApresentadoDoProcessoDto> NaoEncontrado() =>
        Result<FormularioApresentadoDoProcessoDto>.Failure(AcessoAoProcesso.NaoEncontrado());
}
