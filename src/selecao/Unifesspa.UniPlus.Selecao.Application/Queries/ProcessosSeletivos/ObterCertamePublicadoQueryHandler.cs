namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="ObterCertamePublicadoQuery"/> e do
/// <see cref="ObterCertamePublicadoPorIdentificadorQuery"/>: lê a divulgação pública do certame,
/// pelo Guid do processo ou pelo identificador legível congelado.
/// </summary>
/// <remarks>
/// <para>
/// Uma consulta, por chave primária ou pelo índice único do identificador. A projeção acontece uma vez, quando o ato normativo se
/// confirma; a leitura só entrega o que já está pronto. Não há linhagem a resolver, nem pergunta a
/// outro módulo no caminho da requisição, nem documento congelado a interpretar por leitura.
/// </para>
/// <para>
/// <b>A ausência da linha é a única recusa de visibilidade.</b> Processo inexistente, processo em
/// rascunho, processo sem versão vigente e processo cujo ato não se confirmou devolvem a mesma
/// resposta — não por uma regra que os colapse, mas porque nenhum deles tem linha. Distinguir
/// deixou de ser possível, em vez de ser possível e proibido. A outra recusa possível não é de
/// visibilidade: é a projeção guardada que não se deixa ler, e essa aflora como falha de leitura.
/// </para>
/// </remarks>
public static class ObterCertamePublicadoQueryHandler
{
    public static async Task<Result<CertamePublicadoDto>> Handle(
        ObterCertamePublicadoQuery query,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return Servir(divulgado);
    }

    public static async Task<Result<CertamePublicadoDto>> Handle(
        ObterCertamePublicadoPorIdentificadorQuery query,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraPorIdentificadorAsync(query.IdentificadorLegivel, cancellationToken)
            .ConfigureAwait(false);

        return Servir(divulgado);
    }

    /// <summary>
    /// A leitura comum às duas chaves: a ausência da linha é a única recusa de visibilidade, e uma
    /// projeção que não se deixa ler aflora como falha de leitura.
    /// </summary>
    private static Result<CertamePublicadoDto> Servir(CertameDivulgado? divulgado)
    {
        if (divulgado is null)
        {
            return NaoEncontrado();
        }

        return ProjecaoDoCertamePublicado.TentarLerProjecao(divulgado.Certame, out CertamePublicadoDto? certame)
            ? Result<CertamePublicadoDto>.Success(certame)
            : ProjecaoDoCertamePublicado.RecusarDocumentoIlegivel();
    }

    private static Result<CertamePublicadoDto> NaoEncontrado() =>
        Result<CertamePublicadoDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            "Processo Seletivo não encontrado."));
}
