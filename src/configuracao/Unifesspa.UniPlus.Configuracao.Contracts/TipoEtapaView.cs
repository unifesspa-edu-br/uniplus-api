namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Vista ativa dos tipos de etapa para consumo cross-módulo (ADR-0056).</summary>
/// <remarks>
/// <paramref name="AdmitePontuacao"/> e <paramref name="AdmiteEliminacao"/> dizem quais
/// caracteres de etapa o tipo admite — é o que permite ao módulo Seleção recusar uma etapa
/// classificatória de um tipo que não compõe a nota final. Vêm na vista, e não no snapshot
/// congelado da etapa, porque são restrição do cadastro corrente, não identidade do tipo:
/// certame em rascunho segue o catálogo de hoje, e certame publicado já está congelado pelo
/// próprio envelope.
/// </remarks>
public sealed record TipoEtapaView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    bool AdmitePontuacao,
    bool AdmiteEliminacao);
