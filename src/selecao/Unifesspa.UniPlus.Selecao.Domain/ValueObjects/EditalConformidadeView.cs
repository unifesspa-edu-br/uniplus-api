namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

/// <summary>
/// Projeção tipada de um <see cref="Edital"/> consumida pelo
/// <c>ValidadorConformidadeEdital</c>. Existe para que o avaliador opere
/// contra uma view determinística de campos comparáveis, sem precisar
/// expandir o agregado <see cref="Edital"/> com propriedades que ainda
/// não estão modeladas no domínio (critérios de desempate explícitos,
/// documentos por modalidade, atendimentos PcD, concorrência dupla).
/// </summary>
/// <remarks>
/// <para>A projeção V1 via <see cref="From(Edital)"/> só preenche os
/// campos que o agregado atual modela: tipos de etapa (via
/// <c>Etapa.Nome</c> — best-effort até #455 trazer <c>TipoEtapa.Codigo</c>),
/// modalidades (via <c>Cota.Modalidade.ToString()</c>) e bônus
/// (via <c>Edital.BonusRegionalHabilitado</c>). Os demais campos saem
/// vazios/false e os predicados correspondentes reprovam por default —
/// comportamento documentado como limitação V1 no PR da Story #459.</para>
/// <para>Testes que precisam exercitar variantes não suportadas pelo
/// agregado (cenário "Aprovada" de <see cref="DesempateDeveIncluir"/>,
/// por exemplo) constroem a view diretamente via construtor, sem passar
/// pelo método <see cref="From(Edital)"/>.</para>
/// </remarks>
public sealed record EditalConformidadeView(
    Guid EditalId,
    IReadOnlyList<string> CodigosTiposEtapaPresentes,
    IReadOnlyList<string> CodigosModalidadesPresentes,
    IReadOnlyList<string> CriteriosDesempateConfigurados,
    IReadOnlyList<DocumentoObrigatoriedadeView> DocumentosObrigatorios,
    IReadOnlyList<string> ModalidadesComBonus,
    IReadOnlyList<string> AtendimentosDisponiveis,
    bool ConcorrenciaDuplaHabilitada)
{
    /// <summary>
    /// Projeta o agregado <see cref="Edital"/> para a view mínima de V1.
    /// Campos sem cobertura no agregado atual saem vazios/false — ver
    /// remarks da classe.
    /// </summary>
    public static EditalConformidadeView From(Edital edital)
    {
        ArgumentNullException.ThrowIfNull(edital);

        IReadOnlyList<string> tiposEtapa = [.. edital.Etapas.Select(e => e.Nome)];
        IReadOnlyList<string> modalidades = [.. edital.Cotas.Select(c => c.Modalidade.ToString())];
        IReadOnlyList<string> modalidadesComBonus = edital.BonusRegionalHabilitado
            ? modalidades
            : [];

        return new EditalConformidadeView(
            EditalId: edital.Id,
            CodigosTiposEtapaPresentes: tiposEtapa,
            CodigosModalidadesPresentes: modalidades,
            CriteriosDesempateConfigurados: [],
            DocumentosObrigatorios: [],
            ModalidadesComBonus: modalidadesComBonus,
            AtendimentosDisponiveis: [],
            ConcorrenciaDuplaHabilitada: false);
    }
}

/// <summary>
/// Par (modalidade, tipo de documento) usado por
/// <see cref="DocumentoObrigatorioParaModalidade"/> para representar
/// quais documentos cada modalidade do edital exige. Modelado como
/// par textual em V1; entidades dedicadas entram em sprint futura.
/// </summary>
public sealed record DocumentoObrigatoriedadeView(string Modalidade, string TipoDocumento);
