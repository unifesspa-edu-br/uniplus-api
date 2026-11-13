namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;


/// <summary>
/// Corpus da reidratação — agregados <b>ricos</b>, com ids fixos e valores
/// deliberadamente <b>não-default</b>.
/// </summary>
/// <remarks>
/// <para>
/// A golden fixture da #842 <b>não serve de oráculo</b> aqui: ela normaliza os ids
/// voláteis para tokens (<c>&lt;&lt;id-1&gt;&gt;</c>, que não é um GUID e não decodifica) e
/// o agregado dela é pobre de propósito — atendimento vazio, sem bônus, sem desempate,
/// sem eliminações. Um decoder que perdesse metade dos campos passaria por ela.
/// </para>
/// <para>
/// <b>Todo valor aqui é não-default por desenho.</b> Um decoder que esquecesse um campo
/// e o deixasse cair no default (<c>null</c>, <c>0</c>, string vazia) produziria bytes
/// distintos dos congelados — e é exatamente isso que o round-trip acusa. Um corpus de
/// valores default tornaria o esquecimento invisível.
/// </para>
/// <para>
/// Os ids são <b>fixos</b> — inclusive os das etapas, via
/// <see cref="EtapaProcesso.Reidratar"/> — porque o <c>etapa.Id</c> é o único id de
/// entidade-filha que entra no envelope (ADR-0110 D2). Os das demais filhas não entram,
/// então a volatilidade delas não vaza para os bytes.
/// </para>
/// </remarks>
internal static partial class CorpusEnvelope
{
    internal static readonly EnvelopeCodec Codec = new();
    internal static readonly RegistroCodecsEnvelope Registro = new();

    internal const string HashDocumento = "1111111111111111111111111111111111111111111111111111111111111111";

    internal static readonly Guid AtoAbertura = new("01900000-0000-7000-8000-000000000001");
    internal static readonly Guid AtoRetificador = new("01900000-0000-7000-8000-000000000002");

