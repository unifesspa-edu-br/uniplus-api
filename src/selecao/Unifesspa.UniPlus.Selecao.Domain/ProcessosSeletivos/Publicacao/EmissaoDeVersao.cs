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
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId, pedido.Documento, NovoIdDeAto(pedido.Instante), pedido.AtorUsuarioContaId, pedido.Instante);

        return (versao, EventoDe(processoId, versao));
    }

    /// <summary>
    /// Sucede a versão corrente. O comando já entrega o instante efetivo. Quando o relógio regride, a versão nova
    /// ainda assume a vigência da versão que sucede: o identificador do ato novo carrega o instante, e um instante anterior
    /// ordenaria o ato novo antes do ato que ele emenda, invertendo a ordem da cadeia na listagem paginada (ADR-0032).
    /// </summary>
    internal static (VersaoConfiguracao Versao, ProcessoPublicadoEvent Evento) Suceder(
        Guid processoId, VersaoConfiguracao versaoAtual, PedidoDeCongelamento pedido)
    {
        DateTimeOffset instante = versaoAtual.VigenciaDaSucessora(pedido.Instante);

        VersaoConfiguracao versao = VersaoConfiguracao.Suceder(
            versaoAtual, pedido.Documento, NovoIdDeAto(instante), pedido.AtorUsuarioContaId, instante);

        return (versao, EventoDe(processoId, versao));
    }

    // O id nasce no agregado, e não em Publicacoes, porque a versão precisa referenciá-lo antes de o
    // ato existir (ADR-0061): ele é gravado na mesma transação, e a versão o referencia por valor.
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
