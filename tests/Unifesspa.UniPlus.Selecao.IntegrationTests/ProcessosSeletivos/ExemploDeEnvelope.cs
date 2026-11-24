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

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeVagas;

/// <summary>
/// Exemplo da reidratação — agregados <b>ricos</b>, com ids fixos e valores
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
/// distintos dos congelados — e é exatamente isso que o round-trip acusa. Um exemplo de
/// valores default tornaria o esquecimento invisível.
/// </para>
/// <para>
/// Os ids são <b>fixos</b> — inclusive os das etapas, via
/// <see cref="EtapaProcesso.Reidratar"/> — porque o <c>etapa.Id</c> é o único id de
/// entidade-filha que entra no envelope (ADR-0110 D2). Os das demais filhas não entram,
/// então a volatilidade delas não vaza para os bytes.
/// </para>
/// </remarks>
internal static class ExemploDeEnvelope
{
    internal static readonly SnapshotPublicacaoCanonicalizer Canonicalizador = new();
    internal static readonly EnvelopeCodec Registro = new();

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
    internal static Guid EtapaId(int ordem, int variante) =>
        new($"aaaa000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static readonly Guid OfertaMedicina = new("bbbb0000-0000-4000-8000-000000000001");
    internal static readonly Guid OfertaDireito = new("bbbb0000-0000-4000-8000-000000000002");

    // Filhas da objetiva. Variam pela mesma razão que <see cref="EtapaId"/>: os testes de
    // persistência põem dois processos no MESMO Postgres, e um id fixo aqui colidiria na
    // chave primária de produtos_da_etapa / recursos_da_etapa no segundo deles.
    internal static Guid ProdutoDaObjetivaId(int ordem, int variante) =>
        new($"aaab000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    // Itens de avaliação: o id é congelado no envelope, então varia pela mesma razão das filhas
    // acima — dois processos no mesmo Postgres não podem repetir a chave primária.
    internal static Guid ItemDeAvaliacaoId(int ordem, int variante) =>
        new($"aaad000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static readonly Guid TipoBancaExaminadora = new("dddd2222-0000-4000-8000-000000000001");
    internal static readonly Guid TipoBancaRecursal = new("dddd2222-0000-4000-8000-000000000002");
    internal static readonly Guid TipoBancaAnulacao = new("dddd2222-0000-4000-8000-000000000003");

    internal static Guid BancaDaObjetivaId(int ordem, int variante) =>
        new($"aaad000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static Guid BancaDaEtapaId(int etapa, int variante) =>
        new($"aaae000{variante:x}-0000-4000-8000-00000000000{etapa:x}");

    internal const string AtoAnalise = "RESULTADO_ANALISE";
    internal const string AtoAnulacao = "RESULTADO_ANULACAO";

    internal static Guid RecursoDaObjetivaId(int ordem, int variante) =>
        new($"aaac000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static readonly Guid Documento = new("cccc0000-0000-4000-8000-000000000001");
    internal static readonly Guid ReferenciaDemografica = new("dddd0000-0000-4000-8000-000000000001");
    internal static readonly Guid UnidadeAdministradora = new("eeee0000-0000-4000-8000-000000000001");

    /// <summary>ContaId do publicador — evidência forense, não input de negócio.</summary>
    internal const string Ator = "exemplo-tests";

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    /// <summary>
    /// Convenção de contagem do exemplo. Como as demais referências daqui, o hash é semente
    /// fixa e não o do rol vivo — o codec e o round-trip nunca consultam o catálogo, e o
    /// que precisa ser estável é o byte congelado.
    /// </summary>
    internal static ReferenciaRegra AlgoritmoDeContagemDoExemplo() =>
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
        TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000003"), "ENTREVISTA", "Entrevista", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false, exigeCorrecaoIndependente: true).Value!;

    /// <summary>
    /// Permuta a ORDEM DE ENTRADA de uma coleção não ordenada, sem mudar o conteúdo (Story
    /// #928, §7.5): o mesmo conjunto de itens, apresentado ao agregado em ordem física
    /// inversa, tem de produzir bytes canônicos idênticos — a projeção ordena tudo por
    /// chave determinística antes de serializar. Compartilhado por toda coleção do exemplo
    /// que declara <c>permutar</c>: etapas, distribuição de vagas, critérios de desempate,
    /// cronograma de fases, fatos coletados, regras de derivação, destinos da cascata
    /// (dentro de uma mesma origem) e as regras de comprovação e seus itens.
    /// </summary>
    internal static IReadOnlyList<T> Ordem<T>(IReadOnlyList<T> itens, bool inverter) =>
        inverter ? [.. ((IEnumerable<T>)itens).Reverse()] : itens;
}
