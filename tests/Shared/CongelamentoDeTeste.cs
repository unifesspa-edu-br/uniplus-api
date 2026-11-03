namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As transições que congelam uma versão com os argumentos soltos, como os testes as escrevem: cada
/// chamada monta o pedido e delega ao agregado, sem validar nada além do que o agregado valida.
/// </summary>
internal static class CongelamentoDeTeste
{
    public static Result<VersaoConfiguracao> Publicar(
        this ProcessoSeletivo processo,
        DadosEdital dados,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto,
        FatosDeModalidade fatosDeModalidade,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos) =>
        processo.Publicar(Pedido(
            dados, configuracaoCongeladaCanonica, schemaVersion, algoritmoHash, hashDocumento, atorUsuarioSub,
            clock, contexto, fatosDeModalidade, tiposDeAtoUnicos));

    public static Result<VersaoConfiguracao> Retificar(
        this ProcessoSeletivo processo,
        DadosEdital dados,
        VersaoConfiguracao versaoAtual,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        string motivo,
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto,
        FatosDeModalidade fatosDeModalidade,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos) =>
        processo.Retificar(
            Pedido(
                dados, configuracaoCongeladaCanonica, schemaVersion, algoritmoHash, hashDocumento, atorUsuarioSub,
                clock, contexto, fatosDeModalidade, tiposDeAtoUnicos),
            versaoAtual,
            motivo);

    public static Result<VersaoConfiguracao> FecharRetificacao(
        this ProcessoSeletivo processo,
        DadosEdital dados,
        VersaoConfiguracao versaoAtual,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        PrecondicaoIfMatch precondicao,
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto,
        FatosDeModalidade fatosDeModalidade,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos) =>
        processo.FecharRetificacao(
            Pedido(
                dados, configuracaoCongeladaCanonica, schemaVersion, algoritmoHash, hashDocumento, atorUsuarioSub,
                clock, contexto, fatosDeModalidade, tiposDeAtoUnicos),
            versaoAtual,
            precondicao);

    private static PedidoDeCongelamento Pedido(
        DadosEdital dados,
        byte[] canonico,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto,
        FatosDeModalidade fatosDeModalidade,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos) =>
        new()
        {
            Dados = dados,
            Documento = new DocumentoCongelado(canonico, schemaVersion, algoritmoHash, hashDocumento),
            AtorUsuarioSub = atorUsuarioSub,
            Clock = clock,
            Contexto = contexto,
            FatosDeModalidade = fatosDeModalidade,
            TiposDeAtoUnicos = tiposDeAtoUnicos,
        };
}
