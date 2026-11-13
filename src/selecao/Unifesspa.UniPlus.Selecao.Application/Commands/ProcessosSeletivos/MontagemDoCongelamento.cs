namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Lê o contexto de contagem de prazos, canonicaliza a configuração conferida e monta o pedido que o agregado
/// congela. O dataset de calendário incoerente não aborta aqui, porque só importa para o processo cuja contagem
/// distingue dia útil, e quem decide isso é o portão da raiz.
/// </summary>
public sealed class MontagemDoCongelamento(
    LeituraDoContextoDeContagemDePrazos contextos,
    ISnapshotPublicacaoCanonicalizer canonicalizer,
    TimeProvider timeProvider,
    IUserContext userContext)
{
    internal async Task<PedidoDeCongelamento> MontarAsync(ConfiguracaoConferida conferida, CancellationToken cancellationToken)
    {
        ContextoDeContagemDePrazos contexto = await contextos.LerAsync(cancellationToken).ConfigureAwait(false);
        EntradaCanonicalizacao entrada = conferida.Entrada;
        SnapshotCanonico canonico = canonicalizer.Canonicalizar(entrada with { CalendarioDiasUteis = contexto.CalendarioVigente });

        return new PedidoDeCongelamento
        {
            Dados = entrada.Dados,
            Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, entrada.HashDocumento),
            AtorUsuarioSub = userContext.UserId ?? "system",
            Clock = timeProvider,
            Contexto = contexto,
            FatosDeModalidade = conferida.FatosDeModalidade,
            TiposDeAtoUnicos = conferida.TiposDeAtoUnicos,
        };
    }
}
