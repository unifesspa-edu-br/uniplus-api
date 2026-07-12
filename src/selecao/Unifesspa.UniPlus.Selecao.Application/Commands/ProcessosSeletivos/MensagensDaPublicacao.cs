namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Monta as mensagens que saem de uma publicação ou retificação: os domain events do
/// agregado e a requisição de registro do ato em <c>Publicacoes</c> (ADR-0108).
/// </summary>
/// <remarks>
/// Publicar e retificar produzem a MESMA orquestração — o que muda é só o par
/// (ato retificado, motivo), que a retificação preenche e a abertura deixa vazio. A
/// retificação, aliás, não é um tipo de ato: é uma relação entre atos (ADR-0103), e por
/// isso o tipo continua vindo declarado pelo operador nos dois casos.
/// </remarks>
internal static class MensagensDaPublicacao
{
    /// <summary>Rótulo do objeto no vínculo genérico de Publicações (ADR-0105).</summary>
    public const string EntidadeProcessoSeletivo = "PROCESSO_SELETIVO";

    public static object[] Montar(
        ProcessoSeletivo processo,
        PublicacaoResultado resultado,
        DadosDoAto ato,
        TipoAtoPublicadoView tipoConferido,
        string? numeroDeclarado,
        string documentoHash,
        Guid? atoRetificadoId,
        string? motivoRetificacao)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(tipoConferido);

        return
        [
            .. processo.DequeueDomainEvents().Cast<object>(),
            new RegistrarAtoNormativoRequisicao(
                // O id do ato é o do Edital que o originou: decidido por Seleção, dentro da
                // transação, e já gravado em VersaoConfiguracao.AtoCriadorId. É o que torna a
                // reentrega da fila (at-least-once) idempotente — o segundo processamento
                // reencontra o mesmo id e não faz nada.
                AtoId: resultado.Versao.AtoCriadorId,
                Orgao: ato.Orgao,
                Serie: ato.Serie,
                Ano: ato.Ano,
                Numero: numeroDeclarado,
                TipoCodigo: ato.TipoAtoCodigo,
                DataPublicacao: ato.DataPublicacao,
                DocumentoHash: documentoHash,
                Assinante: ato.Assinante,
                VersaoInvocadaId: resultado.Versao.Id,
                VersaoInvocadaHash: resultado.Versao.HashConfiguracao,
                AtoRetificadoId: atoRetificadoId,
                MotivoRetificacao: motivoRetificacao,
                Vinculos: [new VinculoEntidadeRequisicao(EntidadeProcessoSeletivo, processo.Id)],
                // Os atributos como o catálogo os declarava AGORA — copiados por valor
                // (ADR-0061). O registro do ato acontece depois, e o cadastro é editável:
                // relê-lo no consumo faria a decisão que devolveu 204 valer outra coisa.
                AtributosDoTipo: new AtributosDoTipoAto(
                    tipoConferido.CongelaConfiguracao,
                    tipoConferido.UnicoPorObjeto,
                    tipoConferido.EfeitoIrreversivel)),
        ];
    }
}
