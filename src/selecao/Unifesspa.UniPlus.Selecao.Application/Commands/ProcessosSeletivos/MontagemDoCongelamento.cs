namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.ProcessosSeletivos.Bancas;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Lê o contexto de contagem de prazos e o número de corretores de cada etapa, canonicaliza a configuração conferida e
/// monta o pedido que o agregado congela. O dataset de calendário incoerente não aborta aqui, porque só importa para o
/// processo cuja contagem distingue dia útil, e quem decide isso é o portão da raiz. Os corretores são lidos depois da
/// conferência da configuração: a recusa de negócio vem antes do defeito de dado do catálogo.
/// </summary>
public sealed class MontagemDoCongelamento(
    LeituraDoContextoDeContagemDePrazos contextos,
    LeituraDosCorretoresDasEtapas corretores,
    ISnapshotPublicacaoCanonicalizer canonicalizer,
    IUserContext userContext)
{
    internal async Task<PedidoDeCongelamento> MontarAsync(
        ConfiguracaoConferida conferida, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        ContextoDeContagemDePrazos contexto = await contextos.LerAsync(cancellationToken).ConfigureAwait(false);
        CorretoresDasEtapas corretoresDasEtapas = await corretores
            .LerAsync(conferida.Entrada.Processo, cancellationToken)
            .ConfigureAwait(false);

        return Pedido(conferida, instante, contexto, corretoresDasEtapas);
    }

    private PedidoDeCongelamento Pedido(
        ConfiguracaoConferida conferida, DateTimeOffset instante, ContextoDeContagemDePrazos contexto, CorretoresDasEtapas corretoresDasEtapas)
    {
        EntradaCanonicalizacao entrada = conferida.Entrada;
        SnapshotCanonico canonico = canonicalizer.Canonicalizar(entrada with { CalendarioDiasUteis = contexto.CalendarioVigente });

        return new PedidoDeCongelamento
        {
            Dados = entrada.Dados,
            Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, entrada.HashDocumento),
            AtorUsuarioContaId = userContext.UserId ?? "system",
            Instante = instante,
            Contexto = contexto,
            FatosDeModalidade = conferida.FatosDeModalidade,
            TiposDeAtoUnicos = conferida.TiposDeAtoUnicos,
            Corretores = corretoresDasEtapas,
        };
    }
}
