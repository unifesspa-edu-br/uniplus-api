namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A conclusão do ciclo recursal de cada fase. Três estados são legítimos: a fase que não publica preliminar não
/// declara concluinte; a que publica o definitivo de cada preliminar conclui a si mesma; a que deixa algum preliminar
/// sem o definitivo do mesmo tipo declara qual fase a conclui. A matéria é o tipo de ato, e a declaração vence a
/// inferência. Dentro de uma fase as conferências se encadeiam, porque não faz sentido conferir a ordem de uma concluinte
/// que não está no cronograma; entre fases todas são percorridas. A fase cuja lista de produtos tem parte recusada pela leitura
/// do documento não se julga (não se sabe o que ela publica), e a concluinte nessa situação só se julga pelo que não lê os produtos.
/// A fase cuja concluinte a leitura recusou (chave que nenhuma fase tem) também não se julga, e a concluinte que a leitura
/// recusou como fase não está fora do cronograma: a causa é a da leitura, e o ciclo fica indeterminado em vez de aprovado.
/// </summary>
internal static class ConclusaoDoCicloRecursal
{
    internal static IReadOnlyList<FieldError> Recusas(CronogramaDoProcesso cronograma) =>
    [
        .. cronograma.Fases
            .Select((fase, posicao) => (fase, posicao))
            .Where(static par => !par.fase.ParteRecusadaEm(FaseDeclarada.ListaDosProdutos) && !par.fase.ParteRecusadaEm(FaseDeclarada.CampoDaFaseConcluinte))
            .Select(par => Recusa(cronograma, par.fase) is { } erro
                ? CronogramaDoProcesso.NaFase(par.posicao, "faseConcluinteCodigo", erro)
                : null)
            .OfType<FieldError>(),
    ];

    /// <summary>As fases que abrem o ciclo e declaram uma concluinte que a conferência não pôde julgar: de código ambíguo, ou com a janela invertida a impedir a comparação de início.</summary>
    internal static IReadOnlyList<FaseDeclarada> Indeterminadas(CronogramaDoProcesso cronograma) =>
    [
        .. cronograma.Fases.Where(fase => fase.Atos.PublicaResultadoPreliminar
            && (fase.ParteRecusadaEm(FaseDeclarada.CampoDaFaseConcluinte) || DeclaraConcluinteIndeterminada(cronograma, fase))),
    ];

    private static bool DeclaraConcluinteIndeterminada(CronogramaDoProcesso cronograma, FaseDeclarada fase) =>
        fase.FaseConcluinteCodigo is { } codigo
        && !string.Equals(codigo, fase.Codigo, StringComparison.Ordinal)
        && ConcluinteIndeterminada(cronograma, fase, codigo);

    private static bool ConcluinteIndeterminada(CronogramaDoProcesso cronograma, FaseDeclarada fase, string codigo) =>
        cronograma.CodigoAmbiguo(codigo)
        || (cronograma.FaseDeCodigo(codigo) is { } concluinte ? InicioIncomparavelPelaInversao(fase, concluinte) : cronograma.TemParteRecusadaNaListaDeFases);

    private static bool InicioIncomparavelPelaInversao(FaseDeclarada fase, FaseDeclarada concluinte) =>
        fase.Janela.Inicio is not null
        && concluinte.Janela.Inicio is not null
        && (fase.Janela.Invertida || concluinte.Janela.Invertida);

    private static DomainError? Recusa(CronogramaDoProcesso cronograma, FaseDeclarada fase) =>
        fase.Atos.PublicaResultadoPreliminar ? RecusaDaFaseQueAbreOCiclo(cronograma, fase) : ConclusaoSemPreliminar(fase);

    private static DomainError? ConclusaoSemPreliminar(FaseDeclarada fase) =>
        fase.FaseConcluinteCodigo is { } concluinte
            ? new DomainError(
                "ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar",
                $"A fase '{fase.Codigo}' declara a fase '{concluinte}' como concluinte, mas não publica nenhum resultado preliminar — não há ciclo recursal a concluir.")
            : null;

    private static DomainError? RecusaDaFaseQueAbreOCiclo(CronogramaDoProcesso cronograma, FaseDeclarada fase) =>
        fase.FaseConcluinteCodigo is { } codigo ? RecusaDaConcluinteDeclarada(cronograma, fase, codigo) : ConclusaoNaoDeclarada(fase);

    // Sem declaração, a fase conclui a si mesma só quando publica o definitivo de cada matéria que abre: o definitivo de
    // outro tipo não fecha o ciclo.
    private static DomainError? ConclusaoNaoDeclarada(FaseDeclarada fase) =>
        fase.Atos.TiposComCicloAberto() is [{ } tipoSemDefinitivo, ..]
            ? new DomainError(
                "ProcessoSeletivo.ConclusaoNaoDeclarada",
                $"A fase '{fase.Codigo}' publica o resultado preliminar do ato '{tipoSemDefinitivo}' e não publica o definitivo do mesmo tipo — publique-o na própria fase ou declare qual fase do cronograma a conclui.")
            : null;

