namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Abstractions;

using Domain.Entities;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// O ato como a Seleção o entrega à porta de escrita do registro central: o que o catálogo dizia do tipo quando o
/// ato foi conferido, copiado por valor (ADR-0061), e o vínculo com o processo que o publica.
/// </summary>
internal static class AtoPublicadoPeloProcesso
{
    /// <summary>
    /// O ato que cria uma versão de configuração: a abertura e a retificação do edital. O instante da divulgação é o
    /// da vigência da versão, e a versão invocada é a que nasce com ele.
    /// </summary>
    public static AtoNormativoPublicado DaVersao(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        DadosDoAto ato,
        TipoAtoPublicadoView tipoConferido,
        string? numeroDeclarado,
        string documentoHash,
        RetificacaoInfo? retificacao)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(versao);

        return Montar(ato, tipoConferido, processo.Id, versao.AtoCriadorId, versao.VigenteAPartirDe, numeroDeclarado, documentoHash) with
        {
            VersaoInvocada = new VersaoInvocadaDoAto(versao.Id, versao.HashConfiguracao),
            Correcao = retificacao is null ? null : new CorrecaoDoAto(retificacao.EditalRetificadoId, retificacao.Motivo),
        };
    }

    /// <summary>
    /// O ato que não cria versão: o documento avulso e a publicação de um item do edital. Nenhuma versão é invocada, e
    /// a série de numeração do órgão é o próprio tipo do ato.
    /// </summary>
    public static AtoNormativoPublicado DoAtoAvulso(AtoAEnviar ato, AtoDoProcesso registro)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(registro);

        DadosDoAto dados = new(ato.Processo.Orgao, ato.Tipo.Codigo, ato.DataDoAto.Year, ato.DataDoAto, registro.Assinante, ato.Tipo.Codigo);
        string hashDoDocumento = ato.Documentos.Principal.HashSha256!;

        return Montar(dados, ato.Tipo, ato.ProcessoSeletivoId, registro.Id, registro.PublicadoEm, registro.Numero, hashDoDocumento) with
        {
            Correcao = registro.AtoCorrigidoId is { } corrigido ? new CorrecaoDoAto(corrigido, registro.MotivoDaCorrecao!) : null,
            Anexos = [.. ato.Documentos.Anexos.Select(static a => new AnexoDoAtoPublicado(a.Nome, a.Documento.TipoDeMidia, a.Documento.HashSha256!))],
        };
    }

    private static AtoNormativoPublicado Montar(
        DadosDoAto ato,
        TipoAtoPublicadoView tipoConferido,
        Guid processoSeletivoId,
        Guid atoId,
        DateTimeOffset publicadoEm,
        string? numero,
        string documentoHash) =>
        new()
        {
            AtoId = atoId,
            Orgao = ato.Orgao,
            Serie = ato.Serie,
            Ano = ato.Ano,
            Numero = numero,
            TipoCodigo = ato.TipoAtoCodigo,
            TipoConferido = new AtributosDoTipoAto(tipoConferido.CongelaConfiguracao, tipoConferido.UnicoPorObjeto, tipoConferido.EfeitoIrreversivel, tipoConferido.Nome),
            DataDoAto = ato.DataDoAto,
            PublicadoEm = publicadoEm,
            DocumentoHash = documentoHash,
            Assinante = ato.Assinante,
            Vinculos = [new VinculoDoAtoPublicado(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, processoSeletivoId)],
        };
}
