namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Os comandos SQL com que as sementes de dado administrado gravam termos e modelos construídos pelas
/// factories do domínio: o que o domínio recusa lança, nomeando a semente e o item, em vez de virar
/// linha que a API não aceita. Cada inserção ignora a linha já gravada com a mesma chave.
/// </summary>
internal static class SqlDaSemente
{
    /// <summary>O valor do resultado, ou a recusa do domínio como exceção que nomeia a semente e o item.</summary>
    public static T Exigir<T>(Result<T> resultado, string semente, string oQue) =>
        resultado.IsSuccess ? resultado.Value! : throw Recusa(resultado.Errors, semente, oQue);

    /// <summary>A recusa do domínio como exceção que nomeia a semente e o item.</summary>
    public static void Exigir(Result resultado, string semente, string oQue)
    {
        if (resultado.IsFailure)
        {
            throw Recusa(resultado.Errors, semente, oQue);
        }
    }

    /// <summary>
    /// Os comandos que gravam o termo e a versão promovida dele, que o modelo cita, com o autor da
    /// promoção que a semente declara.
    /// </summary>
    public static IEnumerable<string> ComandosDoTermo(
        Guid termoId, Guid versaoId, string nome, string texto, string baseLegal, string autor, DateTimeOffset instante, string semente)
    {
        TermoConsentimento termo = Exigir(
            TermoConsentimento.Criar(nome, texto, baseLegal, FormasAceite.ParaTokenCanonico(FormaAceite.RegistroDigitalComLogIp)),
            semente, $"termo {nome}");
        Exigir(termo.MarcarRevisado(autor, instante), semente, $"revisão do termo {nome}");
        TermoConsentimentoVersao versao = Exigir(termo.Promover(autor, instante), semente, $"promoção do termo {nome}");

        yield return Inserir("termo_consentimento", "id",
            ("id", Uuid(termoId)), ("nome", Texto(termo.Nome)), ("texto_rascunho", Texto(termo.TextoRascunho)),
            ("base_legal_rascunho", Texto(termo.BaseLegalRascunho)),
            ("forma_aceite_rascunho", Texto(FormasAceite.ParaTokenCanonico(termo.FormaAceiteRascunho))),
            ("revisado", Logico(termo.Revisado)), ("is_deleted", Logico(false)), ("created_at", Momento(instante)));
        yield return Inserir("termo_consentimento_versao", "id",
            ("id", Uuid(versaoId)), ("termo_consentimento_id", Uuid(termoId)),
            ("texto", Texto(versao.Texto)), ("base_legal", Texto(versao.BaseLegal)),
            ("forma_aceite", Texto(FormasAceite.ParaTokenCanonico(versao.FormaAceite))), ("hash", Texto(versao.Hash)),
            ("promovida_em", Momento(versao.PromovidaEm)), ("promovida_por", Texto(versao.PromovidaPor)));
    }

    /// <summary>
    /// O comando que grava o modelo, ativo. O conteúdo é conferido por <see cref="ModeloFormulario.Criar"/>
    /// — estrutura, blocos por finalidade e grafo das regras — e serializado pelo mesmo conversor que a
    /// aplicação usa para ler.
    /// </summary>
    public static string ComandoDoModelo(
        Guid id, string codigo, string nome, string descricao, FinalidadeFormulario finalidade, string? tipoProcessoCodigo,
        ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes, DateTimeOffset instante, string semente)
    {
        ModeloFormulario modelo = Exigir(
            ModeloFormulario.Criar(codigo, nome, descricao, finalidade, tipoProcessoCodigo, conteudo, derivacoes),
            semente, $"modelo {codigo}");
        Exigir(modelo.Ativar(), semente, $"ativação do modelo {codigo}");

        return Inserir("modelos_formulario", "id",
            ("id", Uuid(id)), ("codigo", Texto(modelo.Codigo)), ("nome", Texto(modelo.Nome)),
            ("descricao", Texto(modelo.Descricao)), ("finalidade", Texto(EstruturaFormulario.ParaToken(modelo.Finalidade))),
            ("tipo_processo_codigo", Texto(modelo.TipoProcessoCodigo)),
            ("conteudo", $"{Texto(ConteudoDoModeloJson.Serializar(modelo.Conteudo))}::jsonb"),
            ("ativo", Logico(modelo.Ativo)), ("created_at", Momento(instante)));
    }

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

    private static InvalidOperationException Recusa(IEnumerable<FieldError> erros, string semente, string oQue) =>
        new($"A semente {semente} tem {oQue} que o domínio recusa: "
            + string.Join("; ", erros.Select(static e => $"{e.Field}: {e.Error.Message}")));
}