    // Apontar para si mesma não descreve travessia nenhuma: ou a fase conclui a própria matéria publicando o definitivo, e
    // então não há o que declarar, ou não conclui, e a concluinte é outra fase.
    private static DomainError? RecusaDaConcluinteDeclarada(CronogramaDoProcesso cronograma, FaseDeclarada fase, string codigo) =>
        string.Equals(codigo, fase.Codigo, StringComparison.Ordinal)
            ? new DomainError(
                "ProcessoSeletivo.ConclusaoDeclaradaEmFaseQueSeConclui",
                $"A fase '{fase.Codigo}' declara a si mesma como concluinte — a fase que encerra o ciclo recursal de outra é sempre distinta dela; para concluir o próprio ciclo, a fase publica o resultado definitivo e não declara concluinte.")
            : RecusaDaConcluinteLocalizada(cronograma, fase, codigo);

    // Código ambíguo suspende só esta fase: não há como saber qual das fases do código é a concluinte; o código que a leitura recusou como fase também.
    private static DomainError? RecusaDaConcluinteLocalizada(CronogramaDoProcesso cronograma, FaseDeclarada fase, string codigo)
    {
        if (cronograma.CodigoAmbiguo(codigo) || (cronograma.TemParteRecusadaNaListaDeFases && cronograma.FaseDeCodigo(codigo) is null))
        {
            return null;
        }

        return cronograma.FaseDeCodigo(codigo) is { } concluinte
            ? RecusaDaConcluinte(fase, concluinte)
            : new DomainError(
                "ProcessoSeletivo.FaseConcluinteForaDoCronograma",
                $"A fase '{fase.Codigo}' declara '{codigo}' como concluinte, e nenhuma fase do cronograma tem esse código.");
    }

    // Sem a lista de produtos inteira da concluinte não se sabe se ela publica o definitivo, e só a ordem e a janela se julgam.
    private static DomainError? RecusaDaConcluinte(FaseDeclarada fase, FaseDeclarada concluinte) =>
        (concluinte.ParteRecusadaEm(FaseDeclarada.ListaDosProdutos) ? null : RecusaDosProdutosDaConcluinte(fase, concluinte))
        ?? ConcluinteAntecede(fase, concluinte)
        ?? ConcluinteComecaAntes(fase, concluinte);

    private static DomainError? RecusaDosProdutosDaConcluinte(FaseDeclarada fase, FaseDeclarada concluinte) =>
        SemResultadoDefinitivo(fase, concluinte)
        ?? SemDefinitivoDoTipo(fase, concluinte)
        ?? DoisDefinitivos(fase, concluinte);

    private static DomainError? SemResultadoDefinitivo(FaseDeclarada fase, FaseDeclarada concluinte) =>
        concluinte.Atos.PublicaResultadoDefinitivo
            ? null
            : new DomainError(
                "ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo",
                $"A fase '{concluinte.Codigo}', declarada como concluinte de '{fase.Codigo}', não publica nenhum resultado definitivo.");

    private static DomainError? SemDefinitivoDoTipo(FaseDeclarada fase, FaseDeclarada concluinte) =>
        fase.Atos.TiposComCicloAberto().FirstOrDefault(tipo => !concluinte.Atos.PublicaResultadoDefinitivoDe(tipo)) is { } tipo
            ? new DomainError(
                "ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo",
                $"A fase '{concluinte.Codigo}', declarada como concluinte de '{fase.Codigo}', não publica o resultado definitivo do ato '{tipo}', o mesmo tipo do preliminar que '{fase.Codigo}' abre.")
            : null;

    // O tipo que a fase já fecha sozinha não tem o que a concluinte fechar: com o definitivo nos dois lugares, o ciclo teria
    // dois definitivos sem regra que diga qual o fecha.
    private static DomainError? DoisDefinitivos(FaseDeclarada fase, FaseDeclarada concluinte) =>
        fase.Atos.TiposFechadosNaPropriaFase().FirstOrDefault(concluinte.Atos.PublicaResultadoDefinitivoDe) is { } tipo
            ? new DomainError(
                ProcessoSeletivoErrorCodes.ConclusaoDuplicada,
                $"A fase '{fase.Codigo}' já publica o resultado definitivo do ato '{tipo}', e a fase '{concluinte.Codigo}', declarada como concluinte, o publica também — o ciclo ficaria com dois definitivos. Remova a declaração da concluinte de '{fase.Codigo}' ou o definitivo repetido de uma das duas fases.")
            : null;

    private static DomainError? ConcluinteAntecede(FaseDeclarada fase, FaseDeclarada concluinte) =>
        concluinte.Ordem <= fase.Ordem
            ? new DomainError(
                "ProcessoSeletivo.FaseConcluinteAntecedeAConcluida",
                $"A fase '{concluinte.Codigo}' (ordem {concluinte.Ordem}) conclui '{fase.Codigo}' (ordem {fase.Ordem}) — a concluinte não pode anteceder a fase que ela encerra.")
            : null;

    // A ordem já garante a precedência declarada; a janela é a segunda evidência, e só existe quando as duas fases a declaram
    // e nenhuma está invertida.
    private static DomainError? ConcluinteComecaAntes(FaseDeclarada fase, FaseDeclarada concluinte) =>
        !fase.Janela.Invertida
        && !concluinte.Janela.Invertida
        && fase.Janela.Inicio is { } inicioConcluida
        && concluinte.Janela.Inicio is { } inicioConcluinte
        && inicioConcluinte < inicioConcluida
            ? new DomainError(
                "ProcessoSeletivo.FaseConcluinteComecaAntesDaConcluida",
                $"A janela da fase '{concluinte.Codigo}' começa em {inicioConcluinte:O}, antes de '{fase.Codigo}' ({inicioConcluida:O}) — a concluinte não pode começar antes da fase que ela encerra.")
            : null;
}
