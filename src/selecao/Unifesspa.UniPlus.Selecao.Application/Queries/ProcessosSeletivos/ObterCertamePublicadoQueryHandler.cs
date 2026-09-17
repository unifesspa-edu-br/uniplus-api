namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="ObterCertamePublicadoQuery"/>: lê a divulgação pública do certame.
/// </summary>
/// <remarks>
/// <para>
/// Uma consulta, por chave primária. A projeção acontece uma vez, quando o ato normativo se
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

        if (divulgado is null)
        {
            return Result<CertamePublicadoDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                "Processo Seletivo não encontrado."));
        }

        return ProjecaoDoCertamePublicado.TentarLerProjecao(divulgado.Certame, out CertamePublicadoDto? certame)
            ? Result<CertamePublicadoDto>.Success(certame)
            : ProjecaoDoCertamePublicado.RecusarDocumentoIlegivel();
    }
}
