namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Os comandos SQL com que as sementes de dado administrado gravam termos e modelos construídos pelas
/// factories do domínio: o que o domínio recusa lança, nomeando a semente e o item, em vez de virar
/// linha que a API não aceita. Cada inserção ignora a linha já gravada com a mesma chave. O termo e o modelo, que a
/// administração cadastra e remove, só entram quando nenhum registro, vivo ou removido, tem o mesmo identificador ou o
/// mesmo código; o modelo também só entra quando cada termo e versão que o conteúdo cita existe e o termo não foi removido.
/// </summary>
internal static class SqlDaSemente
{
    private const string TabelaDosTermos = "configuracao.termos_de_consentimento";
    private const string TabelaDasVersoes = "configuracao.versoes_do_termo_consentimento";
    private const string TabelaDosModelos = "configuracao.modelos_formulario";

    /// <summary>
    /// Os comandos que gravam o termo e a versão promovida dele, que o modelo cita, com o autor da
    /// promoção que a semente declara. O termo é pulado quando algum termo, vivo ou removido, tem o mesmo
    /// identificador ou o mesmo código, e a versão, quando o termo dela não foi gravado ou já tem versão com o mesmo número.
    /// </summary>
    public static IEnumerable<ComandoDaSemente> ComandosDoTermo(TermoDaSemente semente, DeclaracaoDaSemente declaracao)
    {
        TermoConsentimento termo = TermoRevisado(semente, declaracao);
        TermoConsentimentoVersao versao = declaracao.Exigir(
            termo.Promover(declaracao.Autor, declaracao.Instante), $"promoção do termo {semente.Nome}");

        yield return new(InserirTermo(semente.TermoId, termo, declaracao.Instante),
            new ItemDaSemente(TipoDoItemDaSemente.Termo, semente.TermoId, termo.Codigo.Valor));
        yield return new(InserirVersaoDoTermo(semente, versao),
            new ItemDaSemente(TipoDoItemDaSemente.VersaoDoTermo, semente.VersaoId, termo.Codigo.Valor, semente.TermoId));
    }

    /// <summary>
    /// O código que o termo da semente tem depois de gravado, formado do nome pelo domínio: é por ele que o
    /// modelo cita o termo.
    /// </summary>
    public static string CodigoDoTermo(TermoDaSemente semente, DeclaracaoDaSemente declaracao) =>
        TermoRevisado(semente, declaracao).Codigo.Valor;

    /// <summary>
    /// O comando que grava o modelo, ativo. O conteúdo é serializado pelo mesmo conversor que a
    /// aplicação usa para ler; quem o constrói confere estrutura, blocos por finalidade e grafo das
    /// regras por <see cref="ModeloFormulario.Criar"/>. O modelo é pulado quando algum modelo tem o mesmo
    /// identificador ou o mesmo código, ou quando um termo ou uma versão que ele cita não existe, a versão
    /// não é do termo ou o termo foi removido: a mesma exigência que a escrita do modelo faz.
    /// </summary>
    public static ComandoDaSemente ComandoDoModelo(Guid id, ModeloFormulario modelo, DeclaracaoDaSemente declaracao) =>
        new(InserirQuando("modelos_formulario", "id", CondicaoDoModelo(id, modelo),
                ("id", Uuid(id)), ("codigo", Texto(modelo.Codigo)), ("nome", Texto(modelo.Nome)),
                ("descricao", Texto(modelo.Descricao)), ("finalidade", Texto(EstruturaFormulario.ParaToken(modelo.Finalidade))),
                ("tipo_processo_codigo", Texto(modelo.TipoProcessoCodigo)),
                ("conteudo", $"{Texto(ConteudoDoModeloJson.Serializar(modelo.Conteudo))}::jsonb"),
                ("ativo", Logico(modelo.Ativo)), ("created_at", Momento(declaracao.Instante))),
            new ItemDaSemente(TipoDoItemDaSemente.Modelo, id, modelo.Codigo));

    /// <summary>
    /// A inserção que ignora a linha já gravada com a mesma chave <paramref name="chave"/>. Para os
    /// fatos e os valores a chave é o identificador fixo: um código já ocupado por outro cadastro do
    /// ambiente falha pelo índice único, com o nome do código, em vez de ser ignorado e deixar os
    /// filhos apontando para a linha que não entrou.
    /// </summary>
    public static string Inserir(string tabela, string chave, params (string Coluna, string Valor)[] colunas) =>
        $"INSERT INTO configuracao.{tabela} ({Colunas(colunas)}) VALUES ({Valores(colunas)}) ON CONFLICT ({chave}) DO NOTHING;";

    public static string Uuid(Guid id) => $"'{id}'::uuid";

    public static string Texto(string? texto) => texto is null ? "NULL" : $"'{texto.Replace("'", "''", StringComparison.Ordinal)}'";

    public static string Logico(bool valor) => valor ? "TRUE" : "FALSE";

    public static string Momento(DateTimeOffset instante) =>
        $"'{instante.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}+00'::timestamptz";

    public static string ListaDeTexto(IReadOnlyList<string> itens)
    {
        StringBuilder sql = new("ARRAY[");
        sql.Append(string.Join(", ", itens.Select(Texto)));
        sql.Append("]::text[]");
        return sql.ToString();
    }

