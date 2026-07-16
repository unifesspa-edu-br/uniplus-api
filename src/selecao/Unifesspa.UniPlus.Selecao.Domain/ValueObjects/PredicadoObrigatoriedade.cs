namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Conjunto fechado de predicados de validação legal aplicáveis a um
/// processo seletivo, modelado como discriminated union (sealed records
/// derivados de <see cref="PredicadoObrigatoriedade"/>). Cada variante
/// representa uma shape distinta de regra do catálogo de
/// <c>ObrigatoriedadeLegal</c>, reconhecida pela avaliação de conformidade
/// (ADR-0058).
/// </summary>
/// <remarks>
/// <para>Conforme ADR-0058 (validação data-driven com citação), a forma é
/// fechada por design: novas categorias de regra exigem adição de variante
/// tipada explícita + amendment do ADR. A válvula <see cref="Customizado"/>
/// existe apenas para regras transitórias enquanto a categoria definitiva
/// não é desenhada — uso emite warning no avaliador.</para>
/// <para>Persistência via System.Text.Json polimórfico (atributos abaixo).
/// O discriminator <c>$tipo</c> bate com o ADR-0058 §"Discriminated union".
/// Os 7 names são camelCase para alinhar com a política do projeto —
/// <c>BonusObrigatorio</c>, oitava variante original, foi descartada
/// (ADR-0114, executado pela #853): <c>ConfiguracaoBonusRegional</c> é
/// global ao processo, sem lista de modalidades, tornando a variante
/// incompatível com o agregado real.</para>
/// <para>Exhaustividade do pattern match consumidor é garantida em build:
/// o switch expression no avaliador NÃO usa catch-all, e <c>CS8509</c> é
/// promovido a erro via <c>TreatWarningsAsErrors</c>. Logo, adicionar
/// uma 8ª variante quebra a build até o avaliador absorver o caso.</para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(EtapaObrigatoria), "etapaObrigatoria")]
[JsonDerivedType(typeof(ModalidadesMinimas), "modalidadesMinimas")]
[JsonDerivedType(typeof(DesempateDeveIncluir), "desempateDeveIncluir")]
[JsonDerivedType(typeof(DocumentoObrigatorioParaModalidade), "documentoObrigatorioParaModalidade")]
[JsonDerivedType(typeof(AtendimentoDisponivel), "atendimentoDisponivel")]
[JsonDerivedType(typeof(ConcorrenciaDuplaObrigatoria), "concorrenciaDuplaObrigatoria")]
[JsonDerivedType(typeof(Customizado), "customizado")]
public abstract record PredicadoObrigatoriedade
{
    /// <summary>
    /// Opções canônicas de serialização para
    /// <see cref="PredicadoObrigatoriedade"/>: camelCase + leitura
    /// case-insensitive. Garante que serialização/round-trip em testes,
    /// EF Core jsonb (via <c>ValueObjectConverter</c>) e endpoints HTTP
    /// produzam representações comparáveis.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}

/// <summary>
/// Regra: o edital DEVE incluir uma etapa cujo código corresponde a
/// <paramref name="TipoEtapaCodigo"/>. Em V1, o "código" é comparado
/// contra a propriedade <c>Etapa.Nome</c> do agregado (ordinal,
/// case-insensitive). Quando #455 promover <c>TipoEtapa</c> para entidade
/// com <c>Codigo</c> próprio, a projeção
/// <c>EditalConformidadeView.CodigosTiposEtapaPresentes</c> passa a usar
/// esse <c>Codigo</c> — a regra em si não muda.
/// </summary>
public sealed record EtapaObrigatoria(string TipoEtapaCodigo) : PredicadoObrigatoriedade;

/// <summary>
/// Regra: o edital DEVE oferecer todas as modalidades de concorrência
/// listadas em <paramref name="Codigos"/>. Códigos seguem o naming do
/// enum <c>ModalidadeConcorrencia</c> (AC, LbPpi, LbQ, …).
/// </summary>
public sealed record ModalidadesMinimas(IReadOnlyList<string> Codigos) : PredicadoObrigatoriedade;

/// <summary>
/// Regra: o edital DEVE configurar critério de desempate
/// <paramref name="Criterio"/> (ex.: "Idoso", "Nota da etapa X",
/// "Data de nascimento").
/// </summary>
public sealed record DesempateDeveIncluir(string Criterio) : PredicadoObrigatoriedade;

/// <summary>
/// Regra: a <paramref name="Modalidade"/> indicada DEVE exigir o
/// <paramref name="TipoDocumento"/> indicado.
/// </summary>
public sealed record DocumentoObrigatorioParaModalidade(string Modalidade, string TipoDocumento) : PredicadoObrigatoriedade;

/// <summary>
/// Regra: o edital DEVE oferecer atendimento PcD para todas as necessidades
/// listadas em <paramref name="Necessidades"/>.
/// </summary>
public sealed record AtendimentoDisponivel(IReadOnlyList<string> Necessidades) : PredicadoObrigatoriedade;

/// <summary>
/// Regra: o edital DEVE habilitar concorrência simultânea (Lei 14.723/2023)
/// — candidato cotista concorre em ampla e na modalidade reservada, sendo
/// classificado na situação mais favorável.
/// </summary>
public sealed record ConcorrenciaDuplaObrigatoria : PredicadoObrigatoriedade;

/// <summary>
/// Válvula de escape para regras transitórias que não casam com nenhuma
/// das 6 variantes tipadas acima. Uso é monitorado: o avaliador emite
/// warning a cada execução. Revisão trimestral promove uma variante tipada
/// se houver padrão emergente (ADR-0058 §"válvula de escape").
/// </summary>
/// <remarks>
/// <para><c>Parametros</c> é <see cref="JsonElement"/> (não
/// <see cref="JsonDocument"/>) para evitar gerenciamento de
/// <see cref="IDisposable"/> em um value object imutável — o pattern
/// canônico para JSON aninhado em records é <c>JsonElement</c> clonado.
/// O ADR-0058 fala "JsonDocument parametros" como descrição informal;
/// a forma técnica adotada é <c>JsonElement</c>.</para>
/// </remarks>
public sealed record Customizado(JsonElement Parametros) : PredicadoObrigatoriedade;
