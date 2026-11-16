namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário renderizável da versão em que está a inscrição — a divulgada, no rascunho; a congelada no primeiro envio, depois
/// dele. É a mesma projeção do formulário do processo (<see cref="ProjecaoDoFormularioDaVersao"/>).
/// </summary>
public sealed class FormularioDaInscricao(VersaoDaInscricao versao, IRegistroCodecsEnvelope codecs)
{
    public async Task<Result<FormularioRenderizavelDto>> ProjetarAsync(Inscricao inscricao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        Result<VersaoConfiguracao> resolvida = await versao.ResolverAsync(inscricao, cancellationToken).ConfigureAwait(false);
        return resolvida.IsFailure
            ? Result<FormularioRenderizavelDto>.Failure(resolvida.Error!)
            : DaVersao(inscricao, resolvida.Value!);
    }

    private Result<FormularioRenderizavelDto> DaVersao(Inscricao inscricao, VersaoConfiguracao resolvida)
    {
        Result<FormularioRenderizavelDto?> projetado = ProjecaoDoFormularioDaVersao.Projetar(
            resolvida, FinalidadeFormulario.Inscricao, codecs, acervoDaComprovacao: null);
        if (projetado.IsFailure)
        {
            return Result<FormularioRenderizavelDto>.Failure(projetado.Error!);
        }

        // Uma versão que recebeu inscrição foi publicada com o formulário de inscrição: a ausência
        // dele numa versão fixada é estado que a publicação não produz.
        return Result<FormularioRenderizavelDto>.Success(projetado.Value
            ?? throw new InvalidOperationException(
                $"A versão fixada da inscrição do processo {inscricao.ProcessoSeletivoId} não tem o formulário de inscrição."));
    }
}
