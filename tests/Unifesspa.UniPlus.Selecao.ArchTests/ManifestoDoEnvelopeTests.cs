namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O <b>manifesto</b> do envelope: toda propriedade de negócio das entidades de
/// configuração está declarada como <b>dentro</b> ou <b>fora</b> dele — e a omissão é
/// impossível (Story #859).
/// </summary>
/// <remarks>
/// <para>
/// <b>O ponto cego que este teste fecha.</b> Encoder, decoder e round-trip são três listas
/// escritas à mão. Se alguém acrescentar uma propriedade de negócio a
/// <c>ModalidadeSelecionada</c> — digamos, um novo critério de elegibilidade — e esquecer
/// dela <b>nos dois</b>, o envelope continua idêntico, a golden fixture continua batendo,
/// o round-trip continua verde e a guarda de produção aprova. O campo simplesmente
/// <b>não existe</b> para o congelamento: ele é configurado, publicado, e some no primeiro
/// descarte. <b>Nada acusa</b> — a simetria do esquecimento o torna invisível.
/// </para>
/// <para>
/// A única defesa é uma lista que o compilador force a manter. Aqui, acrescentar uma
/// propriedade a qualquer entidade do grafo <b>quebra o build</b> até que alguém declare,
/// por escrito, se ela entra no documento que tem peso jurídico — ou por que não entra.
/// </para>
/// </remarks>
public sealed class ManifestoDoEnvelopeTests
{
    /// <summary>Herdadas de <c>EntityBase</c> — identidade e auditoria, nunca conteúdo de negócio.</summary>
    private static readonly HashSet<string> DaEntidadeBase =
        ["Id", "CreatedAt", "UpdatedAt", "DomainEvents", "IsDeleted", "DeletedAt", "DeletedBy"];

    /// <summary>
    /// As uniões discriminadas do grafo. A variante é escolhida pelo <b>código da
    /// regra</b>, porque o envelope não carrega discriminador — o que torna cada nova
    /// variante uma chance a mais de o codec silenciosamente não conhecê-la.
    /// </summary>
    private static readonly Type[] UnioesDiscriminadas =
        [typeof(ParametrosCriterioDesempate), typeof(ParametrosRegraEliminacao), typeof(PredicadoObrigatoriedade), typeof(ParametrosRegraAjusteDistribuicao)];

    private const string OrientacaoDePropriedadeNaoDeclarada =
        "uma propriedade de negócio que não está no manifesto é uma que ninguém decidiu se entra no documento " +
        "com peso jurídico. Se ela for esquecida no encoder E no decoder — o que é fácil, são duas listas " +
        "manuais —, o envelope não muda, a golden fixture não muda, o round-trip fica verde e a guarda de " +
        "produção aprova: o campo é configurado, publicado, e some no primeiro descarte, sem que nada acuse. " +
        "Declare-a em ManifestoDoEnvelope: congelada, ou excluída com o motivo. ";

    private const string OrientacaoDeTipoNaoDeclarado =
        "todo tipo que o envelope congela tem de estar no manifesto — inclusive cada VARIANTE de " +
        "ParametrosCriterioDesempate e ParametrosRegraEliminacao, que são persistidas inteiras como JSON e lidas por dois " +
        "`switch` sobre o código da regra. Uma variante nova, ou uma propriedade nova numa existente, esquecida " +
        "nos dois switches, não muda o envelope, não muda a fixture, e o round-trip fica verde: o campo some no " +
        "primeiro descarte. ";

    [Fact(DisplayName = "Toda propriedade de negócio do grafo está declarada no manifesto — dentro ou fora do envelope")]
    public void TodaPropriedade_EstaNoManifesto()
    {
        IEnumerable<string> naoDeclaradas = ManifestoDoEnvelope.Entradas.SelectMany(static entrada =>
        {
            HashSet<string> declaradas = [.. entrada.Value.Congeladas, .. entrada.Value.Excluidas.Select(static e => e.Propriedade)];

            return PropriedadesDeNegocio(entrada.Key)
                .Where(propriedade => !declaradas.Contains(propriedade))
                .Select(propriedade => $"{entrada.Key.Name}.{propriedade}");
        });

        naoDeclaradas.Should().BeEmpty(OrientacaoDePropriedadeNaoDeclarada + $"Não declaradas: {string.Join(", ", naoDeclaradas)}");
    }

    /// <summary>
    /// O manifesto não pode declarar o que não existe: uma propriedade renomeada ou removida
    /// deixaria uma entrada morta, e o teste acima continuaria verde protegendo um fantasma.
    /// </summary>
    [Fact(DisplayName = "O manifesto não declara propriedade que não existe mais")]
    public void Manifesto_NaoTemEntradaMorta()
    {
        IEnumerable<string> fantasmas = ManifestoDoEnvelope.Entradas.SelectMany(static entrada =>
        {
            HashSet<string> reais = [.. PropriedadesPublicas(entrada.Key)];

            return entrada.Value.Congeladas
                .Concat(entrada.Value.Excluidas.Select(static e => e.Propriedade))
                .Where(declarada => !reais.Contains(declarada))
                .Select(declarada => $"{entrada.Key.Name}.{declarada}");
        });

        fantasmas.Should().BeEmpty(
            $"o manifesto declara propriedades que não existem mais no domínio: {string.Join(", ", fantasmas)}");
    }

