namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Vista ativa dos tipos de etapa para consumo cross-módulo (ADR-0056).</summary>
/// <remarks>
/// <paramref name="AdmitePontuacao"/> e <paramref name="AdmiteEliminacao"/> dizem quais
/// caracteres de etapa o tipo admite — é o que permite ao módulo Seleção recusar uma etapa
/// classificatória de um tipo que não compõe a nota final. O Seleção os copia para o snapshot
/// da etapa quando ela é criada ou muda de caráter ou de vínculo — as gravações em que ele já
/// consulta esta vista —, e a partir daí a etapa confere contra o que congelou, como faz com
/// o resto do que traz do cadastro.
/// <para>
/// <paramref name="NotaDeOrigemNoEnem"/> diz que a nota das etapas do tipo vem do ENEM. O Seleção
/// o congela junto com a identidade, quando o vínculo da etapa é criado ou trocado.
/// </para>
/// </remarks>
public sealed record TipoEtapaView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    bool AdmitePontuacao,
    bool AdmiteEliminacao,
    bool NotaDeOrigemNoEnem);
