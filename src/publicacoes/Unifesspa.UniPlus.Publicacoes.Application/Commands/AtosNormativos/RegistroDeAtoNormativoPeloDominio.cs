namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// A porta de escrita do registro de atos: monta o ato com as mesmas conferências do endpoint administrativo
/// (<see cref="MontagemDoAtoNormativo"/>) e o deixa no rastreador, sem confirmar. É o registro que só o domínio dono do
/// objeto faz, porque é ele quem guardou o arquivo do documento.
/// </summary>
/// <remarks>
/// Os atributos do tipo chegam conferidos por quem publica e não são relidos do catálogo, que é editável (ADR-0061).
/// Quem chama faz a gravação única e, na recusa, descarta as alterações (ADR-0170).
/// </remarks>
internal sealed class RegistroDeAtoNormativoPeloDominio(MontagemDoAtoNormativo montagem, IAtoNormativoRepository atos)
    : IRegistroDeAtoNormativo
{
    public async Task<Result> RegistrarAsync(AtoNormativoPublicado ato, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ato);

        Result<AtoMontado> montado = await montagem
            .MontarAsync(ComandoDe(ato), ato.PublicadoEm, cancellationToken)
            .ConfigureAwait(false);
        if (montado.IsFailure)
        {
            return Result.ValidationFailure(SemCampo(montado.Errors));
        }

        await atos.AdicionarAsync(montado.Value!.Ato, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// Os campos do corpo do endpoint não existem no pedido de quem publica por esta porta: as causas
    /// seguem com o mesmo código e a mesma ordem, sem campo.
    /// </summary>
    private static FieldError[] SemCampo(IReadOnlyList<FieldError> recusas) =>
        [.. recusas.Select(recusa => new FieldError(null, recusa.Error))];

    public DomainError? TraduzirConflitoDaGravacao(Exception excecao) => ConflitoNaGravacaoDoAto.Traduzir(excecao);

    private static RegistrarAtoNormativoCommand ComandoDe(AtoNormativoPublicado ato) =>
        IdentificacaoDe(ato) with
        {
            VersaoDoObjetoId = ato.VersaoDoObjeto?.Id,
            VersaoDoObjetoHash = ato.VersaoDoObjeto?.Hash,
            AtoRetificadoId = ato.Retificacao?.AtoRetificadoId,
            MotivoRetificacao = ato.Retificacao?.Motivo,
            Vinculos = [.. ato.Vinculos.Select(static v => new VinculoEntidadeInput(v.EntidadeTipo, v.EntidadeId))],
            Anexos = [.. ato.Anexos.Select(static a => new AnexoDoAtoInput(a.Nome, a.TipoMidia, a.Hash))],
            AtoId = ato.AtoId,
            AtributosDoTipo = ato.AtributosDoTipo,
            ChegouPeloDominioDono = true,
            PublicadoEm = ato.PublicadoEm,
        };

    private static RegistrarAtoNormativoCommand IdentificacaoDe(AtoNormativoPublicado ato) =>
        new(
            Orgao: ato.Orgao,
            Serie: ato.Serie,
            Ano: ato.Ano,
            Numero: ato.Numero,
            TipoCodigo: ato.TipoCodigo,
            DataDoAto: ato.DataDoAto,
            DocumentoHash: ato.DocumentoHash,
            Assinante: ato.Assinante);
}
