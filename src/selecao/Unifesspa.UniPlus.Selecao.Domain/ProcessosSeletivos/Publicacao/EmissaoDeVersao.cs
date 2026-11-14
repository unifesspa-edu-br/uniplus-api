namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Events;

/// <summary>
/// Cria a versão congelada e o evento que a anuncia. O instante do pedido, lido uma única vez do relógio pelo
/// comando, serve ao id do ato e à vigência da versão: o evento publica o instante do sistema, de modo que
/// resolver a configuração nele (ADR-0075) cai dentro da vigência da versão que o próprio ato criou.
/// </summary>
internal static class EmissaoDeVersao
{
    /// <summary>Abre a versão 1 do processo.</summary>
    internal static (VersaoConfiguracao Versao, ProcessoPublicadoEvent Evento) Abrir(Guid processoId, PedidoDeCongelamento pedido)
    {
        DateTimeOffset instante = pedido.Instante;
        DocumentoCongelado documento = pedido.Documento;

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId,
            documento.Canonico,
            documento.SchemaVersion,
            documento.AlgoritmoHash,
            atoCriadorId: NovoIdDeAto(instante),
            atoCriadorHash: documento.HashDocumento,
            pedido.AtorUsuarioSub,
            instante);

        return (versao, EventoDe(processoId, versao));
    }

    /// <summary>
    /// Sucede a versão corrente. O instante é ancorado na vigência dela quando o relógio regride:
    /// o id v7 do ato novo carrega o instante nos bits mais significativos, e um instante cru
    /// ordenaria o ato novo antes do que ele emenda, invertendo a cadeia na paginação por keyset (ADR-0032).
    /// </summary>
    internal static (VersaoConfiguracao Versao, ProcessoPublicadoEvent Evento) Suceder(
        Guid processoId, VersaoConfiguracao versaoAtual, PedidoDeCongelamento pedido)
    {
        DateTimeOffset instante = pedido.Instante < versaoAtual.VigenteAPartirDe ? versaoAtual.VigenteAPartirDe : pedido.Instante;
        DocumentoCongelado documento = pedido.Documento;

        VersaoConfiguracao versao = VersaoConfiguracao.Suceder(
            versaoAtual,
            documento.Canonico,
            documento.SchemaVersion,
            documento.AlgoritmoHash,
            atoCriadorId: NovoIdDeAto(instante),
            atoCriadorHash: documento.HashDocumento,
            atoCriadorRetificaId: versaoAtual.AtoCriadorId,
            pedido.AtorUsuarioSub,
            instante);

        return (versao, EventoDe(processoId, versao));
    }

    // O id nasce no agregado, e não em Publicacoes, porque a versão precisa referenciá-lo antes de o
    // ato existir (ADR-0108); a reentrega da fila tenta gravar o mesmo id, e a chave primária a recusa.
    private static Guid NovoIdDeAto(DateTimeOffset instante) => Guid.CreateVersion7(instante);

    // O nome do membro EditalId é contrato do envelope durável e do schema Avro; o valor é o do ato criador.
    private static ProcessoPublicadoEvent EventoDe(Guid processoId, VersaoConfiguracao versao) =>
        new(
            processoId,
            versao.AtoCriadorId,
            versao.Id,
            versao.HashConfiguracao,
            versao.AtoCriadorHash,
            versao.VigenteAPartirDe);
}
