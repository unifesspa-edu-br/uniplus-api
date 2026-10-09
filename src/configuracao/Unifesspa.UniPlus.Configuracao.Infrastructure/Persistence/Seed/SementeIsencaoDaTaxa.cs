namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Globalization;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// O modelo do formulário de solicitação de isenção da taxa de inscrição que existe ao subir o
/// sistema, com os termos que ele exige. Não tem tipo de processo: serve a qualquer processo seletivo
/// que cobre taxa (UNI-REQ-0144).
/// </summary>
/// <remarks>
/// <para>
/// O formulário coleta o que a isenção por carência socioeconômica da Lei nº 12.799/2013 pede
/// (UNI-REQ-0149): a renda familiar per capita, declarada no pedido; a origem escolar não é perguntada
/// de novo, porque vem da inscrição — o modelo a pressupõe, e só se aplica a processo cuja inscrição a
/// coleta. A condição do direito é <see cref="IsencaoPorCarenciaSocioeconomica.Condicao"/>, sobre as
/// respostas, sem fato derivado próprio.
/// </para>
/// <para>
/// É dado administrado — depois de semeado, edita-se pelas telas de administração —, construído pelas
/// factories do domínio e gravado com identificadores fixos, de prefixo próprio, para a inserção ser
/// idempotente. Os textos dos termos são proposta, confirmada pela tela.
/// </para>
/// </remarks>
public static class SementeIsencaoDaTaxa
{
    /// <summary>O código do modelo de solicitação de isenção.</summary>
    public const string ModeloDeIsencao = "ISENCAO_TAXA_INSCRICAO";

    private const string NomeDaSemente = "da isenção da taxa";

    /// <summary>Quem consta como autor da promoção dos termos semeados, que não passaram por pessoa.</summary>
    private const string AutorDaSemente = "semente-isencao-da-taxa";

    private static readonly DateTimeOffset Instante = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

    private const int TabelaTermo = 1;
    private const int TabelaVersaoDoTermo = 2;
    private const int TabelaModelo = 3;

    /// <summary>
    /// O identificador fixo da linha <paramref name="n"/> da tabela <paramref name="tabela"/>; cada
    /// tabela tem o seu segmento, para uma identidade nunca se repetir entre tabelas.
    /// </summary>
    private static Guid Id(int tabela, int n) =>
        Guid.Parse($"15e0{tabela:D4}-0000-7000-8000-{n:D12}", CultureInfo.InvariantCulture);

    /// <summary>Um termo do pedido de isenção: o código pelo qual o modelo o cita, o nome, o texto e a base legal.</summary>
    public sealed record TermoDaSemente(int N, string Codigo, string Nome, string Texto, string BaseLegal)
    {
        /// <summary>O identificador do termo.</summary>
        public Guid TermoId => Id(TabelaTermo, N);

        /// <summary>O identificador da versão promovida, que o modelo cita.</summary>
        public Guid VersaoId => Id(TabelaVersaoDoTermo, N);
    }

    /// <summary>Os termos que o pedido de isenção exige. Os textos são proposta.</summary>
    public static IReadOnlyList<TermoDaSemente> Termos { get; } =
    [
        new(1, "VERACIDADE_DO_PEDIDO_DE_ISENCAO", "Veracidade das informações do pedido de isenção",
            "Declaro que as informações que presto neste pedido de isenção da taxa de inscrição são verdadeiras e que posso comprová-las quando solicitado. Estou ciente de que a informação falsa leva ao indeferimento do pedido, sem prejuízo das sanções legais.",
            "Lei nº 12.799/2013, art. 1º; edital do processo seletivo."),
        new(2, "USO_DOS_DADOS_DO_PEDIDO_DE_ISENCAO", "Uso dos dados do pedido de isenção",
            "Autorizo a Unifesspa a utilizar os dados que informo neste pedido, e os da minha inscrição, exclusivamente para a análise da isenção da taxa de inscrição deste processo seletivo.",
            "Lei nº 13.709/2018 (LGPD), art. 7º, inciso I; edital do processo seletivo."),
    ];

    /// <summary>
    /// Os comandos SQL que gravam os termos e o modelo, ativo, na ordem das chaves estrangeiras. Cada
    /// inserção ignora a linha que já existe: reaplicar não duplica nem altera.
    /// </summary>
    public static IReadOnlyList<string> Comandos() =>
    [
        .. Termos.SelectMany(static t => SqlDaSemente.ComandosDoTermo(
            t.TermoId, t.VersaoId, t.Nome, t.Texto, t.BaseLegal, AutorDaSemente, Instante, NomeDaSemente)),
        SqlDaSemente.ComandoDoModelo(
            Id(TabelaModelo, 1), ModeloDeIsencao, "Solicitação de isenção da taxa de inscrição",
            "Pedido de isenção da taxa de inscrição pela Lei nº 12.799/2013, para qualquer processo seletivo que cobre taxa.",
            FinalidadeFormulario.IsencaoTaxa, tipoProcessoCodigo: null, Conteudo(),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal), Instante, NomeDaSemente),
    ];

    /// <summary>Os comandos que apagam o que a semente grava, para o <c>Down</c> da migration.</summary>
    public static IReadOnlyList<string> ComandosDeRemocao() =>
    [
        $"DELETE FROM configuracao.modelos_formulario WHERE id::text LIKE '15e0{TabelaModelo:D4}-%';",
        $"DELETE FROM configuracao.termo_consentimento_versao WHERE id::text LIKE '15e0{TabelaVersaoDoTermo:D4}-%';",
        $"DELETE FROM configuracao.termo_consentimento WHERE id::text LIKE '15e0{TabelaTermo:D4}-%';",
    ];

    private static ConteudoDoModelo Conteudo()
    {
        const string renda = "RENDA_FAMILIAR";

        return new(
            "Solicitação de isenção da taxa de inscrição",
            [
                new EtapaDoModelo(renda, 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Renda familiar",
                    "A isenção pela Lei nº 12.799/2013 é de quem tem renda familiar per capita de até um salário mínimo e meio e cursou o ensino médio completo em escola pública ou como bolsista integral em escola privada. A origem escolar é a que você informou na inscrição.",
                    null, null),
                new EtapaDoModelo("DOCUMENTOS", 2, TipoEtapaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental, "Documentos", null, null, null),
                new EtapaDoModelo("REVISAO", 3, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null, null),
            ],
            [
                new ItemDoModelo(
                    IsencaoPorCarenciaSocioeconomica.FatoRenda, 1, renda,
                    "A renda da sua família é de até um salário mínimo e meio por pessoa?", TipoRenderizacao.Booleano, null,
                    "Some a renda bruta mensal de todos da família e divida pelo número de pessoas. Exatamente um salário mínimo e meio também dá direito.",
                    Obrigatoriedade.Sempre, null, [], PedirConfirmacao: false),
            ],
            [.. Termos.Select(static (t, i) => new TermoDoModelo(t.Codigo, i + 1, t.TermoId, t.VersaoId, null, Obrigatoriedade.Sempre))],
            IsencaoPorCarenciaSocioeconomica.FatosDaInscricao,
            []);
    }
}
