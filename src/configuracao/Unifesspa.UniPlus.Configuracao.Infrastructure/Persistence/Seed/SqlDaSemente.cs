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
/// linha que a API não aceita. Cada inserção ignora a linha já gravada com a mesma chave.
/// </summary>
internal static class SqlDaSemente
{
    /// <summary>
    /// Os comandos que gravam o termo e a versão promovida dele, que o modelo cita, com o autor da
    /// promoção que a semente declara.
    /// </summary>
    public static IEnumerable<string> ComandosDoTermo(TermoDaSemente semente, DeclaracaoDaSemente declaracao)
    {
        TermoConsentimento termo = TermoRevisado(semente, declaracao);
        TermoConsentimentoVersao versao = declaracao.Exigir(
            termo.Promover(declaracao.Autor, declaracao.Instante), $"promoção do termo {semente.Nome}");

        yield return InserirTermo(semente.TermoId, termo, declaracao.Instante);
        yield return InserirVersaoDoTermo(semente, versao);
    }

    /// <summary>
    /// O comando que grava o modelo, ativo. O conteúdo é serializado pelo mesmo conversor que a
    /// aplicação usa para ler; quem o constrói confere estrutura, blocos por finalidade e grafo das
    /// regras por <see cref="ModeloFormulario.Criar"/>.
    /// </summary>
    public static string ComandoDoModelo(Guid id, ModeloFormulario modelo, DeclaracaoDaSemente declaracao) =>
        Inserir("modelos_formulario", "id",
            ("id", Uuid(id)), ("codigo", Texto(modelo.Codigo)), ("nome", Texto(modelo.Nome)),
            ("descricao", Texto(modelo.Descricao)), ("finalidade", Texto(EstruturaFormulario.ParaToken(modelo.Finalidade))),
            ("tipo_processo_codigo", Texto(modelo.TipoProcessoCodigo)),
            ("conteudo", $"{Texto(ConteudoDoModeloJson.Serializar(modelo.Conteudo))}::jsonb"),
            ("ativo", Logico(modelo.Ativo)), ("created_at", Momento(declaracao.Instante)));

    /// <summary>
    /// A inserção que ignora a linha já gravada com a mesma chave <paramref name="chave"/>. Para os
    /// fatos, os valores e os termos a chave é o identificador fixo: um código já ocupado por outro
    /// cadastro do ambiente falha pelo índice único, com o nome do código, em vez de ser ignorado e
    /// deixar os filhos apontando para a linha que não entrou.
    /// </summary>
    public static string Inserir(string tabela, string chave, params (string Coluna, string Valor)[] colunas) =>
        $"INSERT INTO configuracao.{tabela} ({string.Join(", ", colunas.Select(static c => c.Coluna))}) "
        + $"VALUES ({string.Join(", ", colunas.Select(static c => c.Valor))}) ON CONFLICT ({chave}) DO NOTHING;";

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
        Inserir("termos_de_consentimento", "id",
            ("id", Uuid(termoId)), ("codigo", Texto(termo.Codigo.Valor)), ("nome", Texto(termo.Nome)), ("texto_rascunho", Texto(termo.TextoRascunho)),
            ("base_legal_rascunho", Texto(termo.BaseLegalRascunho)),
            ("forma_aceite_rascunho", Texto(FormasAceite.ParaTokenCanonico(termo.FormaAceiteRascunho))),
            ("revisado", Logico(termo.Revisado)), ("is_deleted", Logico(false)), ("created_at", Momento(instante)));

    private static string InserirVersaoDoTermo(TermoDaSemente semente, TermoConsentimentoVersao versao) =>
        Inserir("versoes_do_termo_consentimento", "id",
            ("id", Uuid(semente.VersaoId)), ("termo_consentimento_id", Uuid(semente.TermoId)),
            ("numero", versao.Numero.ToString(CultureInfo.InvariantCulture)), ("texto", Texto(versao.Texto)), ("base_legal", Texto(versao.BaseLegal)),
            ("forma_aceite", Texto(FormasAceite.ParaTokenCanonico(versao.FormaAceite))), ("hash", Texto(versao.Hash)),
            ("promovida_em", Momento(versao.PromovidaEm)), ("promovida_por", Texto(versao.PromovidaPor)));
}