    /// <summary>
    /// Ids de etapa <b>fixos</b> — é o que torna a golden fixture determinística (o
    /// <c>etapa.Id</c> é o único id de filha que entra no envelope, ADR-0110 D2).
    /// </summary>
    /// <remarks>
    /// A <paramref name="variante"/> existe só para os testes de <b>persistência</b>: eles
    /// compartilham um Postgres por classe, e dois processos com as mesmas etapas
    /// colidiriam na chave primária de <c>etapas_processo</c>. A variante 0 é a do envelope
    /// congelado — mudá-la mudaria a fixture.
    /// </remarks>
    private static Guid EtapaId(int ordem, int variante) =>
        new($"aaaa000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static readonly Guid OfertaMedicina = new("bbbb0000-0000-4000-8000-000000000001");
    private static readonly Guid OfertaDireito = new("bbbb0000-0000-4000-8000-000000000002");

    // Filhas da objetiva. Variam pela mesma razão que <see cref="EtapaId"/>: os testes de
    // persistência põem dois processos no MESMO Postgres, e um id fixo aqui colidiria na
    // chave primária de produtos_da_etapa / recursos_da_etapa no segundo deles.
    private static Guid ProdutoDaObjetivaId(int ordem, int variante) =>
        new($"aaab000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    // Itens de avaliação: o id é congelado no envelope, então varia pela mesma razão das filhas
    // acima — dois processos no mesmo Postgres não podem repetir a chave primária.
    private static Guid ItemDeAvaliacaoId(int ordem, int variante) =>
        new($"aaad000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static readonly Guid TipoBancaExaminadora = new("dddd2222-0000-4000-8000-000000000001");
    private static readonly Guid TipoBancaRecursal = new("dddd2222-0000-4000-8000-000000000002");
    private static readonly Guid TipoBancaAnulacao = new("dddd2222-0000-4000-8000-000000000003");

    private static Guid BancaDaObjetivaId(int ordem, int variante) =>
        new($"aaad000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    /// <summary>
    /// Código de ato com acento gravado em forma <b>decomposta</b> — <c>A</c> mais acento agudo
    /// combinante — e não na forma composta que um teclado produz.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As duas formas são o mesmo texto para um leitor humano e <b>bytes diferentes</b> para
    /// qualquer comparação, ordenação ou hash. O canonicalizador normaliza para a composta antes
    /// de emitir, e é isso que faz o envelope de dois operadores — um em Linux, outro em macOS,
    /// que normaliza nomes de arquivo em decomposto — produzir o mesmo hash.
    /// </para>
    /// <para>
    /// Está no corpus em decomposto <b>de propósito</b>: com todos os códigos em ASCII, como
    /// estavam, a normalização era identidade e removê-la não mudava um byte das fixtures. O
    /// golden só prova o que o corpus contém.
    /// </para>
    /// </remarks>
    internal const string AtoAnaliseDecomposto = "RESULTADO_ANA\u0301LISE";

    /// <summary>
    /// O par de <see cref="AtoAnaliseDecomposto"/>: sem acento, e escolhido para que a ordem
    /// entre os dois <b>vire</b> conforme o texto esteja normalizado ou não.
    /// </summary>
    /// <remarks>
    /// Os dois coincidem até <c>RESULTADO_AN</c>. No caractere seguinte, a forma decomposta traz
    /// o <c>A</c> base (U+0041) e a composta traz o <c>Á</c> inteiro (U+00C1), enquanto o par traz
    /// <c>U</c> (U+0055) — que fica <b>entre</b> os dois. Ordenar o texto cru põe a análise antes;
    /// ordenar o normalizado põe a anulação antes.
    ///
    /// É essa inversão que torna a normalização da CHAVE DE ORDENAÇÃO observável. Sem um par
    /// assim, dois códigos acentuados quaisquer saem na mesma ordem das duas formas — porque
    /// divergem antes de chegar ao acento — e remover a normalização da chave não muda um byte.
    /// </remarks>
    internal const string AtoAnulacao = "RESULTADO_ANULACAO";

    /// <summary>Mesmo texto do ato acentuado, na forma composta que o envelope tem de emitir.</summary>
    internal const string AtoAnaliseComposto = "RESULTADO_AN\u00C1LISE";

    /// <summary>Código de banca acentuado, também em forma decomposta, pela mesma razão.</summary>
    internal const string BancaAnaliseDecomposta = "BANCA_ANA\u0301LISE";

    /// <summary>Par da banca acima, pela mesma construção: a ordem entre as duas vira com a normalização.</summary>
    internal const string BancaAnulacao = "BANCA_ANULACAO";

    private static Guid RecursoDaObjetivaId(int ordem, int variante) =>
        new($"aaac000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static readonly Guid Documento = new("cccc0000-0000-4000-8000-000000000001");
    private static readonly Guid ReferenciaDemografica = new("dddd0000-0000-4000-8000-000000000001");
    private static readonly Guid UnidadeAdministradora = new("eeee0000-0000-4000-8000-000000000001");

    /// <summary>Sub do publicador — evidência forense, não input de negócio.</summary>
    internal const string Ator = "corpus-tests";

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    /// <summary>
    /// Convenção de contagem do corpus. Como as demais referências daqui, o hash é semente
    /// fixa e não o do rol vivo — o codec e o round-trip nunca consultam o catálogo, e o
    /// que precisa ser estável é o byte congelado.
    /// </summary>
    internal static ReferenciaRegra AlgoritmoDeContagemDoCorpus() =>
        Regra(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, '8');

    /// <summary>
    /// Snapshots de tipo de etapa fixos (issue #1071) — mesmos <c>origemId</c> semeados no
    /// cadastro real de Configuração (<c>CriaCadastroTiposEtapa</c>), por legibilidade; o
    /// decoder/round-trip nunca consulta o cadastro, então a coincidência não é requisito.
    /// </summary>
    internal static TipoEtapaSnapshot TipoEtapaProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    internal static TipoEtapaSnapshot TipoEtapaRedacao() =>
        TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000002"), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    /// <summary>
    /// Tipo que só elimina: congela <c>admitePontuacao = false</c>, para que o envelope rico
    /// carregue os dois valores do sinalizador e o round-trip prove ambos.
    /// </summary>
    internal static TipoEtapaSnapshot TipoEtapaEntrevista() =>
        TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000003"), "ENTREVISTA", "Entrevista", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    /// <summary>
    /// Permuta a ORDEM DE ENTRADA de uma coleção não ordenada, sem mudar o conteúdo (Story
    /// #928, §7.5): o mesmo conjunto de itens, apresentado ao agregado em ordem física
    /// inversa, tem de produzir bytes canônicos idênticos — a projeção ordena tudo por
    /// chave determinística antes de serializar. Compartilhado por toda coleção do corpus
    /// que declara <c>permutar</c>: etapas, distribuição de vagas, critérios de desempate,
    /// cronograma de fases, fatos coletados, regras de derivação, destinos da cascata
    /// (dentro de uma mesma origem) e a árvore de satisfação (raízes e filhos).
    /// </summary>
    private static IReadOnlyList<T> Ordem<T>(IReadOnlyList<T> itens, bool inverter) =>
        inverter ? [.. ((IEnumerable<T>)itens).Reverse()] : itens;
}