    /// <summary>
    /// Toda exclusão carrega um <b>motivo</b>. Uma propriedade excluída do envelope sem
    /// justificativa escrita é uma decisão que ninguém tomou — e é exatamente onde uma
    /// dimensão de negócio se perde por descuido.
    /// </summary>
    [Fact(DisplayName = "Toda propriedade excluída do envelope declara o motivo")]
    public void Exclusoes_TemMotivo()
    {
        IEnumerable<string> semMotivo = ManifestoDoEnvelope.Entradas.SelectMany(static entrada => entrada.Value.Excluidas
            .Where(static excluida => string.IsNullOrWhiteSpace(excluida.Motivo))
            .Select(excluida => $"{entrada.Key.Name}.{excluida.Propriedade}"));

        semMotivo.Should().BeEmpty("uma propriedade fora do envelope tem de ter escrito o porquê");
    }

    /// <summary>
    /// O manifesto não pode <b>ele mesmo</b> ser uma lista escrita à mão que alguém esquece
    /// de estender: os tipos são <b>descobertos</b> a partir do
    /// <see cref="ConfiguracaoPublicada"/>, e cada um que aparecer tem de estar declarado.
    /// </summary>
    /// <remarks>
    /// Sem isto, o manifesto teria o mesmo ponto cego que ele existe para fechar — uma
    /// entidade nova no grafo (ou uma <b>variante nova</b> de <c>Parametros</c>) simplesmente não
    /// seria percorrida, e a propriedade dela nunca precisaria ser declarada. A prova de
    /// completude tem de vir do compilador, não da memória de quem editou o teste.
    /// </remarks>
    [Fact(DisplayName = "Todo tipo alcançável a partir do grafo está no manifesto — inclusive cada variante de Parâmetros")]
    public void TodoTipoDoGrafo_EstaNoManifesto()
    {
        HashSet<Type> alcancados = [];
        AlcancarAsPropriedadesDe(typeof(ConfiguracaoPublicada), alcancados);

        // DadosEdital e ResultadoConformidade não pendem do grafo — entram na canonicalização por fora
        // (bloco `periodo` + `hashesEdital`; `documentosExigidos.obrigatoriedades[]`), mas são igualmente
        // congelados e igualmente reidratados.
        Alcancar(typeof(DadosEdital), alcancados);
        Alcancar(typeof(ResultadoConformidade), alcancados);

        IEnumerable<Type> naoDeclarados = alcancados.Where(t => !ManifestoDoEnvelope.Entradas.ContainsKey(t));

        naoDeclarados.Should().BeEmpty(
            OrientacaoDeTipoNaoDeclarado + $"Não declarados: {string.Join(", ", naoDeclarados.Select(static t => t.Name))}");
    }

    private static IEnumerable<string> PropriedadesPublicas(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(static p => p.Name);

    private static IEnumerable<string> PropriedadesDeNegocio(Type tipo) =>
        PropriedadesPublicas(tipo).Where(nome => !DaEntidadeBase.Contains(nome));

    private static void AlcancarAsPropriedadesDe(Type tipo, HashSet<Type> acumulador)
    {
        IEnumerable<PropertyInfo> deNegocio = tipo
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static p => !DaEntidadeBase.Contains(p.Name));

        foreach (PropertyInfo propriedade in deNegocio)
        {
            Alcancar(TipoDeNegocio(propriedade.PropertyType), acumulador);
        }
    }

    /// <summary>
    /// Acrescenta o tipo ao conjunto e desce recursivamente pelas propriedades dele —
    /// abrindo as uniões discriminadas em <b>todas</b> as suas variantes.
    /// </summary>
    private static void Alcancar(Type? tipo, HashSet<Type> acumulador)
    {
        if (tipo is null || !EDoDominio(tipo) || !acumulador.Add(tipo))
        {
            return;
        }

        if (UnioesDiscriminadas.Contains(tipo))
        {
            acumulador.Remove(tipo);
            AlcancarAsVariantesDe(tipo, acumulador);
            return;
        }

        AlcancarAsPropriedadesDe(tipo, acumulador);
    }

    // A união abstrata não tem propriedades próprias; o que o envelope congela são as
    // VARIANTES dela. Abri-las aqui é o que faz uma variante nova quebrar o build.
    private static void AlcancarAsVariantesDe(Type uniao, HashSet<Type> acumulador)
    {
        foreach (Type variante in uniao.Assembly.GetTypes().Where(t => t.IsSubclassOf(uniao) && !t.IsAbstract))
        {
            Alcancar(variante, acumulador);
        }
    }

    /// <summary>Desembrulha <c>IReadOnlyCollection&lt;T&gt;</c> / <c>Nullable&lt;T&gt;</c> e devolve o tipo que interessa.</summary>
    private static Type? TipoDeNegocio(Type tipo)
    {
        Type alvo = Nullable.GetUnderlyingType(tipo) ?? tipo;
        Type desembrulhado = alvo.IsGenericType && alvo.GetGenericArguments().Length == 1 ? alvo.GetGenericArguments()[0] : alvo;

        return EDoDominio(desembrulhado) ? desembrulhado : null;
    }

    private static bool EDoDominio(Type tipo) =>
        tipo.Namespace?.StartsWith("Unifesspa.UniPlus.Selecao.Domain", StringComparison.Ordinal) == true
        && !tipo.IsEnum
        && tipo != typeof(ProcessoSeletivo);
}
