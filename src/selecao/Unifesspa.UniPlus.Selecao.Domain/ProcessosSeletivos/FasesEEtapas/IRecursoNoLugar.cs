namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O recurso que uma fase ou uma etapa admite, visto por quem procura quem o julga: as bancas dedicadas à 1ª instância, na
/// ordem de quem decide primeiro, as bancas que avaliaram cada item recorrido, o que o lugar deixa sem saber e onde as recusas
/// o apontam.
/// </summary>
internal interface IRecursoNoLugar
{
    RecursoDeclarado Recurso { get; }

    /// <summary>Onde as mensagens dizem que o recurso está: a fase pelo código, a etapa pela posição.</summary>
    string Onde { get; }

    /// <summary>A etapa do recurso e a posição dela; ausente no recurso da fase.</summary>
    EtapaJulgada? Etapa { get; }

    /// <summary>As bancas dedicadas a julgar o recurso em 1ª instância, agrupadas por nível, o que decide primeiro na frente.</summary>
    IReadOnlyList<QuemJulgaORecurso.BancasDoNivel> DedicadasDaPrimeiraInstancia { get; }

    /// <summary>As bancas cuja cópia avalia o mérito e lista o tipo de decisão do item; nenhuma, se o item não tem tipo ou aponta área do ENEM.</summary>
    IReadOnlyList<BancaDeclarada> Avaliadoras(ProcedimentoDeRecursoDeclarado? procedimento);

    /// <summary>Se a leitura recusou parte do que decide quem julga o item.</summary>
    bool Indeterminado(ProcedimentoDeRecursoDeclarado? procedimento);

    FieldError NasBancas(DomainError erro);

    FieldError NaInstanciaSuperior(DomainError erro);

    DomainError SemBanca(TipoMotivoDeDecisao? tipoDeDecisao);
}