    private static TermoConsentimento TermoRevisado(TermoDaSemente semente, DeclaracaoDaSemente declaracao)
    {
        TermoConsentimento termo = declaracao.Exigir(
            TermoConsentimento.Criar(
                semente.Nome, semente.Texto, semente.BaseLegal, FormasAceite.ParaTokenCanonico(FormaAceite.RegistroDigitalComLogIp)),
            $"termo {semente.Nome}");
        declaracao.Exigir(termo.MarcarRevisado(declaracao.Autor, declaracao.Instante), $"revisão do termo {semente.Nome}");
        return termo;
    }

    private static string InserirTermo(Guid termoId, TermoConsentimento termo, DateTimeOffset instante) =>
        InserirQuando("termos_de_consentimento", "id", CondicaoDoTermo(termoId, termo),
            ("id", Uuid(termoId)), ("codigo", Texto(termo.Codigo.Valor)), ("nome", Texto(termo.Nome)), ("texto_rascunho", Texto(termo.TextoRascunho)),
            ("base_legal_rascunho", Texto(termo.BaseLegalRascunho)),
            ("forma_aceite_rascunho", Texto(FormasAceite.ParaTokenCanonico(termo.FormaAceiteRascunho))),
            ("revisado", Logico(termo.Revisado)), ("is_deleted", Logico(false)), ("created_at", Momento(instante)));

    // O termo não entra quando algum termo, vivo ou removido, tem o mesmo identificador ou o mesmo código: o que a
    // administração removeu não volta, e o código ocupado por outro termo vivo deixa o termo da semente de fora, sem
    // derrubar a preparação do banco.
    private static string CondicaoDoTermo(Guid termoId, TermoConsentimento termo) =>
        $"NOT EXISTS (SELECT 1 FROM {TabelaDosTermos} WHERE id = {Uuid(termoId)} OR codigo = {Texto(termo.Codigo.Valor)})";

    // O modelo entra quando nenhum modelo tem o mesmo identificador ou código e cada par termo e versão que o conteúdo
    // serializado cita existe, com a versão do termo e o termo vivo.
    private static string CondicaoDoModelo(Guid id, ModeloFormulario modelo) =>
        string.Join(" AND ",
        [
            $"NOT EXISTS (SELECT 1 FROM {TabelaDosModelos} WHERE id = {Uuid(id)} OR codigo = {Texto(modelo.Codigo)})",
            .. modelo.Conteudo.Termos.Select(static t => (t.TermoId, t.VersaoId)).Distinct().Select(CondicaoDoTermoCitado),
        ]);

    private static string CondicaoDoTermoCitado((Guid TermoId, Guid VersaoId) citado) =>
        $"EXISTS (SELECT 1 FROM {TabelaDosTermos} termo JOIN {TabelaDasVersoes} versao ON versao.termo_consentimento_id = termo.id "
        + $"WHERE termo.id = {Uuid(citado.TermoId)} AND versao.id = {Uuid(citado.VersaoId)} AND termo.is_deleted = FALSE)";

    private static string InserirVersaoDoTermo(TermoDaSemente semente, TermoConsentimentoVersao versao) =>
        InserirQuando("versoes_do_termo_consentimento", "id", CondicaoDaVersao(semente, versao),
            ("id", Uuid(semente.VersaoId)), ("termo_consentimento_id", Uuid(semente.TermoId)),
            ("numero", versao.Numero.ToString(CultureInfo.InvariantCulture)), ("texto", Texto(versao.Texto)), ("base_legal", Texto(versao.BaseLegal)),
            ("forma_aceite", Texto(FormasAceite.ParaTokenCanonico(versao.FormaAceite))), ("hash", Texto(versao.Hash)),
            ("promovida_em", Momento(versao.PromovidaEm)), ("promovida_por", Texto(versao.PromovidaPor)));

    // A versão só entra quando o termo dela está gravado e não removido, para não apontar para linha inexistente nem gravar
    // em cima do que a administração removeu, e quando o termo ainda não tem versão com o mesmo número: a restrição única
    // do termo e do número não é a do identificador, e a versão de outro identificador com o mesmo número derrubaria a
    // preparação do banco.
    private static string CondicaoDaVersao(TermoDaSemente semente, TermoConsentimentoVersao versao) =>
        $"EXISTS (SELECT 1 FROM {TabelaDosTermos} WHERE id = {Uuid(semente.TermoId)} AND is_deleted = FALSE) "
        + $"AND NOT EXISTS (SELECT 1 FROM {TabelaDasVersoes} WHERE termo_consentimento_id = {Uuid(semente.TermoId)} "
        + $"AND numero = {versao.Numero.ToString(CultureInfo.InvariantCulture)})";

    private static string InserirQuando(
        string tabela, string chave, string condicao, params (string Coluna, string Valor)[] colunas) =>
        $"INSERT INTO configuracao.{tabela} ({Colunas(colunas)}) SELECT {Valores(colunas)} WHERE {condicao} ON CONFLICT ({chave}) DO NOTHING;";

    private static string Colunas((string Coluna, string Valor)[] colunas) =>
        string.Join(", ", colunas.Select(static c => c.Coluna));

    private static string Valores((string Coluna, string Valor)[] colunas) =>
        string.Join(", ", colunas.Select(static c => c.Valor));
}
