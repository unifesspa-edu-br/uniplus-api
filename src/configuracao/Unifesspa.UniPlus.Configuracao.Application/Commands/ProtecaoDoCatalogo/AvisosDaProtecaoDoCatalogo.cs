namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using System.Net;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Monta os e-mails do circuito de aprovação da proteção de dados do catálogo de tipos de documento e de fatos do
/// candidato para quem tem a permissão de aprovar e para quem tem a de se manifestar. Os handlers devolvem o resultado
/// como mensagens de saída, que entram no outbox junto da gravação.
/// </summary>
/// <remarks>
/// O corpo traz só o que o próprio cadastro do item declara — quantidades, cadastro, nome e código do item,
/// classificação —, nunca nome de pessoa nem justificativa: quem agiu fica registrado no item, que só quem tem acesso
/// abre.
/// </remarks>
internal static class AvisosDaProtecaoDoCatalogo
{
    public static IReadOnlyList<EnviarEmail> DaSolicitacao(IReadOnlyList<string> destinatarios, int tiposDeDocumento, int fatos)
    {
        string abertura =
            $"Há itens do catálogo de tipos de documento e de fatos do candidato aguardando a aprovação da proteção de dados: {Itens(tiposDeDocumento, fatos)}.";
        const string chamada =
            "Abra no Uni+ a área de proteção de dados do catálogo de tipos de documento e de fatos do candidato para aprovar, recomendar ajuste ou reprovar cada um.";

        return Montar(
            destinatarios,
            "Solicitação de aprovação da proteção de dados do catálogo de tipos de documento e de fatos do candidato",
            $"{abertura}\n\n{chamada}",
            $"<p>{WebUtility.HtmlEncode(abertura)}</p><p>{chamada}</p>",
            "catalogo.protecao.solicitacao");
    }

    /// <summary>
    /// O aviso da solicitação a quem tem a permissão de se manifestar (UNI-REQ-0373): a manifestação técnica é opcional e
    /// não decide.
    /// </summary>
    public static IReadOnlyList<EnviarEmail> DaSolicitacaoParaManifestacao(IReadOnlyList<string> destinatarios, int tiposDeDocumento, int fatos)
    {
        string abertura =
            $"Há itens do catálogo de tipos de documento e de fatos do candidato com a aprovação da proteção de dados solicitada: {Itens(tiposDeDocumento, fatos)}.";
        const string chamada =
            "Abra no Uni+ a área de proteção de dados do catálogo de tipos de documento e de fatos do candidato se quiser registrar manifestação técnica sobre cada um; a decisão cabe ao Representante do controlador.";

        return Montar(
            destinatarios,
            "Solicitação de aprovação da proteção de dados do catálogo de tipos de documento e de fatos do candidato aberta a manifestação",
            $"{abertura}\n\n{chamada}",
            $"<p>{WebUtility.HtmlEncode(abertura)}</p><p>{chamada}</p>",
            "catalogo.protecao.solicitacao.manifestacao");
    }

    public static IReadOnlyList<EnviarEmail> DoAceite(
        IReadOnlyList<string> destinatarios, string cadastro, string nome, string codigo, ClassificacaoDeProtecao classificacao)
    {
        string aviso = $"A recomendação do Representante do controlador foi aceita: o {cadastro} {nome} ({codigo}) está aprovado como {Rotulo(classificacao)}.";

        return Montar(
            destinatarios,
            "Recomendação do Representante do controlador aceita no catálogo de tipos de documento e de fatos do candidato",
            aviso,
            $"<p>{WebUtility.HtmlEncode(aviso)}</p>",
            "catalogo.protecao.aceite");
    }

    private static IReadOnlyList<EnviarEmail> Montar(
        IReadOnlyList<string> destinatarios, string assunto, string corpoEmTexto, string corpoEmHtml, string finalidade) =>
        [.. destinatarios
            .Select(Email.Criar)
            .Where(email => email.IsSuccess)
            .Select(email => EnviarEmail.Para(email.Value!, assunto, corpoEmTexto, corpoEmHtml, finalidade))];

    private static string Itens(int tiposDeDocumento, int fatos) =>
        string.Join(" e ", new[]
        {
            tiposDeDocumento > 0 ? Quantidade(tiposDeDocumento, "tipo de documento", "tipos de documento") : null,
            fatos > 0 ? Quantidade(fatos, "fato", "fatos") : null,
        }.OfType<string>());

    private static string Quantidade(int quantidade, string singular, string plural) =>
        quantidade == 1 ? $"1 {singular}" : $"{quantidade} {plural}";

    private static string Rotulo(ClassificacaoDeProtecao classificacao) => classificacao switch
    {
        ClassificacaoDeProtecao.Publico => "público",
        ClassificacaoDeProtecao.Interno => "interno",
        ClassificacaoDeProtecao.Pessoal => "pessoal",
        ClassificacaoDeProtecao.Identificador => "identificador",
        ClassificacaoDeProtecao.Sensivel => "sensível",
        _ => throw new ArgumentOutOfRangeException(nameof(classificacao), classificacao, "Classificação fora do domínio fechado."),
    };
}
