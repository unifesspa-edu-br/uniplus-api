namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public static class ExportarVersaoPublicadaQueryHandler
{
    public static async Task<Result<VersaoPublicadaExportadaDto>> Handle(ExportarVersaoPublicadaQuery query, IProcessoSeletivoRepository repositorio, IProjecaoDaVersaoPublicada projecao, TimeProvider relogio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(projecao);
        ArgumentNullException.ThrowIfNull(relogio);
        Result<VersaoConfiguracao> resultado = query.Versao is int numero
            ? await ObterPorNumeroAsync(query.ProcessoSeletivoId, numero, repositorio, cancellationToken).ConfigureAwait(false)
            : await SelecaoDaVersaoVigente.ObterAsync(query.ProcessoSeletivoId, relogio.GetUtcNow(), repositorio, cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return Result<VersaoPublicadaExportadaDto>.Failure(resultado.Error!);
        }
        return Result<VersaoPublicadaExportadaDto>.Success(Projetar(resultado.Value!, projecao));
    }

    private static VersaoPublicadaExportadaDto Projetar(VersaoConfiguracao versao, IProjecaoDaVersaoPublicada projecao)
    {
        VersaoPublicada entrada = new(versao.ProcessoSeletivoId, versao.NumeroVersao, versao.VigenteAPartirDe, versao.ConfiguracaoCongelada);
        return new(projecao.Projetar(entrada), $"\"{versao.HashConfiguracao}\"");
    }

    private static async Task<Result<VersaoConfiguracao>> ObterPorNumeroAsync(Guid processoId, int numero, IProcessoSeletivoRepository repositorio, CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versao = await repositorio.ObterVersaoPorNumeroAsync(processoId, numero, cancellationToken).ConfigureAwait(false);
        if (versao is not null)
        {
            return Result<VersaoConfiguracao>.Success(versao);
        }
        bool existe = await repositorio.ExisteAsync(processoId, cancellationToken).ConfigureAwait(false);
        return Result<VersaoConfiguracao>.Failure(existe
            ? new DomainError("ProcessoSeletivo.VersaoNaoEncontrada", $"A versão {numero} não foi encontrada para o processo seletivo.")
            : new DomainError("ProcessoSeletivo.NaoEncontrado", "Processo seletivo não encontrado."));
    }
}
